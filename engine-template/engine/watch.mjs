import {probeClient} from './client-probe.mjs';
import {ClientReadyGate} from './client-readiness.mjs';
import {BotFill,validateFill} from '../host/core/bot-fill.mjs';
import fs from 'node:fs/promises';import path from 'node:path';import {fileURLToPath} from 'node:url';
import {appConfig,resolvePlan} from './bridge.mjs';import {synchronizeAssist,runIsolatedSteps,deathPatchAlreadyApplied,buildEffectsConfig} from './module-cycle.mjs';
import {ConfigStore} from '../host/panel/config-store.mjs';import {Manager} from '../host/panel/manager.mjs';import {LocalAim} from '../host/core/local-aim.mjs';import {PlayerEffects} from '../host/core/player-effects.mjs';import {DeathFast} from '../host/core/death-fast.mjs';import {LocalAssistRuntime} from '../host/core/bf4-local-assist-runtime.mjs';
const root=path.dirname(path.dirname(fileURLToPath(import.meta.url))),host=path.join(root,'host'),parent=Number(process.argv[2]);
const store=new ConfigStore(path.join(host,'settings'),path.join(host,'panel')),manager=new Manager(store),aim=new LocalAim(path.join(host,'core/local-fun'),{root:host,manager,store}),effects=new PlayerEffects(host,aim),death=new DeathFast(host);
const botFill=new BotFill(manager);let lastFill='';await botFill.start();
setInterval(async()=>{try{const raw=await fs.readFile(path.join(host,'panel/bot-fill.json'),'utf8');if(raw!==lastFill){const cfg=validateFill(JSON.parse(raw.replace(/^\uFEFF/,'')));botFill.config=cfg;botFill.refillSince=0;botFill.lastNativeCheck=null;lastFill=raw;}const file=path.join(root,'data/bot-fill-state.json'),temp=file+'.tmp';await fs.writeFile(temp,JSON.stringify({...botFill.snapshot(),at:Date.now()}));await fs.rename(temp,file);}catch{}},1500);
const clientGate=new ClientReadyGate();
let assist=null,lastEffects='',lastGame=0,lastDeathAttempt=0,lastAttemptPid=0,applying=false,closed=false;
async function status(value){const file=path.join(root,'data/module-state.json'),tmp=file+'.tmp';await fs.writeFile(tmp,JSON.stringify({...value,at:Date.now()}));await fs.rename(tmp,file);}
async function close(){if(closed)return;closed=true;clearInterval(timer);await botFill.stop().catch(()=>{});await assist?.close();await effects.close().catch(()=>{});await aim.close().catch(()=>{});manager.close();process.exit(0);}
let lastProbeError='',lastProbeErrorAt=0;
async function logProbeError(error){
 const detail=error?.stack||String(error);if(detail===lastProbeError&&Date.now()-lastProbeErrorAt<30000)return;
 lastProbeError=detail;lastProbeErrorAt=Date.now();const directory=path.join(root,'logs');await fs.mkdir(directory,{recursive:true});await fs.appendFile(path.join(directory,'client-detection.log'),new Date().toISOString()+' '+detail+'\n');
}
async function findClient(){return probeClient({run:(...args)=>aim.run(...args),powerShell:process.env.BF4_LAUNCHER_PS||'C:/Windows/System32/WindowsPowerShell/v1.0/powershell.exe',script:path.join(host,'core/Probe-Client.ps1'),expected:path.join(host,'mods/client/bf4_no_drm.exe'),gate:clientGate,onError:logProbeError});}

async function tick(){
 if(applying||closed)return;applying=true;
 try{
  try{process.kill(parent,0);}catch{await close();return;}
  const c=await appConfig();resolvePlan(c);const active=c.mods.filter(m=>m.enabled);
  const client=await findClient();const wantedEffects=buildEffectsConfig(client.ready?active:[]);
  const vision=active.filter(m=>m.manifest.adapter==='vision');let pid=client.ready?client.pid:0;
  const modules=await runIsolatedSteps([
   {key:'effects',run:async()=>{
    const desired=JSON.stringify(wantedEffects);if(desired!==lastEffects){const before=await effects.snapshot();await effects.save({config:wantedEffects,revision:before.revision});lastEffects=desired;}
    const live=await effects.snapshot();const enabled=active.some(m=>m.manifest.adapter==='player-effects');
    return {state:!enabled?'disabled':!client.ready?'waiting-client-window':live.status?.active?'applied':'waiting-player',message:!enabled?'未启用':!client.ready?'等待游戏窗口稳定后加载':live.status?.status||'等待步兵部署',feedback:live};
   }},
   {key:'vision',run:async()=>{
    if(!client.ready){if(assist){await assist.close();assist=null;}return {state:vision.length?'waiting-client-window':'disabled',message:vision.length?'等待游戏窗口稳定后加载':'未启用'};}
    const desired={skipDeath:false,teamSpawn:vision.some(m=>m.manifest.id==='team-spawn'),autoSpot:vision.some(m=>m.manifest.id==='auto-spot'),skipKey:'MouseRight'};
    if(vision.length&&!assist){await fs.writeFile(path.join(host,'settings/local-assist.json'),JSON.stringify({version:1,...desired}));assist=new LocalAssistRuntime(host,manager);await assist.start();}
    if(!assist)return {state:'disabled',message:'未启用'};
    await synchronizeAssist(assist,desired);const live=await assist.snapshot();
    return {state:live.applied?'applied':!vision.length?'disabled':live.visionRestartRequired||live.teamRestartRequired?'pending-restart':'waiting-client',message:live.applied?'已载入当前客户端':live.message,feedback:live};
   }},
   {key:'client',run:async()=>({state:client.state,pid:client.pid,message:client.message})},
   {key:'death',run:async()=>{
    const enabled=active.some(m=>m.manifest.adapter==='death-fast');if(!enabled)return {state:lastGame===pid&&pid>0?'pending-restart':'disabled',message:lastGame===pid&&pid>0?'下次启动游戏时关闭':'未启用'};
    if(!client.ready)return {state:'waiting-client-window',message:'等待游戏窗口稳定后加载'};
    let live=await death.snapshot();if(deathPatchAlreadyApplied(live,pid))lastGame=pid;
    if(pid>0&&pid!==lastGame&&(pid!==lastAttemptPid||Date.now()-lastDeathAttempt>10000)){
     lastAttemptPid=pid;lastDeathAttempt=Date.now();live=await death.install(pid);if(live.state==='applied')lastGame=pid;
    }
    return {state:live.state==='applied'?'applied':live.state==='error'?'error':pid?'waiting':'waiting-client',message:live.state==='applied'?'死亡卡片 1 秒已加载':live.message||'等待游戏启动',feedback:live};
   }}
  ]);
  const parts=[];for(const [key,label,selected]of [['death','死亡卡片',active.some(m=>m.manifest.adapter==='death-fast')],['effects','玩家效果',active.some(m=>m.manifest.adapter==='player-effects')],['vision','辅助功能',vision.length>0]])if(selected||modules[key]?.state==='pending-restart')parts.push(label+'：'+modules[key].message);
  if(['error','permission-required'].includes(modules.client.state))parts.push('客户端检测：'+modules.client.message);
  await status({summary:parts.join('  /  ')||'没有启用客户端模组。',modules,death:modules.death.feedback,effects:modules.effects.feedback,assist:modules.vision.feedback});
 }catch(error){await logProbeError(error).catch(()=>{});await status({summary:'模组状态更新未完成，请查看运行日志。',error:error.message}).catch(()=>{});}
 finally{applying=false;}
}
const stopPoll=setInterval(async()=>{if(process.env.BF4_WATCH_STOP_FILE&&await fs.access(process.env.BF4_WATCH_STOP_FILE).then(()=>true,()=>false)){clearInterval(stopPoll);await close();}},300);
const timer=setInterval(()=>tick(),2200);process.on('SIGTERM',close);process.on('SIGINT',close);tick();
