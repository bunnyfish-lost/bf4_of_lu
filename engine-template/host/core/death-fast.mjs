import fs from 'node:fs/promises';
import path from 'node:path';
import {spawn} from 'node:child_process';

export class DeathFast{
 constructor(root,options={}){this.file=path.join(root,'settings/death-fast.json');this.run=options.run||runInstaller;}
 async invoke(args){
  let config;try{config=JSON.parse(await fs.readFile(this.file,'utf8'));}catch(e){if(e.code==='ENOENT')return {state:'unavailable',message:'尚未配置死亡卡片1秒补丁。'};throw e;}
  if(config.enabled===false&&!args.includes('--restore'))return {state:'disabled',message:'死亡卡片缩短已关闭；下次启动不加载补丁。'};
  if(!path.isAbsolute(config.directory||''))throw Error('1秒补丁目录无效');
  const exe=path.join(config.directory,'BF4DeathFast.exe');await fs.access(exe);
  return this.run(exe,[...args,'--json']);
 }
 async snapshot(){try{return await this.invoke(['--status']);}catch(e){return {state:'error',message:'1秒补丁核验失败：'+e.message};}}
 async waitForLaunch(){try{return await this.invoke([]);}catch(e){return {state:'error',message:'1秒补丁等待游戏启动失败：'+e.message+'；请退出 BF4 后重试。'};}}
 async install(pid){
  if(!Number.isInteger(pid)||pid<=0)throw Error('未获得有效的游戏进程编号，未加载1秒补丁。');
  try{return await this.invoke(['--pid',String(pid)]);}catch(e){return {state:'error',pid,message:'游戏已启动，但1秒补丁加载失败：'+e.message+'；请退出 BF4 后重试。'};}
 }
}
function runInstaller(exe,args){return new Promise((resolve,reject)=>{
 const child=spawn(exe,args,{cwd:path.dirname(exe),windowsHide:true,shell:false});let stdout='',stderr='',done=false;
 const finish=(error,result)=>{if(done)return;done=true;clearTimeout(timer);error?reject(error):resolve(result);};
 const timeout=args.includes('--pid')||args.includes('--status')?15000:190000;
 const timer=setTimeout(()=>{child.kill();finish(Error('等待补丁结果超时，请退出 BF4 后重试'));},timeout);
 child.stdout.setEncoding('utf8');child.stderr.setEncoding('utf8');child.stdout.on('data',v=>stdout=(stdout+v).slice(-16384));child.stderr.on('data',v=>stderr=(stderr+v).slice(-4096));
 child.on('error',e=>finish(e));child.on('close',code=>{try{const result=JSON.parse(stdout.trim().replace(/^\uFEFF/,''));if(!result.state||typeof result.message!=='string')throw Error('补丁返回格式异常');if(code&&result.state!=='error')throw Error(result.message);finish(null,result);}catch(e){finish(Error(stderr.trim()||e.message));}});
});}
