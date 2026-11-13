import path from 'node:path';
import {fileURLToPath} from 'node:url';
import {json,atomic} from './runtime.mjs';
import {openAudioTools} from './audio-library.mjs';
export async function runLocalAudioContext(file,options={}){
 const context=await json(file);let tools;
 try{
  if(context.apiVersion!==1||context.mode!=='local-action'||typeof context.requestId!=='string')throw Error('本地音效操作上下文不正确');
  const mod=path.dirname(fileURLToPath(import.meta.url));if(path.resolve(context.moduleDirectory).toLowerCase()!==mod.toLowerCase())throw Error('音效模组目录与当前版本不一致');
  tools=await openAudioTools(mod,context.stateDirectory,options);const value=await tools.execute(context.action,context.body||{});
  const result={apiVersion:1,requestId:context.requestId,ok:true,value};await atomic(context.resultFile,result);return result;
 }catch(error){const result={apiVersion:1,requestId:context.requestId,ok:false,error:error.message};await atomic(context.resultFile,result);return result;}
 finally{await tools?.close();}
}
if(process.argv[1]&&path.resolve(process.argv[1])===fileURLToPath(import.meta.url)){
 const at=process.argv.indexOf('--context');if(at<0||!process.argv[at+1])throw Error('缺少本地操作上下文');
 const result=await runLocalAudioContext(process.argv[at+1]);if(!result.ok)process.exitCode=1;
}
