import fs from 'node:fs/promises';
import path from 'node:path';
import {createHash,randomUUID} from 'node:crypto';
import {releaseVersion,upgradeFrom} from './version.mjs';
import {ConfigStore} from '../panel/config-store.mjs';
import {ruleFields,validateConfig} from '../panel/schema.mjs';
const read=async p=>JSON.parse((await fs.readFile(p,'utf8')).replace(/^﻿/,''));
function inside(root,relative){const p=path.resolve(root,relative);if(!p.startsWith(path.resolve(root)+path.sep))throw Error('Invalid upgrade path');return p;}
async function noLinks(root,relative){
 let current=path.resolve(root);
 for(const part of ['',...relative.split(/[\\/]/).filter(Boolean)]){current=part?path.join(current,part):current;try{if((await fs.lstat(current)).isSymbolicLink())throw Error('Upgrade target contains a link');}catch(e){if(e.code!=='ENOENT')throw e;}}
}
export async function upgradeHost(request,run){
 const target=request.installRoot,source=request.packageRoot;
 const marker=await read(path.join(target,'managed-install.json'));
 const settings=await read(path.join(target,'settings/host-settings.json'));
 if(marker.installationId!==settings.installationId)throw Error('安装身份不匹配');
 if(marker.version===releaseVersion)return;
 if(!upgradeFrom.includes(marker.version))throw Error('只能更新由 0.4.0、0.5.0、0.6.0、0.7.0、0.7.1、0.7.2、0.7.3、0.7.4-test.1、0.7.4beta 、0.8.0、0.8.1beta 、0.8.1 、0.8.1 修复版或 0.8.2 向导创建的安装');
 if(!request.allowUpgrade)throw Error('更新将重启服务器，请先确认升级');
 const manifest=await read(path.join(source,'manifest.json'));
 if(manifest.version!==releaseVersion)throw Error('更新包版本不符');
 for(const row of manifest.files){
  const bytes=await fs.readFile(inside(source,row.file));
  if(createHash('sha256').update(bytes).digest('hex')!==row.sha256)throw Error('更新包校验失败：'+row.file);
 }
 const store=new ConfigStore(path.join(target,'settings'),path.join(target,'panel'));
 const snapshot=await store.snapshot(),values={...snapshot.values},adjustments=[];
 for(const f of ruleFields){
  if(!Number.isInteger(values[f.id]))throw Error(f.label+'旧值不是整数，请先修正');
  const next=Math.min(f.max,Math.max(f.min,values[f.id]));
  if(next!==values[f.id]){adjustments.push({field:f.id,label:f.label,from:values[f.id],to:next});values[f.id]=next;}
 }
 if(snapshot.ammoMigration)adjustments.push({field:'infiniteMags',label:'无限火力兼容配置',from:'弹药参数同时开启',to:'无限火力（免换弹）'});
 let rotation=snapshot.rotation;
 const retained=rotation.filter(row=>row.map!=='MP_Flooded');
 if(retained.length!==rotation.length){
  rotation=retained.length?retained:[{map:'MP_Prison',mode:'ConquestLarge0',rounds:1}];
  adjustments.push({field:'rotation',label:'地图轮换',from:snapshot.rotation,to:rotation});
 }
 validateConfig(values,rotation);
 const configNames=[...Object.values(snapshot.files),...Object.values(snapshot.mirrors).map(m=>m.path)].map(p=>{
  const relative=path.relative(target,p);inside(target,relative);return relative;
 });
 const names=[...new Set([...manifest.files.map(f=>f.file),'manifest.json','FILES.sha256',...configNames,'managed-install.json'])];
 for(const name of names)await noLinks(target,name);
 const backup=path.join(target,'backups','update-'+marker.version+'-'+Date.now()+'-'+randomUUID().slice(0,8));
 await fs.mkdir(backup,{recursive:true});
 const records=[];
 for(const name of names){
  const file=inside(target,name);let existed=true;
  try{await fs.mkdir(path.dirname(inside(backup,name)),{recursive:true});await fs.copyFile(file,inside(backup,name));}catch(e){if(e.code!=='ENOENT')throw e;existed=false;}
  records.push({file:name,existed});
 }
 await fs.writeFile(path.join(backup,'upgrade-files.json'),JSON.stringify(records,null,2));
 await run(target,'core/Panel-Control.ps1',['-Action','Stop'],{logDir:request.logDir});
 await run(target,'core/Host-Control.ps1',['-Action','Stop'],{logDir:request.logDir});
 const written=[];
 async function put(name,bytes){const file=inside(target,name);await fs.mkdir(path.dirname(file),{recursive:true});const temp=file+'.upgrade-'+randomUUID();try{await fs.writeFile(temp,bytes);await fs.rename(temp,file);written.push(name);}finally{await fs.unlink(temp).catch(e=>{if(e.code!=='ENOENT')throw e;});}}
 try{
  for(const name of [...manifest.files.map(f=>f.file),'manifest.json','FILES.sha256'])await put(name,await fs.readFile(inside(source,name)));
  await put('settings/host-settings.json',JSON.stringify({...settings,version:releaseVersion},null,2));
  const current=await store.publicConfig();
  const migrated=await store.save({...current,values,rotation});
  if(migrated.backup){for(const row of await read(path.join(migrated.backup,'manifest.json')))written.push(path.relative(target,row.path));}
  await fs.writeFile(path.join(backup,'migration.json'),JSON.stringify({from:marker.version,to:releaseVersion,preset:'CUSTOM',serverType:'UNRANKED',adjustments},null,2));
  await put('managed-install.json',JSON.stringify({...marker,version:releaseVersion}));
 }catch(e){
  const failed=[];
  for(const name of [...new Set(written.reverse())])try{const old=records.find(r=>path.resolve(target,r.file)===path.resolve(target,name));if(!old)throw Error('Missing backup entry');if(old.existed)await fs.copyFile(inside(backup,name),inside(target,name));else await fs.unlink(inside(target,name));}catch{failed.push(name);}
  throw Error('更新失败，服务保持停止。'+(failed.length?'部分文件需要手动恢复。':'已恢复原文件。')+'备份：'+backup+'\n'+e.message);
 }
 return {backup,adjustments};
}
