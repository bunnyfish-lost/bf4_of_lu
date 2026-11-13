import {atomic as stableWrite} from '../runtime.mjs';
import fs from 'node:fs/promises';
import path from 'node:path';
import {spawn} from 'node:child_process';
import {randomUUID} from 'node:crypto';
import {KillEvents} from './kill-events.mjs';
import {DeathSound} from './death-sound.mjs';

export const soundDefaults={enabled:false,playerName:'',volume:70,pack:'test',streakEnabled:true};
export const packNames={test:'简洁提示音',bf1:'战地 1',bfv:'战地 5',cf:'穿越火线',custom:'自定义'};
export const soundSlots=['kill','headKill','hit','headHit',...Array.from({length:8},(_,i)=>'streak'+(i+1))];
export function packSlots(value){if(typeof value==='string')return {kill:value};return Object.fromEntries(soundSlots.filter(k=>typeof value?.[k]==='string').map(k=>[k,value[k]]));}
export function selectCue(pack,slots,event,streakEnabled=true){
  if(event.type==='hit')return event.headshot&&slots.headHit?'headHit':slots.hit?'hit':null;
  if(event.headshot&&slots.headKill)return 'headKill';
  if(pack==='cf'&&streakEnabled&&Number.isInteger(event.streak)&&event.streak>0){
    return slots['streak'+event.streak]?'streak'+event.streak:'kill';
  }
  return 'kill';
}
export function validateSound(value){
  if(!value||typeof value!=='object')throw Error('音效设置无效');
  const v=Object.fromEntries(Object.keys(soundDefaults).map(k=>[k,k==='streakEnabled'&&!Object.hasOwn(value,k)?true:value[k]]));
  if(typeof v.streakEnabled!=='boolean')throw Error('连杀音效开关无效');
  if(typeof v.enabled!=='boolean'||!Number.isInteger(v.volume)||v.volume<0||v.volume>100||!Object.hasOwn(packNames,v.pack))throw Error('请选择音效，音量需为 0～100');
  if(typeof v.playerName!=='string'||v.playerName.length>64||/[\x00-\x1f]/.test(v.playerName)||v.enabled&&!v.playerName.trim())throw Error('请填写 BF4Launcher 玩家 ID');
  v.playerName=v.playerName.trim();return v;
}
async function json(file,fallback){try{return JSON.parse(await fs.readFile(file,'utf8'));}catch(e){if(e.code==='ENOENT')return fallback;throw e;}}
async function put(file,value){await stableWrite(file,value);}
export function validateAudio(data,extension){
  if(!Buffer.isBuffer(data)||data.length<44||data.length>4*1024*1024)throw Error('音效文件需小于 4 MB');
  if(extension==='.wav'&&data.toString('ascii',0,4)==='RIFF'&&data.toString('ascii',8,12)==='WAVE')return;
  if(extension==='.mp3'&&(data.toString('ascii',0,3)==='ID3'||data[0]===255&&(data[1]&224)===224))return;
  throw Error('请选择有效的 WAV 或 MP3 音频');
}
export class KillSound {
  constructor(root,store,options={}){this.root=root;this.store=store;this.spawn=options.spawn||spawn;this.config={...soundDefaults};this.packs={};this.bundled={};this.player=null;this.audioState='未启用';this.audioReady=false;this.lastKill=null;this.closed=false;this.death=new DeathSound(root,{...options,validateAudio});this.events=new KillEvents(()=>store.secrets(),event=>this.deliver(event),options.connect);}
  async start(){this.config=validateSound({...soundDefaults,...await json(path.join(this.root,'settings/kill-sound.json'),{})});await this.loadLibrary();await this.death.start(this.config.playerName);await this.apply();}
  async loadLibrary(){this.packs=await json(path.join(this.root,'settings/sound-packs.json'),{});this.bundled=await json(path.join(this.root,'templates/sound-packs.json'),{});this.death.custom=(await json(path.join(this.root,'settings/death-sound-pack.json'),{})).file||null;}
  snapshot(){return {death:this.death.snapshot(),config:this.config,packs:Object.entries(packNames).map(([id,label])=>({id,label,available:!!this.slots(id).kill,slots:Object.keys(this.slots(id))})),audioState:this.audioState,eventState:this.events.state,lastKill:this.lastKill,received:this.events.events};}
  slots(pack){return pack==='test'?{kill:'kill-test.wav'}:{...packSlots(this.bundled[pack]),...packSlots(this.packs[pack])};}
  async file(pack,slot='kill'){if(!Object.hasOwn(packNames,pack)||!soundSlots.includes(slot))throw Error('未知音效类型');const name=this.slots(pack)[slot];if(!name)throw Error('请先上传该类型的音频');const file=path.join(this.root,pack==='test'?'core/native':'sound-packs',path.basename(name));await fs.access(file);return file;}
  async save(body){const next=validateSound(body);await this.file(next.pack);const old=this.config;this.config=next;try{await this.apply();await put(path.join(this.root,'settings/kill-sound.json'),next);}catch(e){this.config=old;await this.apply().catch(()=>{});throw e;}return this.snapshot();}
  async importAudio(body){if(!soundSlots.includes(body.slot??'kill')||String(body.slot).startsWith('streak')&&body.pack!=='cf')throw Error('音效类型无效');if(!['bf1','bfv','cf','custom'].includes(body.pack)||typeof body.data!=='string'||!/^[A-Za-z0-9+/]*={0,2}$/.test(body.data))throw Error('音效包数据无效');const extension=String(body.extension).toLowerCase(),bytes=Buffer.from(body.data,'base64');validateAudio(bytes,extension);const name=body.pack+'-'+randomUUID()+extension;await fs.mkdir(path.join(this.root,'sound-packs'),{recursive:true});await fs.writeFile(path.join(this.root,'sound-packs',name),bytes);const slot=body.slot??'kill';this.packs[body.pack]={...packSlots(this.packs[body.pack]),[slot]:name};await put(path.join(this.root,'settings/sound-packs.json'),this.packs);if(this.config.enabled&&this.config.pack===body.pack)await this.configurePlayer();return this.snapshot();}
  async ensurePlayer(){
    if(this.player&&!this.player.killed)return;
    const exe=path.join(this.root,'core/native/BF4KillSound.exe');await fs.access(exe);
    const child=this.spawn(exe,['--worker'],{windowsHide:true,stdio:['pipe','pipe','pipe']});this.player=child;this.audioReady=false;this.audioState='播放器启动中';
    child.stdin.on('error',()=>{});let buffer='';
    child.stdout.on('data',chunk=>{buffer+=chunk.toString('utf8');if(buffer.length>16384)buffer='';let i;while((i=buffer.indexOf('\n'))>=0){const line=buffer.slice(0,i);buffer=buffer.slice(i+1);try{const msg=JSON.parse(line);if(this.player===child){if(msg.type==='ready'){this.audioReady=true;this.audioState='已就绪';}if(msg.type==='error'){this.audioReady=false;this.audioState=String(msg.message);}}}catch{}}});
    child.stderr.on('data',()=>{});
    child.on('error',e=>{if(this.player===child){this.audioState='播放器启动失败：'+e.message;this.audioReady=false;this.player=null;}});
    child.on('exit',()=>{if(this.player===child){this.player=null;this.audioReady=false;this.audioState='播放器已退出，重新保存可启动';}});
  }
  writePlayer(value){if(this.player?.stdin.writable)this.player.stdin.write(JSON.stringify(value)+'\n');}
  async configurePlayer(){await this.ensurePlayer();this.audioReady=false;this.audioState='正在加载音效';const files={};for(const slot of Object.keys(this.slots(this.config.pack)))files[slot]=await this.file(this.config.pack,slot);this.writePlayer({type:'configure',files,volume:this.config.volume});}
  stopPlayer(){const child=this.player;this.player=null;this.audioReady=false;if(child){child.stdin.end();const timer=setTimeout(()=>{if(child.exitCode===null)child.kill();},1500);timer.unref();}this.audioState='未启用';}
  async preview(body){if(!Number.isInteger(body.volume)||body.volume<0||body.volume>100)throw Error('音量范围为 0～100');const file=await this.file(body.pack,body.slot??'kill');const exe=path.join(this.root,'core/native/BF4KillSound.exe');await fs.access(exe);if(this.previewChild)throw Error('试听正在进行');const child=this.spawn(exe,['--preview',file,String(body.volume)],{windowsHide:true,stdio:['ignore','pipe','pipe']});this.previewChild=child;let output='';child.stdout.on('data',b=>{if(output.length<16000)output+=b;});child.stderr.on('data',()=>{});try{await new Promise((resolve,reject)=>{const timer=setTimeout(()=>{child.kill();reject(Error('试听超时'));},10000);child.once('error',e=>{clearTimeout(timer);reject(e);});child.once('exit',code=>{clearTimeout(timer);if(code===0)resolve();else reject(Error('试听失败：'+output.slice(0,300)));});});}finally{this.previewChild=null;}return {message:'试听已完成'};}
  deliver(event){if(event.type==='death'){this.death.deliver(event);return;}if(event.type!=='kill')return;const cue=selectCue(this.config.pack,this.slots(this.config.pack),event,this.config.streakEnabled);this.lastKill={at:event.at,player:event.player,headshot:event.headshot,streak:event.streak,cue};const message={type:'kill',id:event.id,at:event.at,headshot:event.headshot,streak:event.streak,cue};if(this.config.enabled&&event.player===this.config.playerName&&this.audioReady)this.writePlayer(message);}
  async apply(){if(this.closed)return;if(this.config.enabled)await this.configurePlayer();else this.stopPlayer();this.syncEvents();}
  syncEvents(){if(this.config.enabled||this.death.config.enabled)this.events.start();else this.events.stop();}
  async saveDeath(body){const result=await this.death.save(body);this.syncEvents();return result;}
  async close(){this.death.close();this.closed=true;this.events.stop();this.stopPlayer();this.previewChild?.kill();}
}
