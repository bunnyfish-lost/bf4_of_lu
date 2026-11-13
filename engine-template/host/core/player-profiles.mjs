import fs from 'node:fs/promises';
import path from 'node:path';
import {isIPv4} from 'node:net';
import {createHash,randomUUID} from 'node:crypto';
import {fileURLToPath,pathToFileURL} from 'node:url';

const readJson=async p=>JSON.parse((await fs.readFile(p,'utf8')).replace(/^\uFEFF/,''));
const exists=async p=>{try{await fs.access(p);return true;}catch(e){if(e.code==='ENOENT')return false;throw e;}};
const digest=b=>createHash('sha256').update(b).digest('hex');
const line=(s,k)=>s.match(new RegExp('^'+k+'[ \\t]*=[ \\t]*([^\\r\\n]*)','mi'))?.[1].trim();
function replaceLine(s,k,v){const re=new RegExp('^'+k+'[ \\t]*=[^\\r\\n]*','gmi');const matches=s.match(re)||[];if(matches.length>1)throw Error('Duplicate '+k);return matches.length?s.replace(re,k+'='+v):s+'\r\n'+k+'='+v+'\r\n';}
export function profileAddress(address){if(!isIPv4(address)||address.split('.').some(v=>String(Number(v))!==v))throw Error('Invalid profile IPv4');return address;}
function inside(root,p){const rel=path.relative(path.resolve(root),path.resolve(p));if(!rel||rel==='..'||rel.startsWith('..'+path.sep)||path.isAbsolute(rel))throw Error('Profile path outside installation');return path.resolve(p);}
async function noLinks(root,p){
 inside(root,p);let cur=path.resolve(root);
 for(const part of ['',...path.relative(root,p).split(path.sep).filter(Boolean)]){if(part)cur=path.join(cur,part);try{if((await fs.lstat(cur)).isSymbolicLink())throw Error('Profile path contains a link');}catch(e){if(e.code!=='ENOENT')throw e;}}
}
async function atomic(p,value){await fs.mkdir(path.dirname(p),{recursive:true});const temp=p+'.'+randomUUID()+'.tmp';try{await fs.writeFile(temp,value);await fs.rename(temp,p);}finally{await fs.unlink(temp).catch(e=>{if(e.code!=='ENOENT')throw e;});}}
export async function dataFiles(root){
 const result=[];
 async function visit(dir,relative=''){for(const ent of await fs.readdir(dir,{withFileTypes:true}).catch(e=>{if(e.code==='ENOENT')return [];throw e;})){
  if(ent.name==='writer.lock')continue;
  const rel=path.join(relative,ent.name),p=path.join(dir,ent.name);
  if(ent.isSymbolicLink())throw Error('Player data contains a link');
  if(ent.isDirectory())await visit(p,rel);
  else if(ent.isFile()){const b=await fs.readFile(p);result.push({file:rel.replaceAll('\\','/'),size:b.length,sha256:digest(b)});}
 }}
 await visit(root);return result.sort((a,b)=>a.file.localeCompare(b.file));
}
async function copyVerified(source,dest,files){
 await fs.mkdir(dest,{recursive:true});
 for(const f of files){const p=inside(dest,path.join(dest,f.file));await fs.mkdir(path.dirname(p),{recursive:true});const b=await fs.readFile(inside(source,path.join(source,f.file)));if(digest(b)!==f.sha256)throw Error('Player data changed during copy');await fs.writeFile(p,b,{flag:'wx'});}
 const copied=await dataFiles(dest);if(JSON.stringify(copied)!==JSON.stringify(files))throw Error('Player data copy verification failed');
}
export async function currentStorage(root,seen=new Set()){
 const key=path.resolve(root).toLowerCase();if(seen.has(key))throw Error('Shared Blaze storage cycle');seen.add(key);
 const settings=await readJson(path.join(root,'settings/host-settings.json'));
 if(settings.sharedBlaze){if(!settings.sharedBlazeRoot)throw Error('Shared Blaze root missing');return currentStorage(settings.sharedBlazeRoot,seen);}
 const config=await fs.readFile(path.join(settings.serverDirectory,'config.txt'),'utf8');
 const data=path.resolve(settings.serverDirectory,line(config,'DataRoot')||'data');
 inside(root,data);await noLinks(root,data);
 const match=path.relative(path.join(root,'saves'),data).replaceAll('\\','/').match(/^(\d+\.\d+\.\d+\.\d+)\/data$/);
 return {root,settings,config,data,address:match?profileAddress(match[1]):profileAddress(settings.legacyProfileAddress||line(config,'ServerIP')||settings.advertiseAddress)};
}
async function assertStopped(root){
 const state=await readJson(path.join(root,'runtime/processes.json'));
 for(const record of state.processes||[])if(record.role==='blaze'){
  try{process.kill(record.pid,0);}catch(e){if(e.code==='ESRCH')continue;throw Error('Cannot verify Blaze is stopped');}
  throw Error('Stop Blaze before switching or backing up player data');
 }
}
async function openProfile(root,address){
 profileAddress(address);const dir=inside(root,path.join(root,'saves',address));await noLinks(root,dir);
 const metaPath=path.join(dir,'profile.json'),data=path.join(dir,'data');
 if(await exists(dir)){
  const meta=await readJson(metaPath);
  if(meta.version!==1||meta.address!==address)throw Error('Profile identity mismatch');
  if(!await exists(data))throw Error('Profile data missing; restore a backup instead of creating an empty profile');
 }else{
  await fs.mkdir(data,{recursive:true});
  await atomic(metaPath,JSON.stringify({version:1,address,createdAt:new Date().toISOString()},null,2));
 }
 return {dir,data};
}
export async function backupCurrent(root){
 await assertStopped(root);const storage=await currentStorage(root);if(storage.root!==root)throw Error('Back up shared Blaze from its owning installation');
 const files=await dataFiles(storage.data);if(!files.length)return null;
 const fingerprint=digest(JSON.stringify(files)),parent=path.join(root,'saves/backups',storage.address);
 const dest=inside(root,path.join(parent,fingerprint));await noLinks(root,dest);
 if(await exists(dest)){const manifest=await readJson(path.join(dest,'manifest.json'));if(manifest.fingerprint!==fingerprint||JSON.stringify(await dataFiles(path.join(dest,'data')))!==JSON.stringify(files))throw Error('Backup verification failed');return dest;}
 await fs.mkdir(parent,{recursive:true});const stage=dest+'.partial-'+randomUUID();await fs.mkdir(stage);
 await copyVerified(storage.data,path.join(stage,'data'),files);
 await atomic(path.join(stage,'manifest.json'),JSON.stringify({version:1,address:storage.address,createdAt:new Date().toISOString(),fingerprint,files},null,2));
 await fs.rename(stage,dest);return dest;
}
export async function validateProfiles(data){
 const ids=await fs.readdir(path.join(data,'players')).catch(e=>{if(e.code==='ENOENT')return [];throw e;});
 for(const id of ids){
  if(!/^\d+$/.test(id))continue;
  const p=await readJson(path.join(data,'players',id,'player.json'));
  if(String(p.Id)!==id||typeof p.Name!=='string'||!p.Stats||typeof p.Stats!=='object'||Array.isArray(p.Stats))throw Error('Invalid player profile: '+id);
 }
}
export async function prepareProfile(root){
 await assertStopped(root);const storage=await currentStorage(root);if(storage.root!==root)throw Error('Prepare shared Blaze from its owning installation');
 const target=profileAddress(storage.settings.advertiseAddress),oldManaged=path.relative(path.join(root,'saves'),storage.data).replaceAll('\\','/').match(/^\d+\.\d+\.\d+\.\d+\/data$/);
 let backup=null;
 if(!oldManaged){
  const files=await dataFiles(storage.data);
  if(files.length){
   backup=await backupCurrent(root);
   const legacyDir=inside(root,path.join(root,'saves',storage.address));await noLinks(root,legacyDir);
   if(await exists(legacyDir)){
    const meta=await readJson(path.join(legacyDir,'profile.json'));
    if(meta.migration?.source!==storage.data||meta.migration?.fingerprint!==digest(JSON.stringify(files)))throw Error('Existing profile conflicts with legacy data; nothing overwritten');
   }else{
    const stage=legacyDir+'.partial-'+randomUUID();await fs.mkdir(stage,{recursive:true});
    await copyVerified(storage.data,path.join(stage,'data'),files);
    await atomic(path.join(stage,'profile.json'),JSON.stringify({version:1,address:storage.address,createdAt:new Date().toISOString(),migration:{source:storage.data,fingerprint:digest(JSON.stringify(files))}},null,2));
    await fs.rename(stage,legacyDir);
   }
  }
 }else backup=await backupCurrent(root);
 const profile=await openProfile(root,target);
 await validateProfiles(profile.data);
 const next=replaceLine(storage.config,'DataRoot',profile.data.replaceAll('\\','/'));
 await atomic(path.join(storage.settings.serverDirectory,'config.txt'),next);
 await atomic(path.join(root,'saves/active.json'),JSON.stringify({version:1,address:target,data:profile.data,selectedAt:new Date().toISOString(),backup},null,2));
 return {address:target,data:profile.data,backup};
}
export async function profileSummary(root){
 const current=await currentStorage(root);const rows=[],errors=[];
 for(const name of await fs.readdir(path.join(current.data,'players')).catch(e=>{if(e.code==='ENOENT')return [];throw e;})){
  if(!/^\d+$/.test(name))continue;
  try{
   const file=path.join(current.data,'players',name,'player.json');await noLinks(current.root,file);
   const p=await readJson(file),stat=await fs.stat(file);
   const statNumber=key=>{const v=Number(p.Stats?.[key]);return Number.isFinite(v)?v:0;};
   rows.push({id:p.Id,name:p.Name,isBot:p.IsBot===true,rank:statNumber('rank'),experience:statNumber('sc_rank'),kills:statNumber('c___k_g'),deaths:statNumber('c___d_g'),revision:p.Revision,reports:p.AppliedReports?.length||0,updatedAt:stat.mtime.toISOString()});
  }catch(e){errors.push(name+': '+e.message);}
 }
 try{
  const {readProgress}=await import('./bot-progress.mjs');const progress=await readProgress(root);
  if(progress){for(const bot of progress.players.filter(p=>p.active||p.experience||p.kills||p.deaths||p.name!=='Bot_'+p.id)){const existing=rows.findIndex(p=>p.isBot&&Number(p.id)===bot.id);if(existing>=0)rows[existing]=bot;else rows.push(bot);}}
 }catch(e){errors.push('Bot: '+e.message);}
 return {address:current.address,data:current.data,selectedAddress:current.settings.advertiseAddress,players:rows.sort((a,b)=>a.id-b.id),humans:rows.filter(p=>!p.isBot).length,bots:rows.filter(p=>p.isBot).length,errors};
}
if(typeof process!=='undefined'&&process.argv[1]&&import.meta.url===pathToFileURL(path.resolve(process.argv[1])).href){
 const root=path.resolve(path.dirname(fileURLToPath(import.meta.url)),'..');
 try{
  const action=process.argv[2];const result=action==='prepare'?await prepareProfile(root):action==='backup'?await backupCurrent(root):action==='status'?await profileSummary(root):null;
  if(result===null&&action!=='backup')throw Error('Unknown profile action');console.log(JSON.stringify(result));
 }catch(e){console.error(e.message);process.exitCode=1;}
}
