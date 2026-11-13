import fs from 'node:fs/promises';
import path from 'node:path';
import {spawn} from 'node:child_process';
const requestFile=process.argv[2];
try {
 const request=JSON.parse(await fs.readFile(requestFile,'utf8'));
 if(!path.isAbsolute(request.exe)||!Array.isArray(request.args)||request.args.some(a=>typeof a!=='string'))throw Error('运行任务格式不正确');
 const outFile=requestFile+'.stdout.log',errFile=requestFile+'.stderr.log';
 const out=await fs.open(outFile,'w'),err=await fs.open(errFile,'w');
 let child,timer,pumpTimer,timeout=false,position=0,pumping=false;
 async function pump(final=false){if(pumping)return;pumping=true;try{const text=await fs.readFile(outFile,'utf8');const end=final?text.length:text.lastIndexOf('\n')+1;if(end>position){const fresh=text.slice(position,end);position=end;for(const line of fresh.split(/\r?\n/).filter(Boolean))process.stdout.write('@OUT '+JSON.stringify(line)+'\n');}}finally{pumping=false;}}
 try {
  const result=await new Promise((resolve,reject)=>{
   child=spawn(request.exe,request.args,{cwd:request.cwd,windowsHide:true,shell:false,stdio:['ignore',out.fd,err.fd]});
   timer=setTimeout(()=>{timeout=true;child.kill();},Math.max(100,request.timeoutMs||180000));
   child.once('error',reject);child.once('exit',(code,signal)=>resolve({exitCode:code??-1,signal}));
   pumpTimer=setInterval(()=>pump().catch(()=>{}),150);
  });
  clearTimeout(timer);clearInterval(pumpTimer);
  while(pumping)await new Promise(r=>setTimeout(r,5));await pump(true);
  process.stdout.write('@RUNNER '+JSON.stringify({...result,timedOut:timeout,outFile,errFile})+'\n');
 } finally {clearTimeout(timer);clearInterval(pumpTimer);await out.close();await err.close();}
} catch(e){console.error(e.message);process.exitCode=1;}
