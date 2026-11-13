import fs from 'node:fs/promises';import path from 'node:path';import crypto from 'node:crypto';import {fileURLToPath} from 'node:url';
export const sleep=ms=>new Promise(r=>setTimeout(r,ms));
export const json=async p=>JSON.parse((await fs.readFile(p,'utf8')).replace(/^\uFEFF/,''));
export async function atomic(file,value,{attempts=16,delay=20}={}){
 await fs.mkdir(path.dirname(file),{recursive:true});const tmp=file+'.'+crypto.randomUUID()+'.tmp';
 await fs.writeFile(tmp,typeof value==='string'?value:JSON.stringify(value,null,2));
 try{for(let n=0;;n++){try{await fs.rename(tmp,file);return;}catch(error){if(!['EPERM','EACCES','EBUSY'].includes(error.code)||n>=attempts-1)throw error;await sleep(Math.min(delay*(n+1),100));}}}
 finally{await fs.unlink(tmp).catch(error=>{if(error.code!=='ENOENT')return;});}
}
export async function appendDiagnostic(file,value,maxBytes=1048576){
 await fs.mkdir(path.dirname(file),{recursive:true});
 const size=await fs.stat(file).then(s=>s.size,()=>0);if(size>maxBytes){await fs.unlink(file+'.previous').catch(()=>{});await fs.rename(file,file+'.previous').catch(()=>{});}
 await fs.appendFile(file,JSON.stringify({at:Date.now(),...value})+'\n');
}
export async function copyOwned(source,target,base){await fs.access(source);const parent=path.dirname(target);await fs.mkdir(parent,{recursive:true});const real=await fs.realpath(parent),root=await fs.realpath(base);if(!real.toLowerCase().startsWith(root.toLowerCase()+path.sep))throw Error('运行组件目录超出本实例');const data=await fs.readFile(source);let old=await fs.readFile(target).catch(e=>{if(e.code!=='ENOENT')throw e;return null;});if(!old||!old.equals(data)){const tmp=target+'.'+crypto.randomUUID()+'.mod-update.tmp';await fs.writeFile(tmp,data);await fs.rename(tmp,target);}}
export async function runModule(entry,create){
 const contextIndex=process.argv.indexOf('--context');const contextFile=contextIndex>=0?process.argv[contextIndex+1]:null;if(!contextFile)throw Error('缺少模组上下文');let ctx=await json(contextFile);const mod=path.dirname(fileURLToPath(entry));if((await fs.realpath(ctx.modDirectory)).toLowerCase()!==(await fs.realpath(mod)).toLowerCase())throw Error('模组目录身份不匹配');
 const token=ctx.instanceId;let plugin,lastConfig='',stopping=false;const handled=new Set();process.on('SIGTERM',()=>stopping=true);process.on('SIGINT',()=>stopping=true);
 let lastStatusError='',lastStatusErrorAt=0;
 const status=async(state,message,details={})=>{
  try{await atomic(ctx.statusFile,{apiVersion:1,instanceId:token,at:Date.now(),state,message,config:ctx.config,details});return true;}
  catch(error){if(!['EPERM','EACCES','EBUSY'].includes(error.code))throw error;const text=error.stack||error.message;if(text!==lastStatusError||Date.now()-lastStatusErrorAt>30000){lastStatusError=text;lastStatusErrorAt=Date.now();await appendDiagnostic(path.join(ctx.stateDirectory,'diagnostics/status-io.jsonl'),{kind:'status-write-delayed',error:text}).catch(()=>{});}return false;}
 };
 try {
  plugin=await create({get context(){return ctx;},mod,host:path.join(ctx.launcherRoot,'host'),state:ctx.stateDirectory,status});
  while(!stopping){ctx=await json(contextFile);if(ctx.instanceId!==token||Date.now()-ctx.heartbeatAt>15000)break;try{process.kill(ctx.launcher.pid,0);}catch{break;}if(await fs.access(ctx.stopFile).then(()=>true,()=>false))break;
   const config=JSON.stringify(ctx.config);if(config!==lastConfig){await plugin.configure?.(ctx.config);lastConfig=config;}
   await plugin.tick?.();const dir=ctx.actionDirectory||path.join(path.dirname(ctx.statusFile),'actions');await fs.mkdir(dir,{recursive:true});
   for(const name of (await fs.readdir(dir)).filter(x=>/^[a-f0-9]{32}\.request\.json$/.test(x)).sort()){
    if(handled.has(name))continue;handled.add(name);const request=await json(path.join(dir,name));if(request.instanceId!==token||request.requestId!==name.slice(0,32)||!Number.isFinite(request.at)||Date.now()-request.at>40000||request.at-Date.now()>5000)continue;const response={instanceId:token,requestId:request.requestId,at:Date.now()};try{if(!plugin.actions?.[request.action])throw Error('模组不支持此操作');response.value=await plugin.actions[request.action](request.body||{});response.ok=true;}catch(e){response.ok=false;response.error=e.message;}await atomic(path.join(dir,request.requestId+'.response.json'),response);
   }
   await sleep(350);
  }
 }catch(e){await appendDiagnostic(path.join(ctx.stateDirectory,'diagnostics/runtime-errors.jsonl'),{kind:'runtime-error',error:e.stack||e.message}).catch(()=>{});await status('error',e.message).catch(()=>{});throw e;}finally{try{await plugin?.close?.();}catch{}if(stopping||await fs.access(ctx.stopFile).then(()=>true,()=>false))await status('stopped','模组已停止');}
}
