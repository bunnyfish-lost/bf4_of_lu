import path from 'node:path';
import {runModule,json} from './runtime.mjs';
import {KillSound} from './support/kill-sound.mjs';
import {prepareAudioLibrary,executeAudioAction,refreshAudioLibrary} from './audio-library.mjs';
await runModule(import.meta.url,async env=>{
 const sandbox=await prepareAudioLibrary(env.mod,env.state);
 const store={async secrets(){return {settings:await json(path.join(env.host,'settings/host-settings.json')),password:(await json(path.join(env.host,'runtime/private.json'))).rconPassword};}};
 const sound=new KillSound(sandbox,store);await sound.start();let config={};
 async function apply(c){const app=await json(path.join(env.context.launcherRoot,'config.json'));const playerName=c.playerName?.trim()||app.playerId;await sound.save({enabled:c.killEnabled!==false,playerName,volume:Number(c.killVolume??70),pack:c.killPack||'test',streakEnabled:c.streakEnabled!==false});await sound.saveDeath({enabled:c.deathEnabled===true,playerName,volume:Number(c.deathVolume??30),pack:c.deathPack||'peace1'});config=c;}
 return {configure:apply,async tick(){await refreshAudioLibrary(sound);const s=sound.snapshot();const ready=(!config.killEnabled||sound.audioReady)&&(!config.deathEnabled||sound.death.ready);await env.status(ready?'applied':'waiting','本机音效：'+s.audioState+' · 事件：'+s.eventState,{audio:s.audioState,death:s.death.audioState});},actions:Object.fromEntries(['preview-kill','preview-death','import-kill','import-death'].map(action=>[action,body=>executeAudioAction(sound,action,body)])),close:async()=>sound.close()};
});
