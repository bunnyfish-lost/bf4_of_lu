import {ClientConnection} from './bf4-client-connection.mjs';
import fs from 'node:fs/promises';
import path from 'node:path';
import {spawn} from 'node:child_process';
import {randomUUID} from 'node:crypto';
import {AssistSettings} from './bf4-local-assist.mjs';

export class LocalAssistRuntime extends AssistSettings {
 constructor(root,manager){super(root);this.manager=manager;this.connection=new ClientConnection({root});this.experiment=path.join(root,'core/local-assist');this.session=randomUUID();this.lastSkip=0;this.buffer='';this.runtimeFile=path.join(root,'runtime/local-assist-input.json');}
 async writeInput(){const s=await super.snapshot();await fs.mkdir(path.dirname(this.runtimeFile),{recursive:true});const tmp=this.runtimeFile+'.tmp';await fs.writeFile(tmp,JSON.stringify({skipDeath:false,teamSpawn:s.teamSpawn,autoSpot:s.autoSpot,skipKey:s.skipKey,virtualKey:s.virtualKey,revision:s.revision}));await fs.rename(tmp,this.runtimeFile);}
 async start(){
  await this.writeInput();
  const child=spawn(path.join(this.experiment,'BF4VisionAssist.exe'),[this.runtimeFile,this.session,String(process.pid)],{cwd:this.experiment,windowsHide:true,stdio:['ignore','pipe','pipe']});this.child=child;this.runtime={pid:child.pid,session:this.session};
  child.stdout.setEncoding('utf8');child.stdout.on('data',chunk=>{this.buffer+=chunk;if(this.buffer.length>65536){this.buffer='';return;}let end;while((end=this.buffer.indexOf('\n'))>=0){const line=this.buffer.slice(0,end).trim();this.buffer=this.buffer.slice(end+1);try{const event=JSON.parse(line);this.onEvent(event).catch(e=>this.note(e.message));}catch{}}});
  child.stderr.setEncoding('utf8');child.stderr.on('data',text=>{this.error=text.slice(0,300);});
  child.on('error',e=>{this.error=e.message;this.runtime={pid:null,session:null};});child.on('exit',()=>{this.runtime={pid:null,session:null};this.child=null;});
 }
 async note(message){this.error=message;await fs.appendFile(path.join(this.root,'panel/activity.jsonl'),JSON.stringify({at:new Date().toISOString(),action:'local-assist',details:{},result:message})+'\n');}
 async onEvent(event){
  if(event.session!==this.session)return;
  if(event.kind==='status'&&event.pid===this.child?.pid){await fs.writeFile(path.join(this.root,'runtime/local-assist-state.json'),JSON.stringify(event));return;}
  // Death skip is unavailable. Never route a stale key press into admin.killPlayer.
  return;
 }
 async snapshot(){const s=await super.snapshot();let report=null;try{const live=JSON.parse(await fs.readFile(path.join(this.root,'runtime/local-assist-state.json'),'utf8'));if(live.pid===this.runtime.pid&&live.session===this.runtime.session&&Number.isFinite(live.at)&&Math.abs(Date.now()-live.at)<4000)report=live;}catch{}const clientConnection=await this.connection.snapshot(report);return {...s,skipDeath:false,skipAvailable:false,clientConnection,message:clientConnection.connected?(this.error||s.message):clientConnection.message,experiment:true,teamRestartRequired:s.teamSpawn!==(s.runtime?.teamClientApplied??s.runtime?.teamSpawn)&&!!s.runtime,visionRestartRequired:s.autoSpot!==s.runtime?.autoSpot&&!!s.runtime};}
 async save(input){if(input.skipDeath)throw Error('死亡跳过暂不可用');const result=await super.save(input);try{await this.writeInput();this.error='';}catch(e){this.error='设置已保存，辅助模块更新失败：'+e.message;}return {...await this.snapshot(),saved:result.saved};}
 async close(){if(this.child){this.child.kill();this.child=null;}this.runtime={pid:null,session:null};}
}
