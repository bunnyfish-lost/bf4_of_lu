import fs from 'node:fs/promises';
import path from 'node:path';
import net from 'node:net';
import { spawn } from 'node:child_process';
import { fileURLToPath, pathToFileURL } from 'node:url';
import { randomUUID } from 'node:crypto';
import {upgradeHost} from './upgrade-host.mjs';
import { ConfigStore } from '../panel/config-store.mjs';

import {releaseVersion,upgradeFrom} from './version.mjs';
export {releaseVersion};
export function validateInstallPath(value) {
  if(typeof value!=='string'||!path.win32.isAbsolute(value)||! /^[A-Za-z]:\\[A-Za-z0-9_ .\\-]+$/.test(value))throw Error('请选择英文安装位置，例如 D:\\BF4Host。压缩包本身可以放在中文目录。');
  const clean=path.win32.normalize(value).replace(/\\+$/,'');
  if(clean.split('\\').some(p=>p==='.'||p==='..'||/[ .]$/.test(p))||clean.length<6||/^[A-Za-z]:\\(?:Windows|Program Files(?: \(x86\))?|Users|ProgramData|Recovery|System Volume Information)$/i.test(clean))throw Error('请选择专用的 BF4Host 文件夹，不要选择系统目录或磁盘根目录。');
  return clean;
}
export function chooseRadmin(adapters) {
  return adapters.filter(a=>a.up && /radmin|famatech/i.test(a.name+' '+a.description) && /^(?:\d{1,3}\.){3}\d{1,3}$/.test(a.address) && !/^(127\.|169\.254\.|0\.)/.test(a.address));
}
export async function resolvePlayNetwork(request,getAdapters) {
  const playMode=request.playMode??(request.address==='127.0.0.1'?'local':'lan');
  if(!['local','lan','custom'].includes(playMode))throw Error('请选择联机或仅本机游玩。');
  if(playMode==='local')return {playMode,address:'127.0.0.1'};
  const adapters=await getAdapters();
  if(playMode==='custom'){
    if(typeof request.address!=='string'||net.isIPv4(request.address)!==true||/^(0\.|127\.|169\.254\.)/.test(request.address)||Number(request.address.split('.')[0])>=224||!adapters.some(a=>a.up&&a.address===request.address))throw Error('请输入主机当前已启用网卡的 IPv4 地址，不能填写客机或公网映射地址。');
    return {playMode,address:request.address};
  }
  if(!chooseRadmin(adapters).some(a=>a.address===request.address))throw Error('所选 Radmin 地址已失效。请确认 Radmin 在线，点击“重新检测”后再开服。');
  return {playMode,address:request.address};
}
export async function normalizeSource(value) {
  for(const dir of [value,path.join(value,'Server')])try{await fs.access(path.join(dir,'BF4BlazeServer.exe'));await fs.access(path.join(dir,'Data/initfs_DedicatedServer'));return path.resolve(dir);}catch{}
  throw Error('没有找到完整的原版 Server。请选择包含 BF4BlazeServer.exe 的文件夹，或它上一层的原版补丁文件夹。');
}
export async function atomicJson(file,value) {
  const tmp=file+'.'+randomUUID()+'.tmp';await fs.writeFile(tmp,JSON.stringify(value,null,2));await fs.rename(tmp,file);
}
export async function runScript(root,relative,args=[],options={}) {
  const logDir=options.logDir||path.join(root,'runtime/launcher-logs');await fs.mkdir(logDir,{recursive:true});
  const stamp=Date.now()+'-'+randomUUID().slice(0,8),outPath=path.join(logDir,stamp+'.out.log'),errPath=path.join(logDir,stamp+'.err.log');
  const out=await fs.open(outPath,'w'),err=await fs.open(errPath,'w');
  const ps=(process.env.BF4_LAUNCHER_PS||path.join(process.env.SystemRoot||'C:/Windows','System32/WindowsPowerShell/v1.0/powershell.exe'));
  try {
    await new Promise((resolve,reject)=>{
      const child=(options.spawn||spawn)(ps,['-NoLogo','-NoProfile','-NonInteractive','-ExecutionPolicy','Bypass','-File',path.join(root,relative),...args],{windowsHide:true,stdio:['ignore',out.fd,err.fd]});
      const timer=setTimeout(()=>{child.kill();reject(Error('操作超时，请查看诊断记录；已有游戏服务未自动终止。'));},options.timeout||240000);
      child.once('error',e=>{clearTimeout(timer);reject(e);});
      child.once('exit',(code,signal)=>{clearTimeout(timer);code===0?resolve():reject(Error('脚本执行失败（'+(code??signal)+'）'));});
    });
    return await fs.readFile(outPath,'utf8');
  } catch(e) {
    const detail=(await fs.readFile(outPath,'utf8')).trim()+'\n'+(await fs.readFile(errPath,'utf8')).trim();
    throw Error(e.message+'\n'+detail.trim());
  } finally {await out.close();await err.close();}
}
export async function copyPackage(source,destination,options={}) {
  const manifest=JSON.parse(await fs.readFile(path.join(source,'manifest.json'),'utf8'));
  await fs.mkdir(destination,{recursive:false});
  options.onCreated?.();
  for(const relative of [...manifest.files.map(f=>f.file),'manifest.json','FILES.sha256']) {
    const dest=path.resolve(destination,relative),src=path.resolve(source,relative);
    if(!dest.startsWith(path.resolve(destination)+path.sep)||!src.startsWith(path.resolve(source)+path.sep))throw Error('发布包文件清单异常。');
    await fs.mkdir(path.dirname(dest),{recursive:true});await fs.copyFile(src,dest);
  }
}
export async function installHost(request,options={}) {
  const target=validateInstallPath(request.installRoot);
  if(path.resolve(target).toLowerCase()===path.resolve(request.packageRoot).toLowerCase())throw Error('安装位置请与解压目录分开，例如 D:\\BF4Host。');
  try {const entries=await fs.readdir(target);if(entries.length)throw Error('安装位置已有文件。请选择一个空文件夹，原文件不会被覆盖。');}catch(e){if(e.code!=='ENOENT')throw e;}
  const source=await normalizeSource(request.source);
  if(typeof request.serverName!=='string'||!request.serverName.trim()||request.serverName.length>60||/["\\\x00-\x1f]/.test(request.serverName))throw Error('服务器名称为 1～60 字，不能包含引号或换行。');
  const stage=target+'.install-'+randomUUID().slice(0,8);
  let ownedStage=false;
  const run=options.run||runScript;
  try {
    await copyPackage(request.packageRoot,stage,{onCreated:()=>{ownedStage=true;}});
    await run(stage,'core/Setup-Host.ps1',['-SourceServer',source,'-Address',request.address,'-PlayMode',request.address==='127.0.0.1'?'local':'lan','-ServerName',request.serverName],{logDir:request.logDir});
    const settingsPath=path.join(stage,'settings/host-settings.json');
    const settings=JSON.parse((await fs.readFile(settingsPath,'utf8')).replace(/^\uFEFF/,''));
    settings.serverDirectory=path.join(target,'runtime/Blaze');settings.gameServerDirectory=path.join(target,'runtime/GameServer');
    await atomicJson(settingsPath,settings);
    await fs.writeFile(path.join(stage,'managed-install.json'),JSON.stringify({version:releaseVersion,installationId:settings.installationId}));
    // Only an empty, explicitly selected destination may be removed before the rename.
    try{await fs.rmdir(target);}catch(e){if(e.code!=='ENOENT')throw e;}
    await fs.rename(stage,target);
    return settings;
  } catch(e) {
    let retained=false;
    if(ownedStage && path.dirname(path.resolve(stage))===path.dirname(path.resolve(target)) && path.resolve(stage).startsWith(path.resolve(target)+'.install-')){
      try{await fs.rm(stage,{recursive:true,force:true});}catch{retained=true;}
    }
    e.message+='\n安装未完成，修正问题后可重新点击开服。'+(retained?'临时文件保留于：'+stage:'');throw e;
  }
}
export async function readInstalled(root) {
  const marker=JSON.parse(await fs.readFile(path.join(root,'managed-install.json'),'utf8'));
  const store=new ConfigStore(path.join(root,'settings'),path.join(root,'panel'));
  const snap=await store.snapshot();
  if(![...upgradeFrom,releaseVersion].includes(marker.version)||marker.installationId!==snap.settings.installationId)throw Error('此目录不是同版本向导创建的安装。请为新安装选择空目录。');
  for(const [key,rel]of Object.entries({serverDirectory:'runtime/Blaze',gameServerDirectory:'runtime/GameServer'}))if(path.resolve(snap.settings[key]).toLowerCase()!==path.resolve(root,rel).toLowerCase())throw Error('安装目录曾被移动，请移回原位置。');
  return {store,snap};
}
export async function findPanelPort(preferred,excluded=[],probe) {
  const available=probe||(async port=>{
    const unused=await new Promise(resolve=>{const socket=net.createConnection({host:'127.0.0.1',port});const finish=result=>{socket.destroy();resolve(result);};socket.once('connect',()=>finish(false));socket.once('error',e=>finish(e.code==='ECONNREFUSED'));socket.setTimeout(500,()=>finish(false));});
    if(!unused)return false;
    return new Promise(resolve=>{const server=net.createServer();server.once('error',()=>resolve(false));server.listen({port,host:'127.0.0.1',exclusive:true},()=>server.close(()=>resolve(true)));});
  });
  const ports=[preferred,...Array.from({length:32},(_,i)=>8787+i)].filter(p=>Number.isInteger(p)&&p>1023&&p<65536&&!excluded.includes(p));
  for(const port of new Set(ports))if(await available(port))return port;
  throw Error('没有找到空闲的本机面板端口，请关闭其他管理面板后重试。');
}
export function friendlyError(message) {
  if(/Panel port \d+ belongs to another service/i.test(message))return '管理网页端口被占用。请重试一键开服，工具会重新选择空闲端口。';
  if(/ports already in use/i.test(message))return '开服端口已被其他程序占用。请在“高级工具 → 检测端口”查看占用程序。';
  if(/Unsupported or missing file|Unsupported server binary/i.test(message))return '原版补丁文件缺失或版本不匹配。请使用 BF4 LAN BOT ALPHA v0.1（2026-10-15）的完整 Server。';
  if(/missing source|Missing source/i.test(message))return '原版 Server 文件不完整，请先完整解压作者补丁。';
  if(/Access.*denied|UnauthorizedAccess|拒绝访问/i.test(message))return '权限不足，请重新双击“开始开服”，并允许 Windows 的管理员提示。';
  if(/Blaze exited|Blaze startup timed out/i.test(message))return '登录服务没有启动成功。请查看诊断记录，重点检查 80、443、42230 是否被占用。';
  return message.split('\n').slice(0,3).join('\n');
}
export async function runWorkflow(request,options={}) {
  const status=options.status||(async()=>{}),run=options.run||runScript;
  const requestHttp=options.fetch||fetch,wait=options.wait||(ms=>new Promise(r=>setTimeout(r,ms)));
  const root=validateInstallPath(request.installRoot);
  const settingsFile=path.join(root,'settings/host-settings.json');
  if(request.mode!=='Stop' && request.freshInstall){
    try{if((await fs.readdir(root)).length)throw Error('重新设置需要空的安装目录，请点击“更改位置”选择新的位置。');}
    catch(e){if(e.code!=='ENOENT')throw e;}
  }
  const remember=async()=>{
    await atomicJson(path.join(request.packageRoot,'launcher-state.json'),{version:releaseVersion,installRoot:root});
    await atomicJson(path.join(root,'launcher-state.json'),{version:releaseVersion,installRoot:root});
  };
  if(request.mode==='Stop') {
    await readInstalled(root);await status({state:'running',title:'正在关闭服务器',detail:'',percent:40});
    await run(root,'core/Entry.ps1',['-Action','Stop'],{logDir:request.logDir});
    await status({state:'done',title:'服务器已关闭',detail:'',percent:100});return;
  }
  const network=await resolvePlayNetwork(request,async()=>options.adapters||JSON.parse(await run(request.packageRoot,'core/Radmin-Addresses.ps1',[],{logDir:request.logDir})));
  request={...request,...network};
  await status({state:'running',title:'正在检查文件',detail:'',percent:5});
  await run(request.packageRoot,'tools/Verify-Package.ps1',[],{logDir:request.logDir});
  let installed;
  try{await fs.access(settingsFile);installed=true;}catch(e){if(e.code!=='ENOENT')throw e;installed=false;}
  if(!installed){await status({state:'running',title:'正在安装服务器',detail:'复制约 2.3 GB 资源',percent:20});await installHost(request,{run});}
  if(installed){
    const marker=JSON.parse(await fs.readFile(path.join(root,'managed-install.json'),'utf8'));
    if(upgradeFrom.includes(marker.version)){
      await status({state:'running',title:'正在更新开服工具',detail:'备份旧版与配置',percent:35});
      await upgradeHost({...request,installRoot:root},run);
    }
  }
  await remember();
  let {store,snap}=await readInstalled(root);
  const changed=snap.settings.advertiseAddress!==request.address;
  if(changed){
    if(!request.allowAddressRestart)throw Error('游玩方式或主机地址已改变，请确认重启服务后应用新地址。');
    await status({state:'running',title:'正在更新主机地址',detail:'重启服务以应用新地址',percent:50});
    await run(root,'core/Host-Control.ps1',['-Action','Stop'],{logDir:request.logDir});
    const current=await store.publicConfig();await store.save({...current,values:{...current.values,advertiseAddress:request.address}});snap=await store.snapshot();
  }
  await status({state:'running',title:'正在检查地址和端口',detail:'',percent:55});
  await run(root,'core/Host-Control.ps1',['-Action','Check'],{logDir:request.logDir});
  await status({state:'running',title:request.playMode==='local'?'正在应用本机模式':'正在设置联机访问',detail:'',percent:62});
  await run(root,'core/Firewall.ps1',[],{logDir:request.logDir});
  let panelReady=false;
  try{const response=await requestHttp('http://127.0.0.1:'+snap.settings.panelPort+'/api/bootstrap',{signal:AbortSignal.timeout(900)});const boot=await response.json();panelReady=boot.app==='bf4-host-panel'&&boot.installationId===snap.settings.installationId;}catch{}
  if(!panelReady){
    await run(root,'core/Panel-Control.ps1',['-Action','Stop'],{logDir:request.logDir});
    const port=await findPanelPort(snap.settings.panelPort,[...snap.settings.blazeTcpPorts,snap.settings.rconPort]);snap.settings.panelPort=port;await atomicJson(settingsFile,snap.settings);
  }
  await status({state:'running',title:'正在启动服务器',detail:'',percent:75});
  await run(root,'core/Panel-Control.ps1',['-Action','Start','-NoBrowser'],{logDir:request.logDir});
  await run(root,'core/Host-Control.ps1',['-Action','Start'],{logDir:request.logDir});
  const url='http://127.0.0.1:'+snap.settings.panelPort+'/';
  await status({state:'running',title:'等待地图加载',detail:'',percent:90,url});
  const deadline=Date.now()+60000;let ready=false;
  while(Date.now()<deadline){try{const state=await(await requestHttp(url+'api/state',{signal:AbortSignal.timeout(6000)})).json();if(state.online&&state.gameState==='IN_GAME'){ready=true;break;}}catch{}await wait(1500);}
  await status({state:ready?'done':'waiting',title:ready?'开服成功':'服务已启动，地图仍在加载',detail:ready?(request.playMode==='local'?'仅本机游玩，启动器 Blaze IP 填 127.0.0.1。':''):'等待 IN_GAME 后加入。',percent:100,url,address:request.address,serverName:snap.settings.serverName});
}
if(process.argv[1]&&import.meta.url===pathToFileURL(path.resolve(process.argv[1])).href){
  const request=JSON.parse((await fs.readFile(process.argv[2],'utf8')).replace(/^\uFEFF/,''));
  const status=value=>atomicJson(request.progressFile,value);
  try{await runWorkflow(request,{status});}
  catch(e){await fs.appendFile(path.join(request.logDir,'error.txt'),new Date().toISOString()+'\n'+e.stack+'\n');await status({state:'error',title:'暂时无法开服',detail:friendlyError(e.message),percent:0,logDir:request.logDir});process.exitCode=1;}
}
