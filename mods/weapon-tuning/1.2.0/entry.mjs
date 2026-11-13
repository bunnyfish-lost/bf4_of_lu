import fs from 'node:fs/promises';import path from 'node:path';import {spawn} from 'node:child_process';import {runModule,json,atomic,appendDiagnostic,sleep} from './runtime.mjs';import {Rcon} from './support/rcon.mjs';
const feature='weapon-tuning';
await runModule(import.meta.url,async env=>{
 const inputFile=path.join(env.state,'helper-input.json'),statusFile=path.join(env.state,'helper-status.json'),exe=path.join(env.mod,'bin','BF4WeaponTuning.exe');await fs.access(exe);
 let helper=null,spawnError=null,config={},lastRoster=0,roster={at:0,ownedServer:false,players:[]};
 async function stop(){if(!helper)return;await atomic(inputFile,{instanceId:env.context.instanceId,enabled:false,at:Date.now()});for(let i=0;i<50&&helper.exitCode===null;i++)await sleep(100);if(helper.exitCode===null)helper.kill();helper=null;}
 return {configure:async value=>{if(feature==='enemy-display'&&(!Number.isInteger(value.distance)||(value.distance!==0&&(value.distance<10||value.distance>300))))throw Error('显示距离为10～300米或0（不限距离）');config=value;},async tick(){
  const ctx=env.context;if(!ctx.server||!ctx.client){await stop();await env.status('waiting','等待本实例游戏服和客户端');return;}
  if(feature==='weapon-tuning'){
   const app=await json(path.join(ctx.launcherRoot,'config.json'));const conflict=app.mods?.some(m=>m.enabled&&m.manifest?.adapter==='player-effects'&&['noRecoil','noSpread','noBulletDrop','fastBullets'].some(k=>m.config?.[k]===true));
   if(conflict){await stop();await env.status('conflict','旧版枪械驱动仍在使用，请在列表中选择要使用的版本');return;}
  }
  if(Date.now()-lastRoster>900){lastRoster=Date.now();let r;try{
   const processes=await json(path.join(env.host,'runtime/processes.json'));const game=processes.processes?.find(p=>p.role==='game');if(game?.pid!==ctx.server.pid||game.startTicks!==ctx.server.startTicks)throw Error('服务端实例已变化');
   const settings=await json(path.join(env.host,'settings/host-settings.json')),secret=await json(path.join(env.host,'runtime/private.json'));r=await Rcon.connect('127.0.0.1',settings.rconPort,1200);await r.login(secret.rconPassword);
   const info=await r.command('serverInfo');if(info[0]!=='OK'||!info.includes('IN_GAME')||!info.includes(settings.advertiseAddress+':'+settings.gamePort))throw Error('等待当前服务器对局');
   const reply=await r.command('admin.listPlayers','all');if(reply[0]!=='OK')throw Error('等待玩家名单');const width=Number(reply[1]),count=Number(reply[2+width]),keys=reply.slice(2,2+width);if(!Number.isInteger(width)||width<1||width>64||!Number.isInteger(count)||count<0||count>256||reply.length<3+width+count*width)throw Error('玩家名单格式异常');
   roster={at:Date.now(),ownedServer:true,endpoint:settings.advertiseAddress+':'+settings.gamePort,server:info[1],players:Array.from({length:count},(_,i)=>Object.fromEntries(keys.map((k,j)=>[k,reply[3+width+i*width+j]])))};
  }catch(error){roster={at:Date.now(),ownedServer:false,players:[],error:error.message};}finally{r?.close();}}
  await atomic(inputFile,{instanceId:ctx.instanceId,at:Date.now(),enabled:true,launcherRoot:ctx.launcherRoot,client:ctx.client,server:ctx.server,config,roster,statusFile,stateDirectory:env.state});
  if(!helper){spawnError=null;helper=spawn(exe,['--input',inputFile],{windowsHide:true,stdio:'ignore'});helper.once('error',error=>{spawnError=error;});}
  if(spawnError)throw Error('模组执行程序启动失败：'+spawnError.message);if(helper.exitCode!==null)throw Error('模组执行程序已退出，请查看本模组诊断记录');
  const live=await json(statusFile).catch(()=>null);const fresh=live?.instanceId===ctx.instanceId&&live.pid===helper.pid&&Date.now()-live.at>=0&&Date.now()-live.at<3000;
  await env.status(fresh?live.state:'waiting',fresh?live.message:'等待本实例模组执行程序回执',{helperPid:helper.pid,clientPid:ctx.client.pid,serverPid:ctx.server.pid,phase:fresh?live.phase:null,markers:fresh?live.markers:0,rendered:fresh?live.rendered:0});
 },close:stop};
});
