import fs from 'node:fs/promises';import path from 'node:path';import {fileURLToPath} from 'node:url';
import {ConfigStore} from '../host/panel/config-store.mjs';import {Manager} from '../host/panel/manager.mjs';import {BotFill,validateFill} from '../host/core/bot-fill.mjs';import {BotDamage} from '../host/core/bot-damage.mjs';import {profileSummary} from '../host/core/player-profiles.mjs';import {levelEditorSnapshot,updateProfileLevel} from '../host/core/profile-level.mjs';
const root=path.dirname(path.dirname(fileURLToPath(import.meta.url))),host=path.join(root,'host');
const json=async p=>JSON.parse((await fs.readFile(p,'utf8')).replace(/^\uFEFF/,''));
export async function management(action,body={},options={}){
 const appRoot=options.root||root,hostRoot=path.join(appRoot,'host');
 const initialized=await fs.access(path.join(appRoot,'data/environment.json')).then(()=>true,()=>false);if(!initialized){if(action==='state')return {initialized:false,online:false,players:[],maps:[]};throw Error('请先完成初始化。');}
 const store=options.store||new ConfigStore(path.join(hostRoot,'settings'),path.join(hostRoot,'panel')),manager=options.manager||new Manager(store),damage=options.damage||new BotDamage(hostRoot);
 try{
  if(action==='state'){
   const state=await manager.state();
   const cfg=validateFill(await json(path.join(hostRoot,'panel/bot-fill.json')).catch(e=>{if(e.code==='ENOENT')return {enabled:false,target:64,reserve:1,mode:'auto',team1:0,team2:0};throw e;}));
   let fill=await json(path.join(appRoot,'data/bot-fill-state.json')).catch(()=>null),same=false;
   try{same=JSON.stringify(validateFill(fill))===JSON.stringify(cfg);}catch{}
   if(!fill||Date.now()-fill.at>15000||fill.at>Date.now()+5000||!same)fill={...cfg,state:cfg.enabled?'waiting':'disabled'};
   return {...state,initialized:true,fill};
  }
  if(action==='player')return await manager.playerAction(body);
  if(action==='kill')return await manager.killPlayers(body);
  if(action==='map')return await manager.mapAction(body);
  if(action==='announce')return await manager.announce(body.text);
  if(action==='profiles')return await profileSummary(hostRoot);
  if(action==='level-editor')return await levelEditorSnapshot(hostRoot);
  if(action==='profile-level')return await updateProfileLevel(hostRoot,body);
  if(action==='loadout')return await store.publicLoadout();
  if(action==='save-loadout')return await store.saveLoadout(body);
  if(action==='damage')return await damage.snapshot();
  if(action==='save-damage')return await damage.save(body);
  if(action==='save-fill') {const fill=new BotFill(manager);const answer=await fill.save(validateFill(body));return answer;}
  throw Error('未知管理操作。');
 }finally{if(!options.manager)manager.close();}
}
if(process.argv[1]&&path.resolve(process.argv[1])===fileURLToPath(import.meta.url)){
 try{const req=await json(process.argv[2]);const value=await management(req.action,req.body||{});console.log('@RESULT '+JSON.stringify(value));}catch(e){console.error(e.message);process.exitCode=1;}
}
