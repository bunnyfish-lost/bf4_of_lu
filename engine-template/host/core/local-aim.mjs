import {isIPv4} from 'node:net';
import fs from 'node:fs/promises';
import path from 'node:path';
import {execFile} from 'node:child_process';
import {promisify} from 'node:util';
import {randomUUID} from 'node:crypto';
import {parsePlayers} from '../panel/manager.mjs';
const run=promisify(execFile),sleep=ms=>new Promise(r=>setTimeout(r,ms));
const powershell=(process.env.BF4_LAUNCHER_PS||path.join(process.env.SystemRoot||'C:/Windows','System32/WindowsPowerShell/v1.0/powershell.exe'));
export function validate(config){
 const trigger=config?.trigger??'ads';
 const extra={godMode:config?.godMode??false,excludeHumans:config?.excludeHumans??true,espEnabled:config?.espEnabled??false,espBoxes:config?.espBoxes??true,espLines:config?.espLines??true,espDistance:config?.espDistance??100,oneHitKill:config?.oneHitKill??false};
 if(['godMode','excludeHumans','espEnabled','espBoxes','espLines','oneHitKill'].some(k=>typeof extra[k]!=='boolean')||!Number.isInteger(extra.espDistance)||(extra.espDistance!==0&&(extra.espDistance<10||extra.espDistance>300)))throw Error('透视距离须为 10～300 米或全图（0），功能开关须为布尔值');
 if(!config||typeof config.enabled!=='boolean'||!['ads','fire'].includes(trigger)||!['head','body'].includes(config.mode)||!Number.isInteger(config.radius)||config.radius<2||config.radius>90||!Number.isInteger(config.strength)||config.strength<5||config.strength>100)throw Error('范围应为 2～89 度或全视野，过渡速度应为 5～100');
 return {...extra,trigger,enabled:config.enabled,mode:config.mode,radius:config.radius,strength:config.strength};
}
export class LocalAim {
 constructor(dir,{root,manager,store,runner=run}={}){
  this.dir=dir;this.root=root;this.manager=manager;this.store=store;this.run=runner;this.busy=false;this.closed=false;
 }
 async read(name){try{return JSON.parse((await fs.readFile(path.join(this.dir,name),'utf8')).replace(/^\uFEFF/,''));}catch{return null;}}
 async atomic(name,value){
  const file=path.join(this.dir,name),temp=file+'.'+randomUUID()+'.tmp';
  try{await fs.writeFile(temp,JSON.stringify(value));await fs.rename(temp,file);}finally{await fs.unlink(temp).catch(e=>{if(e.code!=='ENOENT')throw e;});}
 }
 async scope(){
  if(!this.store)throw Error('本地娱乐模块未绑定安装');
  const {settings}=await this.store.secrets();
  if(!isIPv4(settings.advertiseAddress)||!Number.isInteger(settings.gamePort)||settings.gamePort<1||settings.gamePort>65535)throw Error('当前服务器地址或端口无效');
  return settings;
 }
 async publish(){
  if(this.closed)return;
  if(this.publishing)return this.publishing;
  this.publishing=(async()=>{
   try{
    const settings=await this.scope();
    const roster=await this.manager.withRcon(async(r,liveSettings)=>{
     if(liveSettings.advertiseAddress!==settings.advertiseAddress||liveSettings.gamePort!==settings.gamePort)throw Error('服务器配置已变化');
     const info=await r.command('serverInfo'),endpoint=settings.advertiseAddress+':'+settings.gamePort;
     if(info[0]!=='OK'||!info.includes(endpoint)||!info.includes('IN_GAME'))throw Error('当前服务器尚未进入对局');
     const players=parsePlayers(await r.command('admin.listPlayers','all'));
     return {at:Date.now(),ownedServer:true,server:info[1],endpoint,players};
    });
    if(!this.closed)await this.atomic('roster.json',roster);
   }catch(e){
    await this.atomic('roster.json',{at:Date.now(),ownedServer:false,players:[],error:e.message});
   }
   if(!this.closed)await this.atomic('panel-lease.json',{enabled:true,at:Date.now()});
  })().finally(()=>{this.publishing=null;});
  return this.publishing;
 }
 async startPublishing(){
  if(!this.timer){await this.publish();this.timer=setInterval(()=>this.publish().catch(()=>{}),1000);this.timer.unref();}
 }
 async snapshot(){
  const s=await this.read('status.json');
  if(s&&Date.now()-s.at>=0&&Date.now()-s.at<2500&&s.version==='0.4.3'){
   const native=async name=>fs.stat(path.join(this.dir,name)).then(async x=>Date.now()-x.mtimeMs>=0&&Date.now()-x.mtimeMs<5000?this.read(name):null).catch(()=>null);
   return {...s,running:true,oneHitServer:await native('local-fun-hit-state.json'),godServer:await native('local-god-v2-state.json')};
  }
  const saved=await this.read('aim-settings.json');
  return {running:false,godMode:false,excludeHumans:saved?.excludeHumans??true,enabled:false,espEnabled:false,espBoxes:saved?.espBoxes??true,espLines:saved?.espLines??true,espDistance:saved?.espDistance??100,oneHitKill:false,trigger:saved?.trigger||'ads',mode:saved?.mode||'body',radius:saved?.radius||12,strength:saved?.strength||55,revision:null,status:'应用设置后启动；支持本机和局域网服务器',overlayVisible:false};
 }
 async script(relative){
  return this.run(powershell,['-NoLogo','-NoProfile','-ExecutionPolicy','Bypass','-File',path.join(this.dir,relative)],{windowsHide:true,timeout:20000,maxBuffer:1024*1024});
 }
 async save(body){
  const config=validate(body.config);if(this.busy)throw Error('正在应用娱乐设置，请稍候');if(this.closed)throw Error('面板已关闭');this.busy=true;
  try{
   // Always allow switching everything off, including after a network-mode change.
   if(config.enabled||config.espEnabled||config.oneHitKill||config.godMode)await this.scope();
   let current=await this.snapshot();
   if(current.running&&body.revision!==current.revision)throw Error('设置已更改，请刷新后重试');
   if(!current.running&&!config.enabled&&!config.espEnabled&&!config.oneHitKill&&!config.godMode){
    await this.atomic('aim-settings.json',config);return this.snapshot();
   }
   await this.startPublishing();
   if(!current.running){
    await this.script('Ensure-Aim.ps1');
    const deadline=Date.now()+5000;do{await sleep(150);current=await this.snapshot();}while(!current.running&&Date.now()<deadline);
    if(!current.running)throw Error('本地娱乐模块未启动，请检查日志');
   }
   if(config.oneHitKill||config.godMode)await this.script('native/ohk-health/Ensure-OneHit.ps1');
   if(config.godMode)await this.script('native/god-mode/Ensure-God.ps1');
   const id=randomUUID();
   await this.atomic('aim-request.json',{id,at:Date.now(),expectedRevision:current.revision,config});
   const deadline=Date.now()+3500;
   while(Date.now()<deadline){await sleep(100);const s=await this.snapshot();if(s.running&&s.requestId===id){if(s.requestError)throw Error(s.requestError);return s;}}
   throw Error('未收到本地娱乐模块确认，请刷新查看实际状态');
  }finally{this.busy=false;}
 }
 async close(){
  this.closed=true;clearInterval(this.timer);this.timer=null;await this.publishing;
  if(!this.root)return;
  await this.atomic('panel-lease.json',{enabled:false,at:Date.now()});
  await this.script('../Stop-LocalFun.ps1');
 }
}
