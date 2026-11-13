import fs from 'node:fs/promises';
import path from 'node:path';
import {createHash,randomUUID} from 'node:crypto';
const defaults={version:1,skipDeath:false,teamSpawn:false,autoSpot:false,skipKey:'MouseRight'};
const keyMap={MouseLeft:1,MouseRight:2,MouseMiddle:4,MouseBack:5,MouseForward:6,Space:32,Tab:9,Enter:13,Backspace:8,Insert:45,Delete:46,Home:36,End:35,PageUp:33,PageDown:34,ArrowLeft:37,ArrowUp:38,ArrowRight:39,ArrowDown:40};
export function virtualKey(key){
 if(Object.hasOwn(keyMap,key))return keyMap[key];
 if(typeof key==='string'&&/^Key[A-Z]$/.test(key))return key.charCodeAt(3);
 if(typeof key==='string'&&/^Digit[0-9]$/.test(key))return key.charCodeAt(5);
 if(typeof key==='string'&&/^F(?:[1-9]|1[0-2])$/.test(key))return 111+Number(key.slice(1));
 throw Error('此按键暂不支持，请选择字母、数字、F1～F12、方向键或鼠标键');
}
export function normalizeAssist(value){
 if(!value||typeof value!=='object'||typeof value.skipDeath!=='boolean'||typeof value.teamSpawn!=='boolean')throw Error('辅助功能开关须为布尔值');
 if(value.autoSpot!==undefined&&typeof value.autoSpot!=='boolean')throw Error('自动标记开关须为布尔值');
 virtualKey(value.skipKey);return {version:1,skipDeath:value.skipDeath,teamSpawn:value.teamSpawn,autoSpot:value.autoSpot??false,skipKey:value.skipKey};
}
const optional=async file=>{try{return await fs.readFile(file,'utf8');}catch(e){if(e.code==='ENOENT')return null;throw e;}};
const revision=text=>createHash('sha256').update(text??'<missing>').digest('hex');
export class AssistSettings {
 constructor(root,options={}){this.root=root;this.clock=options.clock||Date.now;this.runtime=options.runtime||{pid:null,session:null};this.pending=Promise.resolve();}
 async read(){const file=path.join(this.root,'settings/local-assist.json'),text=await optional(file);const value=text===null?defaults:JSON.parse(text.replace(/^\uFEFF/,''));if(value.version!==1)throw Error('无法识别辅助配置版本');return {file,text,value:normalizeAssist(value),revision:revision(text)};}
 async snapshot(){
  const saved=await this.read();let live=null;try{const text=await optional(path.join(this.root,'runtime/local-assist-state.json'));if(text)live=JSON.parse(text);}catch(e){if(!(e instanceof SyntaxError))throw e;}
  const valid=!!this.runtime.pid&&!!this.runtime.session&&live?.ready===true&&live.pid===this.runtime.pid&&live.session===this.runtime.session&&Number.isFinite(live.at)&&Math.abs(this.clock()-live.at)<4000;
  const sidesMatch=valid&&['teamClientApplied','teamServerApplied'].every(k=>live[k]===undefined||live[k]===saved.value.teamSpawn);
  const applied=valid&&sidesMatch&&live.revision===saved.revision&&['skipDeath','teamSpawn','skipKey'].every(k=>live[k]===saved.value[k])&&(live.autoSpot===true)===saved.value.autoSpot;
  const enabled=saved.value.skipDeath||saved.value.teamSpawn||saved.value.autoSpot;
  return {...saved.value,revision:saved.revision,virtualKey:virtualKey(saved.value.skipKey),applied,state:applied?'applied':enabled?'unconfirmed':'disabled',message:applied?'辅助模块已载入当前配置；游戏效果需实测':valid&&enabled&&live.teamMessage?live.teamMessage:enabled?'设置已保存，尚未确认游戏内生效':'辅助功能均已关闭',runtime:valid?{pid:live.pid,skipDeath:live.skipDeath,teamSpawn:live.teamSpawn,autoSpot:live.autoSpot===true,skipKey:live.skipKey,...(typeof live.teamClientApplied==='boolean'?{teamClientApplied:live.teamClientApplied,teamServerApplied:live.teamServerApplied,teamMessage:live.teamMessage||''}:{})}:null};
 }
 save(input){const operation=this.pending.then(()=>this.saveOnce(input));this.pending=operation.catch(()=>{});return operation;}
 async saveOnce(input){
  const value=normalizeAssist(input),before=await this.read();if(input.revision!==before.revision)throw Error('辅助设置已变化，请重新读取后再保存');
  const dir=path.join(this.root,'panel/backups');await fs.mkdir(dir,{recursive:true});const backup=path.join(dir,'local-assist-'+randomUUID()+'.json');await fs.writeFile(backup,JSON.stringify({at:new Date(this.clock()).toISOString(),source:before.file,before:before.text,after:value},null,2),'utf8');
  const check=await optional(before.file);if(check!==before.text)throw Error('辅助设置已被其他操作更新，请重新读取');
  await fs.mkdir(path.dirname(before.file),{recursive:true});const tmp=before.file+'.'+randomUUID()+'.tmp';try{await fs.writeFile(tmp,JSON.stringify(value,null,2)+'\n','utf8');await fs.rename(tmp,before.file);}finally{await fs.rm(tmp,{force:true}).catch(()=>{});}
  return {...await this.snapshot(),saved:true};
 }
}
