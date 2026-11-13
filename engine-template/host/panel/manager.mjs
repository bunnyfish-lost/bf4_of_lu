import {RconPool} from '../core/rcon-pool.mjs';
import {enrichBotProgress} from '../core/bot-progress.mjs';
import {readLiveRules} from './rules.mjs';
import fs from 'node:fs/promises';
import path from 'node:path';
import net from 'node:net';
import os from 'node:os';
import { createHash, randomUUID } from 'node:crypto';
import { spawn } from 'node:child_process';
import { Rcon } from '../core/rcon.mjs';
import { PlayerAddressReader, isRadminAddress } from '../core/player-addresses.mjs';

export function parsePlayers(reply) {
  if(reply[0]!=='OK') throw new Error(`玩家列表：${reply[0]}`);
  const width=Number(reply[1]);
  if(!Number.isInteger(width)||width<1||width>64) throw new Error('玩家列表列数异常');
  const keys=reply.slice(2,2+width), count=Number(reply[2+width]);
  if(!Number.isInteger(count)||count<0||count>1024||reply.length!==3+width+count*width) throw new Error('玩家列表长度异常');
  return Array.from({length:count},(_,i)=>Object.fromEntries(keys.map((key,j)=>[key,reply[3+width+i*width+j]])));
}
export function parseMaps(reply) {
  if(reply[0]!=='OK') throw new Error(`地图列表：${reply[0]}`);
  const count=Number(reply[1]),width=Number(reply[2]);
  if(!Number.isInteger(count)||count<0||count>1024||width!==3||reply.length!==3+count*width) throw new Error('地图列表格式异常');
  return Array.from({length:count},(_,i)=>({index:i,map:reply[3+i*3],mode:reply[4+i*3],rounds:Number(reply[5+i*3])}));
}
export const mapRevision = maps => createHash('sha256').update(JSON.stringify(maps)).digest('hex');
export const botPlayers = players => players.filter(p=>p.guid===''&&String(p.type)==='0');
export function tcpReachable(port) {
  return new Promise(resolve=>{const s=net.createConnection({host:'127.0.0.1',port});const finish=v=>{s.destroy();resolve(v);};s.setTimeout(700,()=>finish(false));s.once('error',()=>finish(false));s.once('connect',()=>finish(true));});
}
export class Manager {
  constructor(store, options={}) {this.store=store;this.connect=options.connect||((port)=>Rcon.connect('127.0.0.1',port,1500));this.runProcess=options.runProcess||runProcess;this.activePort=null;this.proconSource=options.proconSource||path.join(path.dirname(store.panelRoot),'procon-source');this.wait=options.wait||(ms=>new Promise(r=>setTimeout(r,ms)));this.botChecks=options.botChecks??30;this.rconPool=new RconPool(this.connect,options.poolOptions);}
  close(){this.rconPool.close();}
  async withRcon(fn) {
    const {settings,password}=await this.store.secrets();
    if(!this.activePort) try {this.activePort=JSON.parse(await fs.readFile(path.join(this.store.panelRoot,'last-live.json'),'utf8')).port;}catch{}
    const ports=[...new Set([settings.rconPort,this.activePort].filter(p=>Number.isInteger(p)&&p>0&&p<=65535))];
    const key=JSON.stringify([settings.rconPort,password]);
    return this.rconPool.run(key,async connect=>{
      let lastError;
      for(const port of ports){
        let connection;
        try {connection=await connect(port);await connection.login(password);this.activePort=port;return connection;}
        catch(e){connection?.close();lastError=e;}
      }
      throw new Error(`管理连接失败：${lastError?.message||'服务未启动'}`);
    },connection=>fn(connection,settings));
  }

  async state() {
    const {settings}=await this.store.secrets();
    const base={capturedAt:new Date().toISOString(),online:false,advertiseAddress:settings.advertiseAddress,gamePort:settings.gamePort,rconPort:settings.rconPort,players:[],maps:[],interfaces:Object.entries(os.networkInterfaces()).flatMap(([name,items])=>(items||[]).filter(v=>v.family==='IPv4'&&!v.internal).map(v=>({name,address:v.address}))),blazeReachable:await tcpReachable(10040)};
    try {
      const live=await this.withRcon(async r=>{
        const info=await r.command('serverInfo');if(info[0]!=='OK')throw new Error(info[0]);
        const roster=parsePlayers(await r.command('admin.listPlayers','all'));
        this.addressReader??=new PlayerAddressReader(path.dirname(this.store.panelRoot));
        const addressed=await this.addressReader.enrich(roster,settings);
        let players=addressed;try{players=await enrichBotProgress(path.dirname(this.store.panelRoot),addressed);}catch(e){base.botProgressError=e.message;}
        const maps=parseMaps(await r.command('mapList.list'));
        const indices=await r.command('mapList.getMapIndices');
        const rules=await readLiveRules(r);
        return {rules,online:true,radminMode:isRadminAddress(settings.advertiseAddress),name:info[1],playersReported:Number(info[2]),maxPlayers:Number(info[3]),mode:info[4],map:info[5],gameState:info.find(x=>['IN_GAME','PRE_GAME','POST_GAME'].includes(x))||'RCON_ONLINE',endpoint:info.find(x=>/^\d+\.\d+\.\d+\.\d+:\d+$/.test(x))||'',players,maps,mapRevision:mapRevision(maps),currentMapIndex:indices[0]==='OK'?Number(indices[1]):null,nextMapIndex:indices[0]==='OK'?Number(indices[2]):null};
      });
      await fs.writeFile(path.join(this.store.panelRoot,'last-live.json'),JSON.stringify({port:this.activePort,observedAt:base.capturedAt}));
      return {...base,...live,activeRconPort:this.activePort,bots:{count:botPlayers(live.players).length,configured:Number(settings.initialBots)||0,maxDeploy:Math.max(0,Math.min(63,live.maxPlayers-Math.max(1,live.players.filter(p=>p.guid).length)))}};
    }catch(e){return {...base,error:e.message};}
  }
  async playerAction(body) {
    if(!['kick','kill','move'].includes(body.action)) throw new Error('未知玩家操作');
    if(typeof body.name!=='string'||!body.name||body.name.length>128||/[\x00-\x1f]/.test(body.name)) throw new Error('玩家名称无效');
    const reason=String(body.reason||'').trim();
    if(reason.length>100||/[\x00-\x1f]/.test(reason))throw new Error('原因请使用 100 字以内的单行文字');
    if(body.action==='kick'&&!reason)throw new Error('请填写踢出原因');
    return this.withRcon(async r=>{
      const players=parsePlayers(await r.command('admin.listPlayers','all'));
      const matches=players.filter(p=>p.name===body.name);
      if(matches.length!==1||(body.guid!==undefined&&matches[0].guid!==body.guid))throw new Error('玩家已离开或身份发生变化，请刷新列表');
      const currentTeam=Number(matches[0].teamId);
      if(body.action==='move'&&![1,2].includes(currentTeam))throw new Error('该玩家尚未加入对战队伍');
      const destination=currentTeam===1?2:1;
      const command=body.action==='kick'?['admin.kickPlayer',body.name,reason]:body.action==='kill'?['admin.killPlayer',body.name]:['admin.movePlayer',body.name,String(destination),'0','true'];
      const result=await r.command(...command);if(result[0]!=='OK')throw new Error(`服务端拒绝：${result.join(' ')}`);
      return {accepted:true,...(body.action==='move'?{team:destination}:{}),message:body.action==='move'?`已将 ${body.name} 调至队伍 ${destination}`:body.action==='kill'?`已执行击杀：${body.name}`:`已踢出：${body.name}`};
    });
  }
  async killPlayers(body) {
    if(!['team','all'].includes(body.scope)||(body.scope==='team'&&![1,2].includes(body.team)))throw new Error('击杀范围无效');
    if(!Array.isArray(body.targets)||!body.targets.length||body.targets.length>64)throw new Error('请选择 1～64 名对战玩家');
    const names=new Set();
    const targets=body.targets.map(p=>{
      if(!p||typeof p.name!=='string'||!p.name||p.name.length>128||/[\x00-\x1f]/.test(p.name)||typeof p.guid!=='string'||p.guid.length>128||/[\x00-\x1f]/.test(p.guid)||![1,2].includes(Number(p.teamId)))throw new Error('玩家快照无效，请刷新列表');
      if(names.has(p.name))throw new Error('玩家快照包含重复名称');
      if(body.scope==='team'&&Number(p.teamId)!==body.team)throw new Error('玩家不属于目标队伍');
      names.add(p.name);return {name:p.name,guid:p.guid,teamId:String(p.teamId)};
    });
    return this.withRcon(async r=>{
      const results=[];
      for(const [index,target] of targets.entries()) {
        try {
          const matches=parsePlayers(await r.command('admin.listPlayers','all')).filter(p=>p.name===target.name);
          if(matches.length!==1||matches[0].guid!==target.guid||matches[0].teamId!==target.teamId){results.push({name:target.name,status:'skipped',reason:'玩家已离开、换队或身份发生变化'});continue;}
          const reply=await r.command('admin.killPlayer',target.name);
          if(['SoldierNotAlive','PlayerNotAlive'].includes(reply[0]))results.push({name:target.name,status:'skipped',reason:'当前未存活，无需重复击杀'});
          else results.push({name:target.name,status:reply[0]==='OK'?'accepted':'failed',...(reply[0]==='OK'?{}:{reason:reply.join(' ')})});
        } catch(e) {
          results.push({name:target.name,status:'failed',reason:`执行中断，结果未确认：${e.message}`});
          for(const pending of targets.slice(index+1))results.push({name:pending.name,status:'skipped',reason:'管理连接中断，未发送指令'});
          break;
        }
      }
      const succeeded=results.filter(p=>p.status==='accepted').length;
      const failed=results.filter(p=>p.status==='failed').length;
      const skipped=results.filter(p=>p.status==='skipped').length;
      return {accepted:failed===0&&skipped===0,scope:body.scope,team:body.scope==='team'?body.team:null,total:targets.length,succeeded,failed,skipped,results,message:`已执行 ${succeeded} 人 · 失败 ${failed} 人 · 跳过 ${skipped} 人`};
    });
  }
  async mapAction(body) {
    if(!['next','restart','switch','queue'].includes(body.action))throw new Error('未知地图操作');
    return this.withRcon(async r=>{
      if(['switch','queue'].includes(body.action)) {
        const maps=parseMaps(await r.command('mapList.list'));
        if(body.revision!==mapRevision(maps))throw new Error('运行中的地图列表已变化，请刷新后重试');
        if(!Number.isInteger(body.index)||body.index<0||body.index>=maps.length)throw new Error('地图索引无效');
        const set=await r.command('mapList.setNextMapIndex',String(body.index));if(set[0]!=='OK')throw new Error(set.join(' '));
      }
      if(body.action!=='queue'){
        const result=await r.command(body.action==='restart'?'mapList.restartRound':'mapList.runNextRound');
        if(result[0]!=='OK')throw new Error(`服务端拒绝：${result.join(' ')}`);
      }
      return {accepted:true,message:body.action==='queue'?'已设置下一张地图':'服务端已接受切换指令，正在等待状态更新'};
    });
  }
  async deployBots(count) {
    if(!Number.isInteger(count)||count<0||count>63)throw new Error('Bot 数量请输入 0～63 的整数');
    const config=await this.store.publicConfig();
    const current=await this.state();
    if(!current.online)throw new Error('请先启动游戏服');
    const reserve=Math.max(1,current.players.filter(p=>p.guid).length);
    const maximum=Math.max(0,Math.min(63,config.values.maxPlayers-reserve));
    if(count>maximum)throw new Error(`当前最多部署 ${maximum} 个 Bot，需为 ${reserve} 名玩家保留席位`);
    const saved=await this.store.save({...config,values:{...config.values,initialBots:count}});
    await this.control('RestartGame');
    let observed=null,consecutive=0;
    for(let i=0;i<this.botChecks;i++){
      await this.wait(1500);
      const live=await this.state();
      observed=live.online?botPlayers(live.players).length:null;
      consecutive=live.online&&live.gameState==='IN_GAME'&&observed===count?consecutive+1:0;
      if(consecutive>=2)return {accepted:true,count,observed,backup:saved.backup,message:count?`已部署 ${count} 个 Bot`:'已清空 Bot'};
    }
    throw new Error(`Bot 配置已保存，实际人数未达到目标。目标 ${count}，当前 ${observed??'服务器未连接'}；请查看游戏服状态。`);
  }
  async announce(text) {
    if(typeof text!=='string'||!text.trim()||text.length>120||/[\x00-\x1f]/.test(text))throw new Error('公告请输入 1～120 字单行文字');
    return this.withRcon(async r=>{const result=await r.command('admin.say',text,'all');if(result[0]!=='OK')throw new Error(result.join(' '));return {accepted:true};});
  }
  async control(action) {
    if(!['Start','Stop','Restart','RestartGame','Procon'].includes(action))throw new Error('未知控制操作');
    if(action==='Procon')await this.prepareProcon();
    const {password}=await this.store.secrets();
    try {
      const result=await this.runProcess(path.join(this.store.panelRoot,'Panel-Process.ps1'),action);
      return {message:action==='Procon'?'已请求打开 Procon':'开服脚本执行完毕，请核对运行状态',output:String(result).split(password).join('[已隐藏]')};
    }catch(e){throw new Error(String(e.message).split(password).join('[已隐藏]'));}
  }
  async prepareProcon() {
    const dest=path.join(this.store.panelRoot,'procon-local');
    const marker=path.join(dest,'panel-connection.json');
    const {settings,password}=await this.store.secrets();
    const connection={port:this.activePort||settings.rconPort,credentialHash:createHash('sha256').update(password).digest('hex')};
    try {
      const old=JSON.parse(await fs.readFile(marker,'utf8'));
      if(old.port!==connection.port||old.credentialHash!==connection.credentialHash)throw new Error('Procon 的连接参数已变化。请退出 Procon 后，在其连接设置中更新管理端口或密码；现有配置未覆盖。');
      await fs.access(path.join(dest,'PRoCon.exe'));return dest;
    }catch(e){if(e.code!=='ENOENT')throw e;}
    try {await fs.access(path.join(this.proconSource,'PRoCon.exe'));}catch {throw new Error('尚未导入 Procon，请运行 07-Import-Procon.cmd；网页管理功能可直接使用。');}
    await fs.mkdir(dest,{recursive:true});
    const files=['PRoCon.exe','PRoCon.exe.config','PRoCon.Core.dll','Ionic.Zip.Reduced.dll','MySql.Data.dll','PRoCon.xml'];
    for(const file of files)await fs.copyFile(path.join(this.proconSource,file),path.join(dest,file));
    for(const dir of ['Localization','Media','docs_licenses'])await fs.cp(path.join(this.proconSource,dir),path.join(dest,dir),{recursive:true});
    await fs.mkdir(path.join(dest,'Configs'),{recursive:true});await fs.mkdir(path.join(dest,'Plugins/BF4'),{recursive:true});
    for(const file of await fs.readdir(path.join(this.proconSource,'Configs')))if(file.endsWith('.def'))await fs.copyFile(path.join(this.proconSource,'Configs',file),path.join(dest,'Configs',file));
    if(!/^[a-f0-9]{16}$/i.test(password))throw new Error('RCON 密码格式与安装配置不符');
    const config=[
      'procon.private.options.setLanguage "cn.loc"',
      'procon.private.options.autoCheckDownloadUpdates False',
      'procon.private.options.autoApplyUpdates False',
      'procon.private.options.autoCheckGameConfigsForUpdates False',
      'procon.private.options.allowanonymoususagedata False',
      'procon.private.options.runPluginsInSandbox True',
      'procon.private.options.layerHideLocalPlugins False',
      'procon.private.options.layerHideLocalAccounts False',
      'procon.private.options.ShowCfmMsgRoundRestartNext True',
      'procon.private.httpWebServer.enable False 27360 "127.0.0.1"',
      `procon.private.servers.add "127.0.0.1" ${connection.port} "${password}"`,
      `procon.private.servers.name "127.0.0.1" ${connection.port} "BF4 Local Host"`
    ].join('\r\n')+'\r\n';
    await fs.writeFile(path.join(dest,'Configs/procon.cfg'),config,'utf8');
    await fs.writeFile(path.join(dest,'Configs/accounts.cfg'),'// Local host configuration. No shared accounts imported.\r\n');
    await fs.writeFile(marker,JSON.stringify(connection,null,2));
    return dest;
  }
}
export async function runProcess(script,action,options={}) {
  const spawnFn=options.spawnFn||spawn;
  const systemRoot=options.systemRoot||(typeof process!=='undefined'?process.env.SystemRoot:'C:/Windows');
  const exe=path.join(systemRoot||'C:/Windows','System32/WindowsPowerShell/v1.0/powershell.exe');
  const logRoot=options.logRoot||path.join(path.dirname(script),'runtime/control');
  await fs.mkdir(logRoot,{recursive:true});
  const id=new Date().toISOString().replace(/[:.]/g,'-')+'-'+randomUUID().slice(0,8);
  const outPath=path.join(logRoot,id+'.stdout.log'),errPath=path.join(logRoot,id+'.stderr.log');
  const out=await fs.open(outPath,'w'),err=await fs.open(errPath,'w');
  try {
    // Descendant game processes can inherit standard handles. Files + the wrapper's
    // exit event prevent waiting for an inherited pipe until the game server stops.
    await new Promise((resolve,reject)=>{
      const child=spawnFn(exe,['-NoLogo','-NoProfile','-ExecutionPolicy','Bypass','-File',script,'-Action',action],{windowsHide:true,stdio:['ignore',out.fd,err.fd]});
      const timer=setTimeout(()=>{child.kill();reject(new Error('开服脚本超过 180 秒。请查看实际服务状态和 runtime/control 日志后再操作。'));},options.timeoutMs||180000);
      child.once('error',e=>{clearTimeout(timer);reject(e);});
      child.once('exit',(code,signal)=>{clearTimeout(timer);if(code===0)resolve();else reject(new Error(`脚本退出码 ${code ?? signal}`));});
    });
    return await readTail(outPath);
  } catch(e) {
    const detail=(await readTail(errPath))||(await readTail(outPath));
    throw new Error(`${e.message}${detail?'\n'+detail:''}`);
  } finally {await out.close();await err.close();}
}
async function readTail(file) {
  const h=await fs.open(file,'r');
  try {const st=await h.stat();const size=Math.min(st.size,32768);const b=Buffer.alloc(size);await h.read(b,0,size,st.size-size);return b.toString('utf8');}finally{await h.close();}
}
