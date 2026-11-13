import fs from 'node:fs/promises';
import path from 'node:path';
import {randomUUID} from 'node:crypto';
import {execFile} from 'node:child_process';
import {promisify} from 'node:util';
const run=promisify(execFile),sleep=ms=>new Promise(r=>setTimeout(r,ms));
const errors={ '-1':'刷车请求已过期，请重试','-2':'车辆回收或生成未能及时完成，请检查角色和服务器状态','-3':'请先以步兵部署到室外空地，并下车后再生成载具','-4':'当前地图没有该车型的已加载资源或受支持的生成方式','-5':'本次服务的手动车辆记录已满，请清理车辆','-6':'生成过于频繁，请稍等两秒','-7':'原生生成点未能创建载具，可能已达到该生成点的网络编号容量','-9':'原生载具模块出现异常，请停止刷车并查看诊断记录'};
const key=x=>x.toLowerCase().replace(/_night$/,'').replace(/[^a-z0-9]/g,'');
export function groupVehicles(catalog,paths){return catalog.groups.map(g=>({...g,vehicles:g.vehicles.map(v=>{const keys=new Set([v.id,...(v.aliases||[])].map(key));if(v.id==='Z-10w')keys.add('chahez10w');const matches=paths.filter(p=>keys.has(key(p.split('/').at(-1))));return {...v,available:matches.length>0,path:matches[0]||null};})}));}
export function validateVehicleAction(b){if(!b||!['refresh','spawn','limit','clear'].includes(b.action))throw Error('未知刷车操作');if(b.action==='limit'&&(typeof b.enabled!=='boolean'||(!b.enabled&&b.confirmed!==true)))throw Error('关闭上限需要确认：可能会导致卡顿或者服务器崩溃。');if(b.action==='spawn'&&(typeof b.model!=='string'||b.model.length>255||typeof b.player!=='string'||!b.player.trim()||Buffer.byteLength(b.player.trim(),'utf8')>63||/[\r\n\0]/.test(b.player+b.model)))throw Error('请选择车型并填写正确的玩家 ID');return b;}
export class Vehicles{
 constructor(root,{runner=run}={}){this.root=root;this.dir=path.join(root,'core/local-fun');this.run=runner;this.sequence=Date.now()*100;this.busy=false;this.startedFor=null;}
 async record(){const x=JSON.parse(await fs.readFile(path.join(this.root,'runtime/processes.json'),'utf8'));const r=(x.processes||x).find(x=>x.role==='game');if(!r)throw Error('游戏服未启动');return r;}
 async config(){try{const c=JSON.parse(await fs.readFile(path.join(this.root,'settings/vehicle-spawn.json'),'utf8'));return {limitEnabled:c.limitEnabled!==false};}catch(e){if(e.code!=='ENOENT')throw e;return {limitEnabled:true};}}
 async state(){try{return JSON.parse(await fs.readFile(path.join(this.dir,'vehicle-state-v2.json'),'utf8'));}catch{return null;}}
 async snapshot(){const [catalog,config,raw,r]=await Promise.all([fs.readFile(path.join(this.dir,'vehicle-catalog.json'),'utf8').then(JSON.parse),this.config(),this.state(),this.record().catch(()=>null)]);const valid=raw?.pid===r?.pid&&raw?.installed===true&&raw.at>=Number(r.startTicks)/10000-62135596800000;const state=valid?raw:null;return {running:!!state,busy:this.busy,limitEnabled:state?.limitEnabled??config.limitEnabled,count:state?.count??0,groups:groupVehicles(catalog,state?.catalog||[]),lastUpdated:state?.at||null,vehicles:state?.vehicles?.map(v=>({id:v.id,retiring:v.retiring}))||[],message:state?'仅统计本模块在当前游戏服中手动生成的载具。':'点击“读取本图载具”检查已加载资源；不额外加载其他车型。'};}
 async atomic(file,content){const tmp=file+'.'+randomUUID()+'.tmp';await fs.writeFile(tmp,content);try{await fs.rename(tmp,file);}finally{await fs.unlink(tmp).catch(()=>{});}}
 async command(action,fields={}){const requestId=String(++this.sequence),expires=Date.now()+20000;const text='[Vehicle]\nSequence='+requestId+'\nExpires='+expires+'\nAction='+action+'\nLimit='+(fields.enabled===false?'0':'1')+'\nPlayerHex='+Buffer.from(fields.player||'').toString('hex')+'\nModelHex='+Buffer.from(fields.model||'').toString('hex')+'\n';await this.atomic(path.join(this.dir,'vehicle-command-v2.ini'),text);for(let i=0;i<220;i++){await sleep(100);const s=await this.state();if(s?.requestId===requestId){if(s.result<0)throw Error(errors[s.result]||'刷车操作失败');return s;}}throw Error('刷车模块未响应；请勿连续点击，检查服务器运行状态');}
 async ensure(){const r=await this.record(),identity=r.pid+':'+r.startTicks;if(this.startedFor===identity)return;await this.run((process.env.BF4_LAUNCHER_PS||path.join(process.env.SystemRoot||'C:/Windows','System32/WindowsPowerShell/v1.0/powershell.exe')),['-NoProfile','-ExecutionPolicy','Bypass','-File',path.join(this.dir,'Ensure-Vehicles.ps1')],{windowsHide:true,timeout:15000,maxBuffer:1048576});await this.command(2,{enabled:(await this.config()).limitEnabled});this.startedFor=identity;}
 async spawnM1(player,active=()=>true){
 validateVehicleAction({action:'spawn',player,model:'M1Abrams'});if(this.busy)throw Error('正在处理上一项刷车操作');this.busy=true;
 try{await this.ensure();if(!active())return;await this.command(0);const state=await this.snapshot();const model=state.groups.flatMap(g=>g.vehicles).find(v=>v.id==='M1Abrams'&&v.available)?.path;if(!model)throw Error('当前地图没有可用的 M1 生成点');if(!active())return;return await this.command(1,{player,model});}finally{this.busy=false;}
 }
 async perform(body){validateVehicleAction(body);if(this.busy)throw Error('正在处理上一项刷车操作');this.busy=true;try{await this.ensure();let result;
 if(body.action==='refresh')result=await this.command(0);
 if(body.action==='spawn'){const s=await this.snapshot();if(!s.groups.some(g=>g.vehicles.some(v=>v.available&&v.path===body.model)))throw Error('当前地图未加载该车型或暂不支持生成，请选择可用车型');result=await this.command(1,{player:body.player.trim(),model:body.model});}
 if(body.action==='limit'){result=await this.command(2,{enabled:body.enabled});await this.atomic(path.join(this.root,'settings/vehicle-spawn.json'),JSON.stringify({limitEnabled:body.enabled}));}
 if(body.action==='clear'){if(body.confirmed!==true)throw Error('清空手动车辆需要确认');result=await this.command(3);}
 return {...await this.snapshot(),busy:false,created:result?.created!=='0',removed:result?.removed||0};
 }finally{this.busy=false;}}
}
