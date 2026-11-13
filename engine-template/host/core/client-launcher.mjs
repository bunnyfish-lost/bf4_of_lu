import fs from 'node:fs/promises';
import path from 'node:path';
import {spawn} from 'node:child_process';
import {randomUUID} from 'node:crypto';
import {DeathFast} from './death-fast.mjs';

export function parseLauncherConfig(text){
 const value=key=>text.replace(/^\uFEFF/,'').match(new RegExp('^\\s*'+key+'\\s*=\\s*([^\\r\\n]*)','mi'))?.[1]?.trim()||'';
 return {playerId:value('UserId')||value('AuthCode'),address:value('BlazeIP')};
}
export function validatePlayerId(value){
 if(typeof value!=='string'||!value.trim()||value.length>64||/[=\r\n\x00-\x1f]/.test(value))throw Error('玩家 ID 请输入 1～64 个字符，不能包含等号或控制字符');
 return value.trim();
}
export class ClientLauncher{
 constructor(root,options={}){this.root=root;this.file=path.join(root,'settings/client-game.json');this.run=options.run||this.runScript.bind(this);this.deathFast=options.deathFast||new DeathFast(root);this.busy=false;}
 async settings(){return JSON.parse((await fs.readFile(path.join(this.root,'settings/host-settings.json'),'utf8')).replace(/^\uFEFF/,''));}
 async validateDirectory(input){
  if(typeof input!=='string'||!path.isAbsolute(input.trim())||/[\x00-\x1f]/.test(input))throw Error('请选择游戏文件夹的完整路径');
  let directory=path.resolve(input.trim());
  if(path.basename(directory).toLowerCase()==='bf4launcher.exe')directory=path.dirname(directory);
  for(const name of ['BF4Launcher.exe','bf4_no_drm.exe'])if(!(await fs.stat(path.join(directory,name)).catch(()=>null))?.isFile())throw Error('游戏文件夹内找不到 '+name);
  return await fs.realpath(directory);
 }
 async inspect(input){
  const directory=await this.validateDirectory(input);
  const config=await fs.readFile(path.join(directory,'config.txt'),'utf8').catch(e=>{if(e.code==='ENOENT')return '';throw e;});
  return {directory,...parseLauncherConfig(config)};
 }
 async snapshot(){
  const settings=await this.settings();let saved={};
  try{saved=JSON.parse(await fs.readFile(this.file,'utf8'));}catch(e){if(e.code!=='ENOENT')throw e;}
  const address=settings.playMode==='local'?'127.0.0.1':settings.advertiseAddress;
  return {directory:saved.directory||'',playerId:saved.playerId||'',address,busy:this.busy,deathFast:await this.deathFast.snapshot()};
 }
 async save(body){
  const directory=await this.validateDirectory(body.directory),playerId=validatePlayerId(body.playerId);
  await fs.mkdir(path.dirname(this.file),{recursive:true});
  const tmp=this.file+'.'+randomUUID()+'.tmp';
  try{await fs.writeFile(tmp,JSON.stringify({directory,playerId},null,2)+'\n');await fs.rename(tmp,this.file);}finally{await fs.rm(tmp,{force:true}).catch(()=>{});}
  return this.snapshot();
 }
 async action(kind,body={}){
  if(this.busy)throw Error('正在处理游戏启动操作，请等待完成');this.busy=true;
  try{
   if(kind==='inspect')return await this.inspect(body.directory);
   if(kind==='pick'){const result=await this.run({action:'Pick',directory:body.directory||''});return result.cancelled?result:await this.inspect(result.directory);}
   if(kind==='save')return await this.save(body);
   if(kind!=='start')throw Error('未知游戏启动操作');
   const saved=await this.save(body);
   if(!/^(?:\d{1,3}\.){3}\d{1,3}$/.test(saved.address)||saved.address.split('.').some(x=>Number(x)>255))throw Error('主机 Blaze IP 无效');
   const result=await this.run({action:'Launch',...saved});
   let deathFast;
   if(result.started&&Number.isInteger(result.gamePid)){
    try{deathFast=await this.deathFast.install(result.gamePid);}catch(e){deathFast={state:'error',message:'游戏已启动，但1秒补丁加载失败：'+e.message};}
   }else if(result.launcherPid&&!result.gamePid)deathFast=await this.deathFast.waitForLaunch();
   else deathFast=await this.deathFast.snapshot();
   if(!result.started&&result.launcherPid&&deathFast.state==='applied')return {...result,started:true,gamePid:deathFast.pid,message:'游戏已启动。游玩期间请保留 BF4Launcher 窗口。',deathFast};
   return {...result,deathFast};
  }finally{this.busy=false;}
 }
 async runScript(request){
  const dir=path.join(this.root,'runtime/client-launch');await fs.mkdir(dir,{recursive:true});
  const file=path.join(dir,randomUUID()+'.json');await fs.writeFile(file,JSON.stringify(request));
  try{return await new Promise((resolve,reject)=>{
   const ps=(process.env.BF4_LAUNCHER_PS||path.join(process.env.SystemRoot||'C:/Windows','System32/WindowsPowerShell/v1.0/powershell.exe'));
   const child=spawn(ps,['-NoLogo','-NoProfile','-NonInteractive','-STA','-ExecutionPolicy','Bypass','-File',path.join(this.root,'core/Launch-Client.ps1'),'-RequestFile',file],{cwd:this.root,windowsHide:true,shell:false});
   let stdout='',stderr='',ended=false;const timer=setTimeout(()=>{child.kill();reject(Error('操作等待超时；如启动器已打开，可在其中继续启动游戏'));},request.action==='Pick'?120000:45000);
   child.stdout.setEncoding('utf8');child.stderr.setEncoding('utf8');child.stdout.on('data',v=>{stdout=(stdout+v).slice(-65536);});child.stderr.on('data',v=>{stderr=(stderr+v).slice(-4096);});
   child.on('error',e=>{clearTimeout(timer);ended=true;reject(e);});const finish=code=>{clearTimeout(timer);if(ended)return;ended=true;try{const result=JSON.parse(stdout.trim().replace(/^\uFEFF/,''));if(code||result.error)throw Error(result.error||'启动失败');resolve(result);}catch(e){reject(Error(e instanceof SyntaxError?stderr.trim()||stdout.trim()||'启动组件未返回结果':e.message));}};child.on('close',finish);child.on('exit',code=>setTimeout(()=>finish(code),250));
  });}finally{await fs.rm(file,{force:true});}
 }
}
