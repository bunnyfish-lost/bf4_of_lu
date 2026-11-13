import {runtimeProcesses} from './runtime-processes.mjs';
import fs from 'node:fs/promises';
import path from 'node:path';
import crypto from 'node:crypto';
import {createReadStream} from 'node:fs';
import {fileURLToPath} from 'node:url';
export const runtimeVersion='v2-1107',launcherVersion='0.3.0-1107';
const base=path.dirname(path.dirname(fileURLToPath(import.meta.url)));
const read=async p=>JSON.parse((await fs.readFile(p,'utf8')).replace(/^\uFEFF/,''));
const stat=async p=>{try{return await fs.lstat(p);}catch(e){if(e.code==='ENOENT')return null;throw e;}};
const same=(a,b)=>path.resolve(a).toLowerCase()===path.resolve(b).toLowerCase();
const inside=(root,p)=>{const rel=path.relative(root,path.resolve(p));return !!rel&&!rel.startsWith('..'+path.sep)&&rel!=='..'&&!path.isAbsolute(rel);};
export async function digest(p){const h=crypto.createHash('sha256');for await(const b of createReadStream(p))h.update(b);return h.digest('hex');}
const jsonText=v=>JSON.stringify(v,null,2)+'\n';
function line(s,key){return s.match(new RegExp('^[ \t]*'+key+'[ \t]*=[ \t]*([^\r\n]*)','mi'))?.[1].trim();}
function setLine(s,key,value){const re=new RegExp('^[ \t]*'+key+'[ \t]*=[^\r\n]*','gmi');return re.test(s)?s.replace(re,()=>key+'='+value):s.trimEnd()+'\r\n'+key+'='+value+'\r\n';}
export class Environment {
 constructor(root=base,{processProbe=runtimeProcesses}={}){this.processProbe=processProbe;this.root=path.resolve(root);this.host=path.join(this.root,'host');this.game=path.join(this.host,'runtime/GameServer');this.blaze=path.join(this.host,'runtime/Blaze');this.client=path.join(this.host,'mods/client');}
 async config(){return read(path.join(this.root,'config.json'));}
 managed(p){p=path.resolve(p);if(!inside(this.root,p))throw Error('运行文件不在本启动器目录内：'+p);return p;}
 async sourcePlan(c){
  const server=path.resolve(c.sourceServer||''),client=path.resolve(c.gameDirectory||'');
  if(inside(this.root,server)||inside(this.root,client)||same(server,this.root)||same(client,this.root))throw Error('请选择启动器目录以外的 v2 游戏和 Server 文件夹。');
  const spec=await read(path.join(this.host,'templates/compatibility.json')),clients=await read(path.join(this.root,'engine/client-patches.json'));
  const copies=Object.entries(spec.files).map(([name,sha256])=>({source:path.join(server,name),target:path.join(name==='BF4BlazeServer.exe'?this.blaze:this.game,name),sha256,role:'v2 服务端'}));
  for(const s of clients){if(s.bytes?.length)throw Error('v2 不使用旧版登录器字节补丁。');copies.push({source:path.join(client,s.file),target:path.join(this.client,s.file),sha256:s.originalSha256,role:'v2 客户端'});}
  for(const name of ['builtLevels.json','ProviderID.dat','ServerOptions.cfg'])copies.push({source:path.join(server,name),target:path.join(this.game,name),role:'服务端资源',optional:['ProviderID.dat','ServerOptions.cfg'].includes(name)});
  for(const name of ['initfs_DedicatedServer','layout.toc','sanity.toc'])copies.push({source:path.join(server,'Data',name),target:path.join(this.game,'Data',name),role:'服务端索引'});
  for(const name of ['d3dcompiler_46.dll','Engine.BuildInfo_Win64_retail.dll','Extension.Twinkle.Codec_Win64_retail.dll','EAWebKit64.dll','Extension.Twinkle.JavaScriptCore_Win64_retail.dll'])copies.push({source:path.join(client,name),target:path.join(this.client,name),role:'客户端运行库'});
  const links=[{source:path.join(server,'Data/DedicatedServer'),target:path.join(this.game,'Data/DedicatedServer')},{source:path.join(server,'assets'),target:path.join(this.blaze,'assets')}];
  for(const name of ['Data','Update','Core','pb'])links.push({source:path.join(client,name),target:path.join(this.client,name),optional:name==='pb'});
  return {copies,links,requiredResources:[path.join(server,'assets/http.bfpack'),path.join(server,'assets/default'),path.join(server,'assets/stats')]};
 }
 async inspect({source=true,hashes=true}={}){
  const c=await this.config(),plan=await this.sourcePlan(c),items=[];
  for(const entry of plan.copies){
   let sourceStat=null;if(source){sourceStat=await stat(entry.source);if(!sourceStat){if(!entry.optional)items.push({kind:'source-missing',file:entry.source,role:entry.role});continue;}if(!sourceStat.isFile())items.push({kind:'source-invalid',file:entry.source,role:entry.role});else if(hashes&&entry.sha256&&await digest(entry.source)!==entry.sha256)items.push({kind:'source-version',file:entry.source,role:entry.role});}
   const targetStat=await stat(entry.target);if(!targetStat){if(!entry.optional||sourceStat)items.push({kind:'runtime-missing',file:entry.target});continue;}
   if(targetStat.isSymbolicLink()||!targetStat.isFile())items.push({kind:'runtime-invalid',file:entry.target});
   else if(hashes&&(entry.sha256||sourceStat)&&await digest(entry.target)!==(entry.sha256||await digest(entry.source)))items.push({kind:'runtime-version',file:entry.target});
  }
  for(const entry of plan.links){if(source&&!await stat(entry.source)){if(!entry.optional)items.push({kind:'source-missing',file:entry.source});continue;}const target=await stat(entry.target);if(!target){if(!entry.optional||await stat(entry.source))items.push({kind:'link-missing',file:entry.target});continue;}try{if(!same(await fs.realpath(entry.target),await fs.realpath(entry.source)))items.push({kind:'link-target',file:entry.target});}catch{items.push({kind:'link-target',file:entry.target});}}
  if(source)for(const p of plan.requiredResources)if(!await stat(p))items.push({kind:'source-missing',file:p,role:'v2 资源'});
  const required=[path.join(this.game,'Instance/Admin/Startup.txt'),path.join(this.game,'Instance/Admin/MapList.txt'),path.join(this.game,'Scripts/Win32Game.cfg'),path.join(this.game,'dinput8_bf4hook.ini'),path.join(this.blaze,'config.txt')];
  for(const p of required)if(!await stat(p))items.push({kind:'config-missing',file:p});
  for(const p of ['settings/host-settings.json','runtime/private.json','runtime/processes.json','settings/bot-loadout.json','settings/bot-damage.json','panel/bot-fill.json','settings/client-game.json']){try{await read(path.join(this.host,p));}catch{items.push({kind:'config-invalid',file:path.join(this.host,p)});}}
  try{const s=await read(path.join(this.host,'settings/host-settings.json'));if(!same(s.gameServerDirectory||'',this.game)||!same(s.serverDirectory||'',this.blaze))items.push({kind:'relocated',file:path.join(this.host,'settings/host-settings.json')});}catch{}
  try{const state=await read(path.join(this.root,'data/environment.json'));if(state.runtimeVersion!==runtimeVersion||state.complete!==true)items.push({kind:'runtime-upgrade',file:path.join(this.root,'data/environment.json')});}catch{items.push({kind:'not-initialized',file:path.join(this.root,'data/environment.json')});}
  return {ok:items.length===0,runtimeVersion,launcherVersion,items,checkedAt:new Date().toISOString(),plan};
 }
 async assertIdle(){
  const processes=await this.processProbe();const expected=[path.join(this.game,'bf4_Server_Final.exe'),path.join(this.blaze,'BF4BlazeServer.exe'),path.join(this.client,'bf4_no_drm.exe'),path.join(this.client,'BF4Launcher.exe')];if(processes.some(p=>!p||p.unreadable||typeof p.path!=='string'||!p.path.trim()))throw Error('无法确认游戏进程所属目录，请用管理员运行后再检查。');if(processes.some(p=>expected.some(e=>same(e,p.path))))throw Error('本实例仍有运行中的服务或客户端，请先停止后再修复运行文件。');
  const state=await read(path.join(this.host,'runtime/processes.json')).catch(()=>({processes:[]}));
  for(const p of state.processes||[]){try{process.kill(Number(p.pid),0);}catch(e){if(e.code==='ESRCH')continue;if(e.code==='EPERM')throw Error('请先停止本实例服务，再检查和更新运行文件。');throw e;}throw Error('请先停止本实例服务，再检查和更新运行文件。');}
 }
 async repair({apply=async()=>{},progress=()=>{}}={}){
  await fs.mkdir(path.join(this.root,'data'),{recursive:true});const lockFile=path.join(this.root,'data/environment.lock');let lock;
  try{lock=await fs.open(lockFile,'wx');await lock.writeFile(jsonText({pid:process.pid,at:Date.now()}));}catch(e){if(e.code==='EEXIST'){const prior=await read(lockFile).catch(()=>null);if(prior?.pid){try{process.kill(prior.pid,0);}catch(x){if(x.code==='ESRCH'){await fs.unlink(lockFile);return this.repair({apply,progress});}}}throw Error('另一个运行环境检查正在进行。');}throw e;}
  let tx=null;try{
   progress('检查 v2 来源文件与运行环境');const check=await this.inspect(),sourceErrors=check.items.filter(i=>i.kind.startsWith('source-'));
   if(sourceErrors.length)throw Error('v2 来源文件需要检查：\n'+sourceErrors.map(i=>(i.kind==='source-version'?'版本不匹配':'缺少或无法读取')+' · '+(i.role||'v2 资源')+' '+path.basename(i.file)+'：'+i.file).join('\n'));
   if(check.ok)return {initialized:true,repaired:0,...check,plan:undefined};
   await this.assertIdle();for(const item of check.items.filter(i=>i.kind==='config-invalid'&&['bot-loadout.json','bot-damage.json','bot-fill.json'].includes(path.basename(i.file)))){if(await stat(item.file))throw Error('配置格式需要检查，已保留原文件：'+item.file);}const id=new Date().toISOString().replace(/[:.]/g,'-')+'-'+crypto.randomUUID().slice(0,8);const dir=path.join(this.root,'data/environment-backups',id);await fs.mkdir(dir,{recursive:true});
   const watched=new Map(),changes=[];tx={dir,watched,changes};
   const watch=async p=>{p=this.managed(p);for(let parent=path.dirname(p);inside(this.root,parent);parent=path.dirname(parent)){if((await stat(parent))?.isSymbolicLink())throw Error('运行配置的父目录不能指向实例外部：'+parent);}if(watched.has(p))return;const old=await stat(p);if(old?.isDirectory()&&!old.isSymbolicLink())throw Error('保留现有目录，未替换：'+p);let backup=null;if(old){backup=path.join(dir,String(watched.size));if(old.isSymbolicLink())await fs.symlink(await fs.readlink(p),backup,'junction');else await fs.copyFile(p,backup);}watched.set(p,{backup,link:!!old?.isSymbolicLink()});};
   const write=async(p,bytes)=>{await watch(p);await fs.mkdir(path.dirname(p),{recursive:true});const tmp=p+'.environment-'+crypto.randomUUID()+'.tmp';try{await fs.writeFile(tmp,bytes);await fs.rename(tmp,p);}finally{await fs.unlink(tmp).catch(()=>{});}changes.push(p);};
   const writeJson=async(p,value)=>write(p,jsonText(value));
   const ensure=async(p,bytes)=>{if(!await stat(p))await write(p,bytes);};
   for(const entry of check.plan.copies){if(!await stat(entry.source))continue;const want=entry.sha256||await digest(entry.source);const current=await stat(entry.target);if(current?.isFile()&&!current.isSymbolicLink()&&await digest(entry.target)===want)continue;progress('准备 '+path.basename(entry.target));await watch(entry.target);await fs.mkdir(path.dirname(entry.target),{recursive:true});const temp=entry.target+'.environment-'+crypto.randomUUID()+'.tmp';try{await fs.copyFile(entry.source,temp);if(await digest(temp)!==want)throw Error('来源文件在复制时变化：'+entry.source);await fs.rename(temp,entry.target);}finally{await fs.unlink(temp).catch(()=>{});}changes.push(entry.target);}
   for(const entry of check.plan.links){if(!await stat(entry.source))continue;const current=await stat(entry.target);if(current){try{if(same(await fs.realpath(entry.target),await fs.realpath(entry.source)))continue;}catch{}if(!current.isSymbolicLink())throw Error('资源位置已有实体目录，已保留：'+entry.target);}
    await watch(entry.target);await fs.mkdir(path.dirname(entry.target),{recursive:true});if(current)await fs.unlink(entry.target);await fs.symlink(entry.source,entry.target,'junction');changes.push(entry.target);
   }
   const c=await this.config(),settingsFile=path.join(this.host,'settings/host-settings.json');let prior=await read(settingsFile).catch(()=>({}));if(prior.sharedBlaze)throw Error('请先解除共用后端，再更新为 v2 独立运行环境。');
   const settings={installationId:crypto.randomUUID(),version:'0.8.3',serverName:'BF4 LOCAL BOTS',advertiseAddress:'127.0.0.1',gamePort:25205,rconPort:25210,panelPort:8787,maxSoldiers:64,initialBots:31,blazeTcpPorts:[80,443,10040,10041,4217,42127,42230],...prior,serverDirectory:this.blaze,gameServerDirectory:this.game,offlineVersion:runtimeVersion};
   await writeJson(settingsFile,settings);
   const startup=path.join(this.game,'Instance/Admin/Startup.txt'),secretFile=path.join(this.host,'runtime/private.json');const oldStartup=await fs.readFile(startup,'utf8').catch(()=>''),fromStartup=oldStartup.match(/^admin\.password[ \t]+([a-f0-9]{16})[ \t]*$/mi)?.[1];
   let secret=await read(secretFile).catch(()=>null);if(!secret?.rconPassword)secret={rconPassword:fromStartup||crypto.randomBytes(8).toString('hex')};if(!/^[a-f0-9]{16}$/.test(secret.rconPassword))throw Error('本实例管理密码格式需要检查：'+secretFile);await writeJson(secretFile,secret);
   const template=async n=>fs.readFile(path.join(this.host,'templates',n),'utf8');
   await ensure(startup,(await template('Startup.txt')).trimEnd()+'\r\nadmin.password '+secret.rconPassword+'\r\n');
   let startupText=await fs.readFile(startup,'utf8');const passLine=/^admin\.password[^\r\n]*$/gmi;startupText=passLine.test(startupText)?startupText.replace(passLine,'admin.password '+secret.rconPassword):startupText.trimEnd()+'\r\nadmin.password '+secret.rconPassword+'\r\n';if(startupText!==await fs.readFile(startup,'utf8'))await write(startup,startupText);
   for(const [name,target] of [['MapList.txt',path.join(this.game,'Instance/Admin/MapList.txt')],['Win32Game.cfg',path.join(this.game,'Scripts/Win32Game.cfg')],['dinput8_bf4hook.ini',path.join(this.game,'dinput8_bf4hook.ini')]])await ensure(target,await template(name));
   const hook=path.join(this.game,'dinput8_bf4hook.ini');let hookText=await fs.readFile(hook,'utf8');for(const [k,v]of [['ExternalIP',''],['ExternalPort','0'],['BotRandomRank','1']])if(line(hookText,k)===undefined)hookText=setLine(hookText,k,v);if(hookText!==await fs.readFile(hook,'utf8'))await write(hook,hookText);
   let blazeText=await fs.readFile(path.join(this.blaze,'config.txt'),'utf8').catch(()=>null);let dataRoot=blazeText?line(blazeText,'DataRoot'):null;
   if(!dataRoot){const active=await read(path.join(this.host,'saves/active.json')).catch(()=>null);if(active?.data)dataRoot=active.data;else if(await stat(path.join(this.host,'saves',settings.advertiseAddress,'data')))dataRoot=path.join(this.host,'saves',settings.advertiseAddress,'data');}
   if(dataRoot&&path.isAbsolute(dataRoot)&&prior.serverDirectory){const oldHost=path.dirname(path.dirname(prior.serverDirectory));if(inside(oldHost,dataRoot)){const rebound=path.join(this.host,path.relative(oldHost,dataRoot));if(await stat(dataRoot)&&!await stat(rebound))throw Error('原玩家档案未迁移，请保留档案目录：'+dataRoot);dataRoot=rebound;}}
   const state=await read(path.join(this.root,'data/environment.json')).catch(()=>null),migrating=state?.runtimeVersion!==runtimeVersion||check.items.some(i=>i.kind==='runtime-version'&&same(i.file,path.join(this.blaze,'BF4BlazeServer.exe')));
   let nextBlaze=blazeText&&!migrating?blazeText:await template('BlazeConfig.txt');
   if(blazeText&&migrating){const defaults=await template('BlazeConfig-v1.txt');const pairs=Object.fromEntries([...defaults.matchAll(/^([A-Za-z0-9_]+)[ \t]*=[ \t]*([^\r\n]*)/gm)].map(x=>[x[1],x[2].trim()]));pairs.ListenAddress='127.0.0.1';for(const row of blazeText.matchAll(/^([A-Za-z0-9_]+)[ \t]*=[ \t]*([^\r\n]*)/gm)){const key=row[1],value=row[2].trim();if(!['ServerIP','DataRoot','AssetsRoot'].includes(key)&&pairs[key]!==value)nextBlaze=setLine(nextBlaze,key,value);}}
   nextBlaze=setLine(nextBlaze,'ServerIP',settings.advertiseAddress);if(!blazeText||migrating&&[undefined,'127.0.0.1'].includes(line(blazeText,'ListenAddress')))nextBlaze=setLine(nextBlaze,'ListenAddress','127.0.0.1');nextBlaze=setLine(nextBlaze,'DataRoot',dataRoot||'data');nextBlaze=setLine(nextBlaze,'AssetsRoot','assets');
   if(nextBlaze!==blazeText)await write(path.join(this.blaze,'config.txt'),nextBlaze);
   for(const n of ['BanList.txt','ReservedSlotsList.txt'])await ensure(path.join(this.game,'Instance/Admin',n),'');
   const records=path.join(this.host,'runtime/processes.json');try{await read(records);}catch{await writeJson(records,{processes:[]});}
   for(const [file,value] of [['settings/bot-loadout.json',{version:1,weaponIds:[]}],['settings/bot-damage.json',{version:1,percent:100}],['runtime/processes.json',{processes:[]}],['panel/bot-fill.json',{enabled:false,target:64,reserve:0}]])await ensure(path.join(this.host,file),jsonText(value));
   for(const p of [startup,hook,path.join(this.game,'Instance/Admin/MapList.txt'),path.join(this.game,'Scripts/Win32Game.cfg'),path.join(this.blaze,'config.txt'),settingsFile,path.join(this.host,'settings/client-game.json'),path.join(this.client,'config.txt')])await watch(p);
   progress('恢复实例配置并应用当前设置');await apply();
   await writeJson(path.join(this.root,'data/environment.json'),{schemaVersion:2,complete:true,runtimeVersion,launcherVersion,root:this.root,sourceServer:c.sourceServer,sourceGame:c.gameDirectory,at:new Date().toISOString()});
   await writeJson(path.join(this.root,'data/source-fingerprints.json'),{runtimeVersion,files:Object.fromEntries(check.plan.copies.filter(p=>p.sha256).map(p=>[p.role+'/'+path.basename(p.source),p.sha256]))});
   const verified=await this.inspect();if(!verified.ok)throw Error('修复后的运行文件仍需检查：'+verified.items.map(i=>path.basename(i.file)).join('、'));
   const result={initialized:true,repaired:changes.length,runtimeVersion,backup:dir,changes,at:new Date().toISOString()};await fs.writeFile(path.join(dir,'result.json'),jsonText(result));return result;
  }catch(error){if(tx){for(const [p,old] of [...tx.watched].reverse()){try{const current=await stat(p);if(current?.isSymbolicLink())await fs.unlink(p);else if(current?.isFile())await fs.unlink(p);if(old.backup){await fs.mkdir(path.dirname(p),{recursive:true});if(old.link)await fs.symlink(await fs.readlink(old.backup),p,'junction');else await fs.copyFile(old.backup,p);}}catch(rollback){await fs.appendFile(path.join(tx.dir,'rollback-errors.log'),p+' '+rollback.message+'\n');}}await fs.writeFile(path.join(tx.dir,'error.json'),jsonText({error:error.message,at:new Date().toISOString()}));}throw error;
  }finally{await lock?.close();await fs.unlink(lockFile).catch(()=>{});}
 }
 async requireReady(){const check=await this.inspect({source:true,hashes:true});if(!check.ok)throw Error('运行环境需要检查：请点击“检查并修复运行环境”。\n'+check.items.map(i=>i.kind+' · '+i.file).join('\n'));return check;}
}
