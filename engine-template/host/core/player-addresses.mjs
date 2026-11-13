import fs from 'node:fs/promises';
import path from 'node:path';
import {isIP} from 'node:net';
import {StringDecoder} from 'node:string_decoder';

export const isRadminAddress=address=>isIP(address||'')===4&&address.startsWith('26.');

export class AddressLogParser {
 constructor(minTime=0){this.minTime=minTime;this.addresses=new Map();this.block=null;this.partial='';this.decoder=new StringDecoder('utf8');}
 feed(bytes){
  const lines=(this.partial+this.decoder.write(bytes)).split(/\r?\n/);this.partial=lines.pop();
  if(this.partial.length>16384){this.partial='';this.block=null;}
  for(const line of lines){
   const header=/^\[UTC (\d{2})\/(\d{2})\/(\d{4}) (\d{2}:\d{2}:\d{2}\.\d{3})\] BlazeSDK\(0\): "GameLoop": Info: \[CANA\] UserInfoT:\s*$/.exec(line);
   if(header){const time=Date.parse(`${header[3]}-${header[1]}-${header[2]}T${header[4]}Z`);this.block=time>=this.minTime?{name:null}:null;continue;}
   if(line.startsWith('[UTC ')){this.block=null;continue;}
   if(!this.block)continue;
   const name=/^\s{4}userInfo->strName = \((.{1,128}), , , \)\s*$/.exec(line);
   if(name&&!/[\x00-\x1f\x7f]/.test(name[1])){this.block.name=name[1];this.addresses.delete(name[1]);continue;}
   const address=/^\s{4}userInfo->uAddr = ([\d.]+)\s*$/.exec(line);
   if(address){if(this.block.name&&isRadminAddress(address[1]))this.addresses.set(this.block.name,address[1]);this.block=null;}
  }
 }
}

export class PlayerAddressReader {
 constructor(root){this.root=root;this.gameKey='';this.fileKey='';this.offset=0;this.parser=new AddressLogParser();this.pending=null;}
 async enrich(players,settings){
  if(!isRadminAddress(settings.advertiseAddress))return players;
  try {
   if(!this.pending)this.pending=this.read(settings).finally(()=>{this.pending=null;});
   await this.pending;
   const counts=new Map();for(const p of players)counts.set(p.name,(counts.get(p.name)||0)+1);
   const names=new Set(players.filter(p=>p.guid).map(p=>p.name));
   for(const name of this.parser.addresses.keys())if(!names.has(name))this.parser.addresses.delete(name);
   return players.map(p=>p.guid?{...p,radminIp:counts.get(p.name)===1?this.parser.addresses.get(p.name)||null:null}:p);
  } catch {return players.map(p=>p.guid?{...p,radminIp:null}:p);}
 }
 async read(settings){
  const state=JSON.parse(await fs.readFile(path.join(this.root,'runtime/processes.json'),'utf8'));
  const games=state.processes.filter(p=>p.role==='game');if(games.length!==1)throw Error('No unique tracked game');
  const gameKey=JSON.stringify([games[0].pid,games[0].startTicks,games[0].path]);
  const minTime=Number((BigInt(games[0].startTicks)-621355968000000000n)/10000n);
  if(gameKey!==this.gameKey){this.gameKey=gameKey;this.fileKey='';this.offset=0;this.parser=new AddressLogParser(minTime);}
  const logRoot=path.join(settings.gameServerDirectory,'Instance/Logs');
  const files=(await fs.readdir(logRoot)).filter(name=>/^RuntimeLog_.+_server\.log$/.test(name));
  if(files.length!==1)throw Error('No unique active game log');
  const file=path.join(logRoot,files[0]);const h=await fs.open(file,'r');
  try {
   const st=await h.stat(),fileKey=JSON.stringify([file,st.ino,st.birthtimeMs]);
   let skipFirst=false;
   if(fileKey!==this.fileKey||st.size<this.offset){
    this.fileKey=fileKey;this.offset=Math.max(0,st.size-4*1024*1024);skipFirst=this.offset>0;
    const addresses=this.parser.addresses;this.parser=new AddressLogParser(minTime);this.parser.addresses=addresses;
   }
   // A bounded tail and incremental reads keep polling independent of total log size.
   const length=Math.min(st.size-this.offset,4*1024*1024);if(length<=0)return;
   const bytes=Buffer.alloc(length);const {bytesRead}=await h.read(bytes,0,length,this.offset);this.offset+=bytesRead;
   let content=bytes.subarray(0,bytesRead);
   if(skipFirst){const newline=content.indexOf(10);content=newline<0?Buffer.alloc(0):content.subarray(newline+1);}
   this.parser.feed(content);
  } finally {await h.close();}
 }
}
