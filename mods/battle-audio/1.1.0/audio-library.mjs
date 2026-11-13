import fs from 'node:fs/promises';
import path from 'node:path';
import crypto from 'node:crypto';
import {atomic} from './runtime.mjs';
import {KillSound} from './support/kill-sound.mjs';

async function copyMissing(source,target){
 if(await fs.access(target).then(()=>true,()=>false))return;
 const temp=target+'.'+crypto.randomUUID()+'.tmp';
 try{await fs.copyFile(source,temp);try{await fs.rename(temp,target);}catch(error){if(!await fs.access(target).then(()=>true,()=>false))throw error;}}
 finally{await fs.unlink(temp).catch(()=>{});}
}
export async function prepareAudioLibrary(mod,state){
 const sandbox=path.join(state,'audio-runtime');
 for(const dir of ['core/native','templates','settings','sound-packs'])await fs.mkdir(path.join(sandbox,dir),{recursive:true});
 for(const sub of ['native','sounds'])for(const name of await fs.readdir(path.join(mod,'resources',sub))){
  const source=path.join(mod,'resources',sub,name);if(!(await fs.stat(source)).isFile())continue;
  await copyMissing(source,path.join(sandbox,sub==='native'?'core/native':'sound-packs',name));
 }
 const packs=JSON.parse((await fs.readFile(path.join(mod,'resources/sound-packs.json'),'utf8')).replace(/^\uFEFF/,''));
 await atomic(path.join(sandbox,'templates/sound-packs.json'),packs);return sandbox;
}
export async function openAudioTools(mod,state,options={}){
 const sandbox=await prepareAudioLibrary(mod,state);
 const sound=new KillSound(sandbox,{async secrets(){throw Error('本地试听不读取服务端连接信息');}},options);
 await sound.loadLibrary();return {sound,async execute(action,body={}){return executeAudioAction(sound,action,body);},async close(){await sound.close();}};
}
export async function executeAudioAction(sound,action,body={}){
 if(action==='preview-kill'){await sound.preview({pack:body.killPack||'test',slot:body.slot||'kill',volume:Number(body.volume??70)});return {message:'已播放击杀音效试听'};}
 if(action==='preview-death'){await sound.death.preview({pack:body.deathPack||'peace1',volume:Number(body.volume??30)});return {message:'已播放死亡音效试听'};}
 if(action==='import-kill'||action==='import-death'){
  const file=String(body.file||'');if(!file)throw Error('请选择音频文件');const stat=await fs.stat(file);if(!stat.isFile()||stat.size>4*1024*1024)throw Error('音频文件不能超过4MiB');
  const data=(await fs.readFile(file)).toString('base64'),extension=path.extname(file).toLowerCase();
  if(action==='import-death')await sound.death.importAudio({data,extension});
  else await sound.importAudio({data,extension,pack:body.killPack==='test'?'custom':body.killPack||'custom',slot:body.slot||'kill'});
  return {message:'音频已导入，可直接试听；在参数设置中选择对应声音包后保存。'};
 }
 throw Error('此音效操作不受支持');
}
export async function refreshAudioLibrary(sound){
 const before=JSON.stringify([sound.packs,sound.bundled,sound.death.custom]);await sound.loadLibrary();
 if(before===JSON.stringify([sound.packs,sound.bundled,sound.death.custom]))return false;
 if(sound.config.enabled)await sound.configurePlayer();
 if(sound.death.config.enabled)await sound.death.configure();return true;
}
