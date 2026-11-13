import {Rcon,encodePacket,decodePacket} from './rcon.mjs';
export function vehicleChatPlayer(words){
 if(words.length!==4||words[0]!=='player.onChat'||words[2].trim().toLowerCase()!=='panzer'||words[3]!=='all')return null;
 const name=words[1];return typeof name==='string'&&name.trim()&&name!=='Server'&&Buffer.byteLength(name,'utf8')<=63&&!/[\r\n\0]/.test(name)?name:null;
}
export class VehicleChat {
 constructor(secrets,vehicles,{connect=Rcon.connect,report=()=>{},now=Date.now}={}){this.secrets=secrets;this.vehicles=vehicles;this.connect=connect;this.report=report;this.now=now;this.stopped=true;this.generation=0;this.last=0;this.pending=null;}
 start(){if(!this.stopped)return;this.stopped=false;this.run(++this.generation);}
 accept(words,generation=this.generation){const player=vehicleChatPlayer(words);if(!player||this.stopped||generation!==this.generation||this.pending||this.now()-this.last<5000)return;this.last=this.now();this.pending=this.vehicles.spawnM1(player,()=>!this.stopped&&generation===this.generation).catch(e=>this.report(e.message)).finally(()=>{this.pending=null;});}
 async run(generation){let r;try{
 const {settings,password}=await this.secrets();if(this.stopped||generation!==this.generation)return;
 r=await this.connect('127.0.0.1',settings.rconPort);if(this.stopped||generation!==this.generation){r.close();return;}this.connection=r;await r.login(password);
 let buffer=Buffer.alloc(0);const seen=new Set();r.socket.on('data',chunk=>{try{buffer=Buffer.concat([buffer,chunk]);while(buffer.length>=12){const size=buffer.readUInt32LE(4);if(size<12||size>1048576)throw Error('Vehicle event length invalid');if(buffer.length<size)break;const packet=decodePacket(buffer.subarray(0,size));buffer=buffer.subarray(size);if(!packet.serverOrigin||packet.response)continue;r.socket.write(encodePacket((packet.sequence|0xc0000000)>>>0,['OK']));if(seen.has(packet.sequence))continue;seen.add(packet.sequence);if(seen.size>4096)seen.delete(seen.values().next().value);this.accept(packet.words,generation);}}catch(e){r.close();}});
 if(this.stopped||generation!==this.generation)return;const answer=await r.command('admin.eventsEnabled','true');if(answer[0]!=='OK')throw Error('Vehicle events unavailable');await new Promise(resolve=>{if(r.socket.destroyed)resolve();else r.socket.once('close',resolve);});
 }catch{}finally{r?.close();if(this.connection===r)this.connection=null;if(!this.stopped&&generation===this.generation)this.timer=setTimeout(()=>this.run(generation),2500);}}
 async close(){this.stopped=true;++this.generation;clearTimeout(this.timer);this.connection?.close();await this.pending;}
}
