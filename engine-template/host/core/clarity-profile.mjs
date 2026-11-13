import fs from 'node:fs/promises';
import path from 'node:path';
import os from 'node:os';
import {execFile} from 'node:child_process';
import {promisify} from 'node:util';
import {randomUUID} from 'node:crypto';
const run=promisify(execFile);
const preset={'GstRender.TextureQuality':'2','GstRender.MeshQuality':'2','GstRender.TerrainQuality':'2','GstRender.AnisotropicFilter':'4','GstRender.WeaponDOF':'0','GstRender.MotionBlur':'0','GstRender.MotionBlurEnabled':'0'};
const keys=Object.keys(preset);
const lines=text=>text.match(/[^\n]*\n|[^\n]+$/g)||[];
const keyOf=line=>/^([^\s]+)\s+/.exec(line)?.[1];
async function atomic(file,text){await fs.mkdir(path.dirname(file),{recursive:true});const tmp=file+'.'+randomUUID()+'.tmp';try{await fs.writeFile(tmp,text,'utf8');await fs.rename(tmp,file);}finally{await fs.rm(tmp,{force:true}).catch(()=>{});}}
export class ClarityProfile {
 constructor(root,options={}){this.root=root;this.file=options.profile||path.join(os.homedir(),'Documents/Battlefield 4/settings/PROFSAVE_profile');this.stateFile=path.join(root,'settings/clarity-profile.json');this.gameRunning=options.gameRunning||async function(){const {stdout}=await run((process.env.BF4_LAUNCHER_PS||'C:/Windows/System32/WindowsPowerShell/v1.0/powershell.exe'),['-NoLogo','-NoProfile','-NonInteractive','-Command',"[Console]::Write([Diagnostics.Process]::GetProcessesByName('bf4_no_drm').Length)"],{windowsHide:true,timeout:8000});return Number(stdout.trim())>0;};}
 async state(){let text;try{text=await fs.readFile(this.stateFile,'utf8');}catch(e){if(e.code==='ENOENT')return null;throw e;}const s=JSON.parse(text);if(s.version!==1||s.file!==this.file||typeof s.enabled!=='boolean'||!s.original||keys.some(k=>s.original[k]!==null&&typeof s.original[k]!=='string'))throw Error('画面恢复记录不可用，请先检查备份');return s;}
 async snapshot(){const text=await fs.readFile(this.file,'utf8'),s=await this.state();const current=Object.fromEntries(lines(text).map(line=>[keyOf(line),line.trim().split(/\s+/).slice(1).join(' ')]));const applied=!!s?.enabled&&keys.every(k=>current[k]===preset[k]);return {enabled:!!s?.enabled,applied,canRestore:!!s?.enabled,profile:this.file,message:applied?'清晰预设已写入，下次启动 BF4 生效':s?.enabled?'部分设置与预设不同；可重新应用或恢复原设置':'保留当前画面设置；可应用清晰预设'};}
 async set(enabled){
  if(typeof enabled!=='boolean')throw Error('画面开关须为布尔值');
  if(await this.gameRunning())throw Error('请先正常退出 BF4，再更改画面预设，避免游戏覆盖配置');
  const before=await fs.readFile(this.file,'utf8');let s=await this.state();
  if(!enabled&&!s?.enabled)return this.snapshot();
  if(enabled&&!s?.enabled){const original=Object.fromEntries(keys.map(k=>[k,lines(before).find(l=>keyOf(l)===k)??null]));s={version:1,file:this.file,enabled:true,original,trailingNewline:before.endsWith('\n')};}
  const eol=before.includes('\r\n')?'\r\n':'\n',seen=new Set();
  const updated=lines(before).map(line=>{const k=keyOf(line);if(!keys.includes(k))return line;seen.add(k);if(!enabled)return s.original[k]??'';const ending=line.endsWith('\r\n')?'\r\n':line.endsWith('\n')?'\n':'';return line.match(/^\S+\s+/)[0]+preset[k]+ending;});
  let after=updated.join('');
  if(enabled)for(const k of keys)if(!seen.has(k)){if(after&&!after.endsWith('\n'))after+=eol;after+=k+' '+preset[k]+eol;}
  if(!enabled)for(const k of keys)if(!seen.has(k)&&s.original[k]!==null){if(after&&!after.endsWith('\n'))after+=eol;after+=s.original[k];}
  if(!enabled&&s.trailingNewline===false)after=after.replace(/\r?\n$/,'');
  const history=path.join(this.root,'panel/backups/clarity-'+randomUUID());await fs.mkdir(history,{recursive:true});await fs.writeFile(path.join(history,'PROFSAVE_profile.before'),before,'utf8');await fs.writeFile(path.join(history,'restore-state.json'),JSON.stringify(s,null,2),'utf8');
  if(await fs.readFile(this.file,'utf8')!==before)throw Error('画面设置已变化，请重新读取');
  if(enabled)await atomic(this.stateFile,JSON.stringify(s,null,2)+'\n');
  await atomic(this.file,after);s.enabled=enabled;await atomic(this.stateFile,JSON.stringify(s,null,2)+'\n');await fs.writeFile(path.join(history,'PROFSAVE_profile.after'),after,'utf8');
  return {...await this.snapshot(),saved:true,message:enabled?'清晰预设已写入，请启动 BF4 查看效果':'已恢复本次预设涉及的原画面设置'};
 }
}
