import fs from 'node:fs/promises';
import path from 'node:path';
import {fileURLToPath} from 'node:url';
import {randomUUID} from 'node:crypto';

const logName = /\.log(?:\.(?:\d+|old|bak))?$/i;
const taskName = /^\d{8}-\d{6}-[a-f0-9]{32}\.json$/i;
const scopes = new Set(['all','services','game','launcher']);
const equalPath = (a,b) => process.platform === 'win32' ? a.toLowerCase() === b.toLowerCase() : a === b;
const inside = (base,p) => equalPath(base,p) || (process.platform === 'win32' ? p.toLowerCase().startsWith(base.toLowerCase()+path.sep) : p.startsWith(base+path.sep));
const alive = pid => {if(!Number.isInteger(pid)||pid<=0)return false;try{process.kill(pid,0);return true;}catch(e){return e.code!=='ESRCH';}};
const readJson = async p => JSON.parse((await fs.readFile(p,'utf8')).replace(/^\uFEFF/,''));

export async function cleanLogs(root,{enabled=false,scope='all',audit=true,removeFile=fs.unlink}={}) {
 const result={enabled:enabled===true,scope,deletedFiles:0,freedBytes:0,skippedFiles:0,errors:[],state:'disabled',at:Date.now()};
 if(enabled!==true)return result;
 if(!scopes.has(scope))throw Error('Unknown log cleanup scope');
 const base=await fs.realpath(root);
 async function safe(p) {
  const resolved=path.resolve(p);if(!inside(base,resolved))return false;
  let current=base;
  for(const part of path.relative(base,resolved).split(path.sep).filter(Boolean)) {
   current=path.join(current,part);
   try{const st=await fs.lstat(current);if(st.isSymbolicLink())return false;}catch(e){if(e.code==='ENOENT')return false;throw e;}
  }
  return equalPath(await fs.realpath(resolved),resolved);
 }
 async function stopped() {
  if(scope==='launcher')return true;
  const file=path.join(base,'host/runtime/processes.json');
  if(!await safe(file)){result.state='blocked';result.reason='服务状态不可读取，日志已保留';return false;}
  let state;try{state=await readJson(file);}catch{result.state='blocked';result.reason='服务状态不可读取，日志已保留';return false;}
  if(!Array.isArray(state.processes)){result.state='blocked';result.reason='服务状态不可读取，日志已保留';return false;}
  const roles=scope==='game'?['game','stats']:['game','stats','blaze'];
  if(state.processes.some(r=>roles.includes(r.role)&&alive(Number(r.pid)))){result.state='blocked';result.reason='服务仍在运行，日志已保留';return false;}
  return true;
 }
 async function erase(file,kind='log') {
  try {
   const st=await fs.lstat(file);if(!st.isFile()||st.isSymbolicLink())return;
   if(!await safe(file)){result.skippedFiles++;return;}
   if(kind==='mod') {
    const processFile=path.join(path.dirname(file),'process.json');
    if(!await safe(processFile)){result.skippedFiles++;return;}
    let p;try{p=await readJson(processFile);}catch{result.skippedFiles++;return;}
    if(alive(Number(p.pid))||alive(Number(p.wrapperPid))){result.skippedFiles++;return;}
   }
   if(!await safe(file)){result.skippedFiles++;return;}
   await removeFile(file);result.deletedFiles++;result.freedBytes+=st.size;
  }catch(e){if(e.code==='ENOENT')return;result.skippedFiles++;result.errors.push({file:path.relative(base,file),code:e.code||'ERROR'});}
 }
 async function directory(relative,{recursive=false,tasks=false,mod=false,text=false}={}) {
  const dir=path.join(base,relative);
  try {
   if(!await safe(dir))return;
   for(const item of await fs.readdir(dir,{withFileTypes:true})) {
    const file=path.join(dir,item.name);
    if(item.isSymbolicLink())continue;
    if(item.isDirectory()){if(recursive)await directory(path.relative(base,file),{recursive,tasks,mod,text});continue;}
    if(logName.test(item.name)||(tasks&&taskName.test(item.name))||(text&&/\.txt$/i.test(item.name)))await erase(file,mod?'mod':'log');
   }
  }catch(e){if(e.code!=='ENOENT')result.errors.push({file:relative,code:e.code||'ERROR'});}
 }
 if(!await stopped())return result;
 result.state='cleaned';
 if(scope!=='launcher') {
  await directory('host/runtime/GameServer');
  await directory('host/runtime/GameServer/Instance/Logs',{recursive:true});
  await directory('host/runtime/GameServer/Instance/TelemetryTransactionLog',{recursive:true});
  if(scope==='game') {
   for(const file of ['local-stats.stdout.log','local-stats.stderr.log'])await erase(path.join(base,'host/runtime',file));
  }else {
   await directory('host/runtime');
   await directory('host/runtime/Blaze');
   await directory('host/runtime/Blaze/logs',{recursive:true,text:true});
  }
 }
 if(scope==='all'||scope==='launcher') {
  await directory('logs',{recursive:true,tasks:true});
  await erase(path.join(base,'launcher-error.log'));
 }
 if(scope==='all')await directory('data/mod-runtime',{recursive:true,mod:true});
 if(audit) {
  const dir=path.join(base,'data');
  try {
   try{await fs.mkdir(dir);}catch(e){if(e.code!=='EEXIST')throw e;}
   if(await safe(dir)) {
    const file=path.join(dir,'log-cleanup.latest.json');
    try{const st=await fs.lstat(file);if(st.isSymbolicLink())throw Error('Linked audit file');}catch(e){if(e.code!=='ENOENT')throw e;}
    const tmp=file+'.'+randomUUID()+'.tmp';
    await fs.writeFile(tmp,JSON.stringify(result,null,2),{flag:'wx'});
    if(await safe(dir))await fs.rename(tmp,file);else await fs.unlink(tmp);
   }
  }catch(e){result.errors.push({file:'data/log-cleanup.latest.json',code:e.code||'ERROR'});}
 }
 return result;
}

if(process.argv[1]&&equalPath(path.resolve(process.argv[1]),fileURLToPath(import.meta.url))) {
 const root=path.dirname(path.dirname(fileURLToPath(import.meta.url)));
 try {
  const cfg=await readJson(path.join(root,'config.json'));
  const index=process.argv.indexOf('--scope');const scope=index>=0?process.argv[index+1]:'all';
  console.log(JSON.stringify(await cleanLogs(root,{enabled:cfg.autoCleanLogs===true,scope})));
 }catch(e){console.log(JSON.stringify({enabled:true,state:'error',deletedFiles:0,freedBytes:0,skippedFiles:0,errors:[{code:e.code||'ERROR'}],reason:'日志清理未完成，文件已保留'}));}
}
