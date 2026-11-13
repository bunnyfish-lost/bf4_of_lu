import fs from 'node:fs/promises';
import path from 'node:path';
import {fileURLToPath,pathToFileURL} from 'node:url';
import {Rcon} from './rcon.mjs';
import {parsePlayers} from '../panel/manager.mjs';
import {currentStorage,profileSummary} from './player-profiles.mjs';
const read=async p=>JSON.parse((await fs.readFile(p,'utf8')).replace(/^\uFEFF/,''));
const wait=ms=>new Promise(r=>setTimeout(r,ms));
export async function saveConnectedPlayers(root,options={}){
 const settings=await read(path.join(root,'settings/host-settings.json'));
 const secret=await read(path.join(root,'runtime/private.json'));
 const storage=await currentStorage(root);
 let lastPort;try{lastPort=(await read(path.join(root,'panel/last-live.json'))).port;}catch(e){if(e.code!=='ENOENT')throw e;}
 const ports=[...new Set([settings.rconPort,lastPort].filter(Number.isInteger))];
 let r,lastError;
 for(const port of ports)try{r=await(options.connect||Rcon.connect)('127.0.0.1',port,2000);await r.login(secret.rconPassword);break;}catch(e){r?.close();r=null;lastError=e;}
 if(!r)throw Error('Cannot contact the game to save current progress: '+lastError?.message);
 const result={startedAt:new Date().toISOString(),address:storage.address,players:[]};
 const pause=options.wait||wait;
 const reportOf=async id=>(await read(path.join(storage.data,'players',String(id),'player.json'))).AppliedReports||[];
 try{
  for(let batch=0;batch<3;batch++){
   const players=parsePlayers(await r.command('admin.listPlayers','all')).filter(p=>p.guid);
   if(!players.length){result.finishedAt=new Date().toISOString();return result;}
   const profiles=await profileSummary(root),pending=[];
   if(profiles.errors.length)throw Error('Player archive unreadable: '+profiles.errors.join('; '));
   for(const p of players){
    const matches=profiles.players.filter(s=>!s.isBot&&s.name===p.name);
    if(matches.length!==1)throw Error('Cannot match player archive: '+p.name);
    const id=matches[0].id,before=JSON.stringify(await reportOf(id));
    const current=parsePlayers(await r.command('admin.listPlayers','all')).filter(v=>v.name===p.name);
    if(current.length){
     if(current.length!==1||current[0].guid!==p.guid)throw Error('Player identity changed before shutdown: '+p.name);
     const reply=await r.command('admin.kickPlayer',p.name,'ServerRestart');
     if(!['OK','PlayerNotFound','InvalidPlayerName'].includes(reply[0]))throw Error('Cannot disconnect '+p.name+': '+reply[0]);
    }
    pending.push({name:p.name,id,before,hasProgress:Number(p.kills)>0||Number(p.deaths)>0||Number(p.score)>0});
   }
   for(let attempt=0;attempt<(options.checks??40);attempt++){
    await pause(500);
    const present=parsePlayers(await r.command('admin.listPlayers','all')).filter(p=>p.guid);
    for(const p of pending){
     if(p.saved)continue;
     const changed=JSON.stringify(await reportOf(p.id))!==p.before;
     if(!present.some(v=>v.name===p.name)&&(changed||!p.hasProgress)){p.saved=true;result.players.push({name:p.name,id:p.id,reportObserved:changed});}
    }
    if(pending.every(p=>p.saved))break;
   }
   const missing=pending.filter(p=>!p.saved);
   if(missing.length)throw Error('Current-round progress was not confirmed for '+missing.map(p=>p.name).join(', ')+'. Services were left running; inspect Blaze logs.');
  }
  throw Error('Players continued joining during shutdown; services were left running.');
 }finally{r.close();}
}
if(typeof process!=='undefined'&&process.argv[1]&&import.meta.url===pathToFileURL(path.resolve(process.argv[1])).href){
 const root=path.resolve(path.dirname(fileURLToPath(import.meta.url)),'..');
 try{
  const result=await saveConnectedPlayers(root);
  await fs.writeFile(path.join(root,'runtime/last-player-save.json'),JSON.stringify(result,null,2));
  console.log('Player progress checked before shutdown: '+result.players.length);
 }catch(e){console.error(e.message);process.exitCode=1;}
}
