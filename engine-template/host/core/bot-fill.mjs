import fs from 'node:fs/promises';
import path from 'node:path';
import { spawn } from 'node:child_process';
import { fileURLToPath } from 'node:url';
import { parsePlayers } from '../panel/manager.mjs';
import {autoTeamPlan,teamPlan,arrangeBotTeams} from './bot-teams.mjs';

const here=path.dirname(fileURLToPath(import.meta.url));
export function validateFill(value) {
  if(!value || typeof value.enabled!=='boolean' || !Number.isInteger(value.target) || value.target<1 || value.target>64 || !Number.isInteger(value.reserve) || value.reserve<0 || value.reserve>8 || value.reserve>=value.target) throw new Error('填充人数为 1～64，预留席位为 0～8 且小于填充人数');
  const mode=value.mode??'auto',team1=value.team1??0,team2=value.team2??0;
  if(!['auto','teams'].includes(mode))throw new Error('未知 Bot 填充方式');
  if(![team1,team2].every(n=>Number.isInteger(n)&&n>=0&&n<=64)||team1+team2>64)throw new Error('两队 Bot 数量须为 0～64 的整数，合计不能超过 64');
  return {enabled:value.enabled,target:value.target,reserve:value.reserve,mode,team1,team2};
}
export function fillPlan(players,maximum,config) {
  return autoTeamPlan(players,maximum,config);
}
export async function nativeCount(instance,count=-1,expected=-1) {
  if(instance!=='host' || !Number.isInteger(count)||count < -1 || count>64 || !Number.isInteger(expected)||expected < -1||expected>64)throw new Error('Invalid native Bot request');
  const exe=(process.env.BF4_LAUNCHER_PS||path.join(process.env.SystemRoot||'C:/Windows','System32/WindowsPowerShell/v1.0/powershell.exe'));
  const args=['-NoLogo','-NoProfile','-NonInteractive','-ExecutionPolicy','Bypass','-File',path.join(here,'Set-BotCount.ps1'),'-Count',String(count),'-Expected',String(expected)];
  return new Promise((resolve,reject)=>{
    const child=spawn(exe,args,{windowsHide:true,stdio:['ignore','pipe','pipe']});
    let out='',err='',settled=false;
    const finish=(e,v)=>{if(settled)return;settled=true;clearTimeout(timer);e?reject(e):resolve(v);};
    const timer=setTimeout(()=>{child.kill();finish(new Error('Bot 参数访问超时'));},8000);
    child.stdout.on('data',b=>{out+=b;if(out.length>32768){child.kill();finish(new Error('Bot 参数输出异常'));}});
    child.stderr.on('data',b=>{err=(err+b).slice(-4096);});
    child.once('error',e=>finish(e));
    child.once('exit',code=>{
      if(code!==0)return finish(new Error('Bot 参数操作失败：'+(err.trim()||code)));
      try{const result=JSON.parse(out.trim());if(!Number.isInteger(result.observed)||result.observed<0||result.observed>64)throw Error('Bot 参数回读异常');finish(null,result.observed);}catch(e){finish(e);}
    });
  });
}
export class BotFill {
  constructor(manager,options={}) {
    this.manager=manager;this.instance=options.instance||'host';this.file=options.file||path.join(manager.store.panelRoot,'bot-fill.json');
    this.native=options.native||nativeCount;this.clock=options.clock||Date.now;
    this.config=validateFill({enabled:false,target:64,reserve:1});this.status={state:'disabled'};
    this.balanceFile=this.file+'.balance.json';this.balanceOwned=false;
    this.pending=false;this.suspended=false;this.refillSince=0;this.timer=null;this.stopped=true;this.lastEvent='';
    this.lastNativeCheck=null;
  }
  async start(){try{this.config=validateFill(JSON.parse(await fs.readFile(this.file,'utf8')));}catch(e){if(e.code!=='ENOENT')throw e;}try{this.balanceOwned=JSON.parse(await fs.readFile(this.balanceFile,'utf8')).restore===true;}catch(e){if(e.code!=='ENOENT')throw e;}this.stopped=false;this.schedule(0);}
  schedule(ms=1000){if(!this.stopped){this.timer=setTimeout(()=>this.tick().finally(()=>this.schedule()),ms);this.timer.unref?.();}}
  async stop(){this.stopped=true;clearTimeout(this.timer);while(this.pending)await new Promise(r=>setTimeout(r,50));}
  snapshot(){return {...this.status,...this.config};}
  async save(value){
    const config=validateFill(value);
    if(config.enabled&&config.mode==='teams'){
      const saved=await this.manager.store.publicConfig(),live=await this.manager.state();
      const maximum=Math.min(saved.values.maxPlayers,live.online?live.maxPlayers:64);
      const plan=teamPlan(live.online?live.players:[],maximum,config);
      if(config.team1+config.team2>maximum-plan.humans)throw new Error(`当前最多可设置 ${Math.max(0,maximum-plan.humans)} 个 Bot（容量 ${maximum}，真人 ${plan.humans}）`);
    }
    await fs.writeFile(this.file+'.tmp',JSON.stringify(config,null,2));await fs.rename(this.file+'.tmp',this.file);
    this.config=config;this.refillSince=0;this.lastNativeCheck=null;this.status={state:config.enabled?'settling':this.balanceOwned?'restoring':'disabled'};
    return this.snapshot();
  }
  async balance(r,teams){
    if(!teams&&!this.balanceOwned)return;
    const desired=teams?false:(await this.manager.store.publicConfig()).values.autoBalance;
    if(typeof desired!=='boolean')throw new Error('无法读取队伍平衡设置');
    const observed=await r.command('vars.autoBalance');
    if(observed[0]!=='OK'||!['true','false'].includes(observed[1]))throw new Error('无法读取实际队伍平衡状态');
    if(teams&&!this.balanceOwned){await fs.writeFile(this.balanceFile,JSON.stringify({restore:true}));this.balanceOwned=true;}
    if(observed[1]!==String(desired)){
      const result=await r.command('vars.autoBalance',String(desired));
      if(result[0]!=='OK')throw new Error('队伍平衡设置失败：'+result.join(' '));
      const check=await r.command('vars.autoBalance');
      if(check[0]!=='OK'||check[1]!==String(desired))throw new Error('队伍平衡设置尚未生效');
    }
    if(!teams){await fs.rm(this.balanceFile,{force:true});this.balanceOwned=false;}
  }
  async pause(){this.suspended=true;while(this.pending)await new Promise(r=>setTimeout(r,50));}
  resume(){this.suspended=false;this.refillSince=0;this.lastNativeCheck=null;}
  async record(event){
    const key=JSON.stringify(event);if(key===this.lastEvent)return;this.lastEvent=key;
    await fs.appendFile(path.join(this.manager.store.panelRoot,'bot-fill-events.jsonl'),JSON.stringify({at:new Date(this.clock()).toISOString(),...event})+'\n').catch(()=>{});
  }
  async tick(){
    if(this.pending||this.suspended)return;
    if(!this.config.enabled&&!this.balanceOwned){this.status={state:'disabled'};return;}
    this.pending=true;
    try {
      if(!this.config.enabled){await this.manager.withRcon(r=>this.balance(r,false));this.status={state:'disabled'};return;}
      const config=this.config,teams=config.mode==='teams';let gameKey;
      const plan=await this.manager.withRcon(async r=>{
        const info=await r.command('serverInfo');
        if(info[0]!=='OK'||!info.includes('IN_GAME'))throw Error('等待游戏服进入对局');
        if(info[4]!=='ConquestLarge0')throw Error('当前模式 '+info[4]+' 不在本包的 Bot 支持范围，请加载已配置的大型征服地图轮换。');
        gameKey=JSON.stringify([info[1],info[3],info[4],info[5],info[6]]);
        await this.balance(r,teams);
        const players=parsePlayers(await r.command('admin.listPlayers','all'));
        return teams?teamPlan(players,Number(info[3]),config):fillPlan(players,Number(info[3]),config);
      });
      if(this.suspended||!this.config.enabled||this.config!==config)return;
      const balanced=plan.bots===plan.desired&&[1,2].every(t=>plan.teams[t]===plan.desiredTeams[t]);
      this.status={...plan,state:balanced?'balanced':'settling',checkedAt:new Date(this.clock()).toISOString()};
      if(plan.bots===plan.desired){
        this.refillSince=0;
        this.lastNativeCheck=null;
        if(!balanced){const moved=await this.manager.withRcon(r=>arrangeBotTeams(r,config,{cancelled:()=>this.suspended||!this.config.enabled||this.config!==config}));await this.record({event:'teams',mode:config.mode||'auto',...moved,desiredTeams:plan.desiredTeams});}
        else await this.record({event:'balanced',humans:plan.humans,bots:plan.bots,teams:plan.teams,...(teams?{}:{reserve:plan.reserve})});
        return;
      }
      if(plan.bots<plan.desired){
        if(!this.refillSince)this.refillSince=this.clock();
        if(this.clock()-this.refillSince<5000){this.status.state='refill-wait';return;}
      }else this.refillSince=0;
      const last=this.lastNativeCheck;
      if(plan.bots<plan.desired&&last?.desired===plan.desired&&last.gameKey===gameKey&&this.clock()-last.at<10000){this.status.state='settling';return;}
      const current=await this.native(this.instance);
      if(this.suspended||!this.config.enabled||this.config!==config)return;
      if(current!==plan.desired){await this.native(this.instance,plan.desired,current);this.status.lastApplied=plan.desired;await this.record({event:'apply',from:current,to:plan.desired,humans:plan.humans});}
      this.lastNativeCheck={desired:plan.desired,gameKey,at:this.clock()};
      this.status.state='settling';
    }catch(e){this.lastNativeCheck=null;this.status={state:'error',error:e.message,checkedAt:new Date(this.clock()).toISOString()};}
    finally{this.pending=false;}
  }
}
