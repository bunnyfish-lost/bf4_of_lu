import fs from 'node:fs/promises';
import path from 'node:path';
import {spawn} from 'node:child_process';
import {randomUUID} from 'node:crypto';

export const deathDefaults={enabled:false,playerName:'',volume:30,pack:'peace1'};
export const deathPacks={peace1:'和平精英 1',peace2:'和平精英 2',custom:'自定义'};
export function validateDeath(value){
  if(!value||typeof value!=='object')throw Error('死亡音效设置无效');
  const c=Object.fromEntries(Object.keys(deathDefaults).map(k=>[k,value[k]]));
  if(typeof c.enabled!=='boolean'||!Object.hasOwn(deathPacks,c.pack)||!Number.isInteger(c.volume)||c.volume<0||c.volume>100)throw Error('请选择死亡音效，音量需为 0～100');
  if(typeof c.playerName!=='string'||c.playerName.length>64||/[\x00-\x1f]/.test(c.playerName)||c.enabled&&!c.playerName.trim())throw Error('请填写死亡音效对应的 BF4Launcher 玩家 ID');
  c.playerName=c.playerName.trim();return c;
}
async function json(file,fallback){try{return JSON.parse(await fs.readFile(file,'utf8'));}catch(e){if(e.code==='ENOENT')return fallback;throw e;}}
async function put(file,value){await fs.mkdir(path.dirname(file),{recursive:true});const tmp=file+'.'+randomUUID()+'.tmp';try{await fs.writeFile(tmp,JSON.stringify(value,null,2));await fs.rename(tmp,file);}finally{await fs.unlink(tmp).catch(()=>{});}}

export class DeathSound {
  constructor(root,options={}){this.root=root;this.spawn=options.spawn||spawn;this.validateAudio=options.validateAudio;this.config={...deathDefaults};this.custom=null;this.child=null;this.ready=false;this.state='已关闭';this.lastDeath=null;}
  async start(playerName=''){
    this.config=validateDeath({...deathDefaults,playerName,...await json(path.join(this.root,'settings/death-sound.json'),{})});
    this.custom=(await json(path.join(this.root,'settings/death-sound-pack.json'),{})).file||null;
    if(this.config.enabled)await this.configure();
  }
  snapshot(){return {config:this.config,packs:Object.entries(deathPacks).map(([id,label])=>({id,label,available:id!=='custom'||!!this.custom})),audioState:this.state,lastDeath:this.lastDeath};}
  async file(pack){
    if(!Object.hasOwn(deathPacks,pack))throw Error('未知死亡音效');
    if(pack==='custom'&&!this.custom)throw Error('请先上传自定义死亡音效');
    const file=pack==='custom'?path.join(this.root,'sound-packs',path.basename(this.custom)):path.join(this.root,'core/native',pack==='peace1'?'death-peace-1.wav':'death-peace-2.wav');
    await fs.access(file);return file;
  }
  async save(body){
    const next=validateDeath(body);if(next.enabled)await this.file(next.pack);
    const old=this.config;this.config=next;
    try{if(next.enabled)await this.configure();else this.stop();await put(path.join(this.root,'settings/death-sound.json'),next);}
    catch(e){this.config=old;if(old.enabled)await this.configure().catch(()=>{});else this.stop();throw e;}
    return this.snapshot();
  }
  async importAudio(body){
    if(typeof body.data!=='string'||!/^[A-Za-z0-9+/]*={0,2}$/.test(body.data))throw Error('音频数据无效');
    const bytes=Buffer.from(body.data,'base64'),extension=String(body.extension).toLowerCase();this.validateAudio(bytes,extension);
    const name='death-'+randomUUID()+extension;await fs.mkdir(path.join(this.root,'sound-packs'),{recursive:true});await fs.writeFile(path.join(this.root,'sound-packs',name),bytes);
    await put(path.join(this.root,'settings/death-sound-pack.json'),{file:name});this.custom=name;
    if(this.config.enabled&&this.config.pack==='custom')await this.configure();return this.snapshot();
  }
  async configure(){
    const file=await this.file(this.config.pack);
    if(!this.child||this.child.killed){
      const exe=path.join(this.root,'core/native/BF4KillSound.exe');await fs.access(exe);
      const child=this.spawn(exe,['--worker'],{windowsHide:true,stdio:['pipe','pipe','pipe']});this.child=child;let buffer='';
      child.stdin.on('error',()=>{});child.stderr.on('data',()=>{});
      child.stdout.on('data',chunk=>{buffer+=chunk.toString('utf8');if(buffer.length>16384)buffer='';let i;while((i=buffer.indexOf('\n'))>=0){const line=buffer.slice(0,i);buffer=buffer.slice(i+1);try{const msg=JSON.parse(line);if(this.child===child){if(msg.type==='ready'){this.ready=true;this.state='已就绪';}else if(msg.type==='error'){this.ready=false;this.state=String(msg.message);}}}catch{}}});
      child.on('error',e=>{if(this.child===child){this.child=null;this.ready=false;this.state='播放器启动失败：'+e.message;}});
      child.on('exit',()=>{if(this.child===child){this.child=null;this.ready=false;this.state='播放器已退出，重新保存可启动';}});
    }
    this.ready=false;this.state='正在加载死亡音效';this.child.stdin.write(JSON.stringify({type:'configure',file,volume:this.config.volume})+'\n');
  }
  deliver(event){
    if(event.type!=='death'||event.player!==this.config.playerName)return;
    this.lastDeath={player:event.player,at:event.at};
    if(this.config.enabled&&this.ready&&this.child?.stdin.writable)this.child.stdin.write(JSON.stringify({type:'kill',id:event.id,at:event.at,cue:'kill'})+'\n');
  }
  async preview(body){
    if(!Number.isInteger(body.volume)||body.volume<0||body.volume>100)throw Error('音量范围为 0～100');
    const file=await this.file(body.pack);if(this.previewChild)throw Error('试听正在进行');
    const child=this.spawn(path.join(this.root,'core/native/BF4KillSound.exe'),['--preview',file,String(body.volume)],{windowsHide:true,stdio:['ignore','pipe','pipe']});this.previewChild=child;let output='';
    child.stdout.on('data',b=>{if(output.length<16000)output+=b;});child.stderr.on('data',()=>{});
    try{await new Promise((resolve,reject)=>{const timer=setTimeout(()=>{child.kill();reject(Error('试听超时'));},10000);child.once('error',e=>{clearTimeout(timer);reject(e);});child.once('exit',code=>{clearTimeout(timer);if(code===0)resolve();else reject(Error('试听失败：'+output.slice(0,300)));});});}finally{this.previewChild=null;}
    return {message:'试听已完成'};
  }
  stop(){const child=this.child;this.child=null;this.ready=false;this.state='已关闭';if(child){child.stdin.end();const timer=setTimeout(()=>{if(child.exitCode===null)child.kill();},1500);timer.unref();}}
  close(){this.stop();this.previewChild?.kill();}
}
