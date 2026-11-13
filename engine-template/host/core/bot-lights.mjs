import fs from 'node:fs/promises';
import path from 'node:path';
import {createHash,randomUUID} from 'node:crypto';
const token=()=>randomUUID().replaceAll('-','');
const revision=text=>createHash('sha256').update(text??'<missing>').digest('hex');
const json=async file=>JSON.parse((await fs.readFile(file,'utf8')).replace(/^\uFEFF/,''));
const optional=async file=>{try{return await fs.readFile(file,'utf8');}catch(e){if(e.code==='ENOENT')return null;throw e;}};
const boolean=value=>{if(typeof value!=='boolean')throw Error('AI 配件设置须为开或关');return value;};
const mode=value=>{if(!['off','all'].includes(value))throw Error('固定配件规则只支持关闭或全部 AI');return value;};
async function atomicWrite(file,text){const tmp=file+'.'+token()+'.tmp';try{await fs.writeFile(tmp,text,'utf8');await fs.rename(tmp,file);}finally{await fs.rm(tmp,{force:true}).catch(()=>{});}}
export async function lightsSettings(root){
 const file=path.join(root,'settings/bot-lights.json'),text=await optional(file),data=text===null?{version:3,mode:'off'}:JSON.parse(text.replace(/^\uFEFF/,''));
 if(![1,2,3].includes(data.version))throw Error('无法识别 AI 配件配置版本');
 const selected=data.version===1?(boolean(data.enabled)?'all':'off'):(data.version===2&&data.mode==='enemies'?'all':mode(data.mode));
 return {file,text,mode:selected,wholeRail:true,enabled:selected==='all',revision:revision(text)};
}
export async function prepareLights(root,session){
 if(!/^[a-f0-9]{32}$/.test(session))throw Error('Invalid Bot lights session');
 const settings=await json(path.join(root,'settings/host-settings.json')),game=path.join(root,'runtime/GameServer');
 if(path.resolve(settings.gameServerDirectory).toLowerCase()!==path.resolve(game).toLowerCase())throw Error('AI 配件安装目录不匹配');
 const config=await lightsSettings(root),requestId=token();
 await atomicWrite(path.join(game,'bot-lights.ini'),'[BotLights]\r\nSessionId='+session+'\r\nRequestId='+requestId+'\r\nEnabled='+(config.enabled?'1':'0')+'\r\nScope='+config.mode+'\r\nWholeRail=1\r\n');
 return {mode:config.mode,wholeRail:true,enabled:config.enabled,requestId,session};
}
export class BotLights {
 constructor(root){this.root=root;}
 async snapshot(){
  const config=await lightsSettings(this.root),base={policy:'fixed-startup',mode:config.mode,wholeRail:true,enabled:config.enabled,revision:config.revision,appliedMode:null,appliedEnabled:null,applied:false,restartRequired:false};
  const settings=await json(path.join(this.root,'settings/host-settings.json')),game=path.join(this.root,'runtime/GameServer');
  if(path.resolve(settings.gameServerDirectory).toLowerCase()!==path.resolve(game).toLowerCase())throw Error('AI 配件安装目录不匹配');
  const raw=await optional(path.join(this.root,'runtime/processes.json')),records=raw===null?[]:JSON.parse(raw.replace(/^\uFEFF/,'')).processes?.filter(p=>p.role==='game')||[];
  if(!records.length)return {...base,state:'stopped',message:'已保存；应用时开服并加载固定规则'};
  if(records.length!==1||path.resolve(records[0].path).toLowerCase()!==path.resolve(game,'bf4_Server_Final.exe').toLowerCase())throw Error('游戏服进程记录不匹配');
  const rec=records[0];let gate;try{gate=await json(path.join(this.root,'runtime/bot-start.json'));}catch(e){if(e.code!=='ENOENT')throw e;}
  if(!gate||gate.phase!=='ready'||gate.installationId!==settings.installationId||gate.pid!==rec.pid||String(gate.startTicks)!==String(rec.startTicks)||!gate.lights)return {...base,state:'restart-required',restartRequired:true,message:'已保存；应用时重启游戏服加载固定规则'};
  let live;try{live=await json(path.join(game,'bot-lights-state.json'));}catch(e){if(e.code!=='ENOENT'&&!(e instanceof SyntaxError))throw e;}
  if(['lights-1','lights-2','lights-3'].includes(live?.build)&&live.pid===rec.pid&&live.session===gate.session)return {...base,state:'restart-required',restartRequired:true,message:'已保存；应用时重启游戏服，移除旧的后台检查'};
  if(!live||live.version!==1||live.build!=='lights-4'||live.policy!=='fixed-startup'||live.pid!==rec.pid||live.session!==gate.session||live.ready!==true||live.configValid!==true||live.wholeRail!==true)return {...base,state:'unconfirmed',message:'尚未取得本次开服的固定规则加载记录'};
  mode(live.mode);boolean(live.enabled);if(live.enabled!==(live.mode==='all'))throw Error('固定配件规则记录不一致');
  const ini=await optional(path.join(game,'bot-lights.ini')),requestId=ini?.match(/^RequestId=([a-f0-9]{32})\r?$/m)?.[1];
  if(!requestId||live.requestId!==requestId)return {...base,state:'unconfirmed',message:'固定规则加载记录与本次开服配置不匹配'};
  const applied=live.mode===config.mode;
  return {...base,state:applied?'applied':'restart-required',applied,restartRequired:!applied,appliedMode:live.mode,appliedEnabled:live.enabled,pid:live.pid,session:live.session,requestId:live.requestId,message:applied?(live.enabled?'本次开服已固定禁用全部 AI 的该配件栏；无定时检查':'本次开服已关闭该配件栏限制'):'已保存；应用时重启游戏服切换固定规则'};
 }
 async save(input){
  const selected=input.mode===undefined?(boolean(input.enabled)?'all':'off'):mode(input.mode),before=await lightsSettings(this.root);
  if(input.revision!==before.revision)throw Error('AI 配件配置已变化，请重新读取后再保存');
  const backup=path.join(this.root,'panel/backups','bot-lights-'+token()+'.json');await fs.mkdir(path.dirname(backup),{recursive:true});await fs.writeFile(backup,JSON.stringify({at:new Date().toISOString(),before:before.text,mode:selected,policy:'fixed-startup'},null,2));
  if(await optional(before.file)!==before.text)throw Error('AI 配件配置已变化，请重新读取后再保存');
  await atomicWrite(before.file,JSON.stringify({version:3,mode:selected},null,2)+'\n');
  return {...await this.snapshot(),saved:true};
 }
}
