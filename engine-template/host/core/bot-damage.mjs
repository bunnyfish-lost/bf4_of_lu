import fs from 'node:fs/promises';
import path from 'node:path';
import {createHash,randomUUID} from 'node:crypto';
const token=()=>randomUUID().replaceAll('-','');
const revision=text=>createHash('sha256').update(text??'<missing>').digest('hex');
const json=async file=>JSON.parse((await fs.readFile(file,'utf8')).replace(/^\uFEFF/,''));
const optional=async file=>{try{return await fs.readFile(file,'utf8');}catch(e){if(e.code==='ENOENT')return null;throw e;}};
export function validateDamagePercent(value){if(!Number.isInteger(value)||value<0||value>200||value%10!==0)throw Error('人机伤害须为 0～200%，以 10% 为单位');return value;}
export async function atomicDamageWrite(file,text){const tmp=file+'.'+token()+'.tmp';try{await fs.writeFile(tmp,text,'utf8');await fs.rename(tmp,file);}finally{await fs.rm(tmp,{force:true}).catch(()=>{});}}
export async function damageSettings(root){const file=path.join(root,'settings/bot-damage.json'),text=await optional(file);const data=text===null?{version:1,percent:100}:JSON.parse(text.replace(/^\uFEFF/,''));if(data.version!==1)throw Error('无法识别人机伤害配置版本');return {file,text,percent:validateDamagePercent(data.percent),revision:revision(text)};}
export async function prepareDamage(root,session){
 if(!/^[a-f0-9]{32}$/.test(session))throw Error('Invalid Bot damage session');
 const settings=await json(path.join(root,'settings/host-settings.json')),game=path.join(root,'runtime/GameServer');
 if(path.resolve(settings.gameServerDirectory).toLowerCase()!==path.resolve(game).toLowerCase())throw Error('人机伤害安装目录不匹配');
 const config=await damageSettings(root),requestId=token();
 await atomicDamageWrite(path.join(game,'bot-damage.ini'),'[BotDamage]\r\nSessionId='+session+'\r\nRequestId='+requestId+'\r\nPercent='+config.percent+'\r\n');
 return {percent:config.percent,requestId,session};
}
export class BotDamage {
 constructor(root,options={}){this.root=root;this.clock=options.clock||Date.now;this.wait=options.wait||(ms=>new Promise(r=>setTimeout(r,ms)));this.checks=options.checks??30;}
 async snapshot(){
  const config=await damageSettings(this.root),base={percent:config.percent,revision:config.revision,min:0,max:200,step:10,appliedPercent:null,applied:false,restartRequired:false};
  const settings=await json(path.join(this.root,'settings/host-settings.json'));
  const game=path.join(this.root,'runtime/GameServer');if(path.resolve(settings.gameServerDirectory).toLowerCase()!==path.resolve(game).toLowerCase())throw Error('人机伤害安装目录不匹配');
  const raw=await optional(path.join(this.root,'runtime/processes.json'));const records=raw===null?[]:JSON.parse(raw.replace(/^\uFEFF/,'')).processes?.filter(p=>p.role==='game')||[];
  if(!records.length)return {...base,state:'stopped',message:'已保存；下次开服自动应用'};
  if(records.length!==1||path.resolve(records[0].path).toLowerCase()!==path.resolve(game,'bf4_Server_Final.exe').toLowerCase())throw Error('游戏服进程记录不匹配');
  const rec=records[0];let gate;try{gate=await json(path.join(this.root,'runtime/bot-start.json'));}catch(e){if(e.code!=='ENOENT')throw e;}
  if(!gate||gate.phase!=='ready'||gate.installationId!==settings.installationId||gate.pid!==rec.pid||String(gate.startTicks)!==String(rec.startTicks)||!gate.damage)return {...base,state:'restart-required',restartRequired:true,message:'已保存；重启游戏服后加载伤害模块'};
  if(gate.damageBinding!=='v2-damage-context-2')return {...base,state:'restart-required',restartRequired:true,message:'已保存；重启游戏服后加载 v2 伤害修正版'};
  let live,stat;try{const file=path.join(game,'bot-damage-state.json');live=await json(file);stat=await fs.stat(file);}catch(e){if(e.code!=='ENOENT'&&!(e instanceof SyntaxError))throw e;}
  if(!live||live.version!==1||!['damage-1','damage-2'].includes(live.build)||live.pid!==rec.pid||live.session!==gate.session||live.ready!==true||this.clock()-stat.mtimeMs>5000||stat.mtimeMs-this.clock()>5000)return {...base,state:'unconfirmed',message:'尚未收到当前游戏服的伤害状态'};
  if(!Number.isInteger(live.percent)||live.percent<0||live.percent>200||live.percent%10)throw Error('游戏服伤害回读无效');
  if(!live.configValid)return {...base,state:'invalid-runtime',appliedPercent:live.percent,message:'运行配置无效，游戏服保留上次伤害设置'};
  const ini=await optional(path.join(game,'bot-damage.ini'));const requestId=ini?.match(/^RequestId=([a-f0-9]{32})\r?$/m)?.[1];
  const applied=live.percent===config.percent&&live.requestId===requestId;
  return {...base,state:applied?'applied':'pending',applied,appliedPercent:live.percent,requestId:live.requestId,session:live.session,pid:live.pid,message:applied?'伤害模块已应用 '+live.percent+'%，后续命中按新倍率计算':'配置已保存，等待游戏服确认'};
 }
 async save(input){
  const percent=validateDamagePercent(input.percent),before=await damageSettings(this.root);if(input.revision!==before.revision)throw Error('人机伤害配置已变化，请重新读取后再保存');
  const previous=await this.snapshot();
  const backup=path.join(this.root,'panel/backups','bot-damage-'+token()+'.json');await fs.mkdir(path.dirname(backup),{recursive:true});await fs.writeFile(backup,JSON.stringify({at:new Date().toISOString(),before:before.text,percent},null,2));
  if(await optional(before.file)!==before.text)throw Error('人机伤害配置已变化，请重新读取后再保存');
  await atomicDamageWrite(before.file,JSON.stringify({version:1,percent},null,2)+'\n');
  if(previous.state==='stopped'||previous.restartRequired)return {...await this.snapshot(),saved:true};
  const gate=await json(path.join(this.root,'runtime/bot-start.json'));
  let prepared;try{prepared=await prepareDamage(this.root,gate.session);}catch(e){return {...await this.snapshot(),saved:true,applied:false,message:'已保存，实时应用失败：'+e.message};}
  for(let i=0;i<this.checks;i++){await this.wait(100);const current=await this.snapshot();if(current.applied&&current.requestId===prepared.requestId)return {...current,saved:true};if(current.state==='stopped'||current.restartRequired)return {...current,saved:true};}
  return {...await this.snapshot(),saved:true,applied:false,message:'已保存，但未确认实时生效；请刷新检查当前值'};
 }
}
