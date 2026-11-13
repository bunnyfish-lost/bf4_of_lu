import fs from 'node:fs';
import path from 'node:path';
import {spawn} from 'node:child_process';
const file=process.argv[2];
let context=JSON.parse(fs.readFileSync(file,'utf8').replace(/^\uFEFF/,''));
const descriptor=path.join(path.dirname(file),'process.json');
const write=value=>{const temp=descriptor+'.tmp';fs.writeFileSync(temp,JSON.stringify({instanceId:context.instanceId,wrapperPid:process.pid,at:Date.now(),...value}));fs.renameSync(temp,descriptor);};
let child,stopping=false,deadline=0,forced=false,ended=false;
const log=fs.openSync(context.logFile,'a');
function stop(){if(stopping)return;stopping=true;deadline=Date.now()+6000;fs.writeFileSync(context.stopFile,'stop');}
try{
 child=spawn(context.launch.executable,context.launch.arguments,{cwd:context.launch.workingDirectory,windowsHide:true,stdio:['ignore',log,log],env:{...process.env,BF4_MOD_CONTEXT:file}});
 child.once('spawn',()=>write({phase:'running',pid:child.pid,executable:context.launch.executable}));
 child.once('error',error=>{if(ended)return;ended=true;write({phase:'error',message:error.message});fs.closeSync(log);process.exitCode=1;clearInterval(timer);});
 child.once('exit',(exitCode,signal)=>{if(ended)return;ended=true;write({phase:stopping?'stopped':'exited',pid:child.pid,exitCode,signal,forced});fs.closeSync(log);clearInterval(timer);});
}catch(error){write({phase:'error',message:error.message});fs.closeSync(log);throw error;}
const timer=setInterval(()=>{
 try{
  context=JSON.parse(fs.readFileSync(file,'utf8').replace(/^\uFEFF/,''));
  let parentAlive=true;try{process.kill(context.launcher.pid,0);}catch{parentAlive=false;}
  if(fs.existsSync(context.stopFile)||!parentAlive||Date.now()-context.heartbeatAt>10000)stop();
 }catch{stop();}
 if(stopping&&Date.now()>deadline&&!ended&&!forced){forced=true;if(process.platform==='win32')spawn('taskkill.exe',['/PID',String(child.pid),'/T','/F'],{windowsHide:true,stdio:'ignore'});else child.kill();}
},250);
process.on('SIGINT',stop);process.on('SIGTERM',stop);
