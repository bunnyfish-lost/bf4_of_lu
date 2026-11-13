import {randomUUID} from 'node:crypto';
import {Rcon, encodePacket, decodePacket} from './rcon.mjs';

export function parseRoster(reply) {
  if(reply[0]!=='OK') throw Error('无法读取音效玩家列表');
  const width=Number(reply[1]), count=Number(reply[2+width]);
  if(!Number.isInteger(width)||width<1||width>64||!Number.isInteger(count)||count<0||count>256||reply.length!==3+width+width*count) throw Error('音效玩家列表格式错误');
  const keys=reply.slice(2,2+width);
  return Array.from({length:count},(_,i)=>Object.fromEntries(keys.map((k,j)=>[k,reply[3+width+i*width+j]])));
}
export function ownKill(words, roster) {
  if(words[0]!=='player.onKill'||words.length!==5||!words[1]||words[1]===words[2]||['Death','Suicide','AdminKill'].includes(words[3])) return null;
  const killers=roster.filter(p=>p.name===words[1]), victims=roster.filter(p=>p.name===words[2]);
  if(killers.length!==1||victims.length!==1||!killers[0].guid) return null;
  const killer=killers[0], victim=victims[0];
  if(!['1','2'].includes(killer.teamId)||!['1','2'].includes(victim.teamId)||killer.teamId===victim.teamId) return null;
  return {player:killer.name,guid:killer.guid,headshot:words[4]==='true'};
}

export function ownDeath(words,roster){
  if(words[0]!=='player.onKill'||words.length!==5||!words[2])return null;
  const victims=roster.filter(p=>p.name===words[2]);
  if(victims.length!==1||!victims[0].guid||!['1','2'].includes(victims[0].teamId))return null;
  return {player:victims[0].name,guid:victims[0].guid};
}

export class LifeKills {
  constructor(){this.players=new Map();}
  clear(){this.players.clear();}
  reconcile(roster){for(const [name,life] of this.players)if(!roster.some(p=>p.name===name&&p.guid===life.guid))this.players.delete(name);}
  accept(words,roster){
    if(['server.onLevelLoaded','server.onRoundOver'].includes(words[0]))this.clear();
    if(['player.onLeave','player.onJoin'].includes(words[0]))this.players.delete(words[1]);
    if(words[0]!=='player.onKill'||words.length!==5)return null;
    this.players.delete(words[2]);
    const kill=ownKill(words,roster);if(!kill)return null;
    const old=this.players.get(kill.player);
    const streak=old?.guid===kill.guid?Math.min(old.streak+1,1000000):1;
    this.players.set(kill.player,{guid:kill.guid,streak});
    return {...kill,streak};
  }
}

export class KillEvents {
  constructor(secrets, receive, connect=Rcon.connect) {this.secrets=secrets;this.receive=receive;this.connect=connect;this.state='未启用';this.stopped=true;this.connection=null;this.roster=[];this.timer=null;this.events=0;}
  start() {if(!this.stopped)return;this.stopped=false;this.generation=(this.generation||0)+1;this.run(this.generation);}
  async run(generation) {
    if(this.stopped||generation!==this.generation)return;
    let r;
    try {
      this.state='正在连接游戏服';
      const {settings,password}=await this.secrets();
      r=await this.connect('127.0.0.1',settings.rconPort);
      if(this.stopped||generation!==this.generation){r.close();return;}
      this.connection=r;
      await r.login(password);
      this.roster=parseRoster(await r.command('admin.listPlayers','all'));this.rosterTime=Date.now();
      let buffer=Buffer.alloc(0);const seen=new Set(),session=randomUUID(),lives=new LifeKills();this.lives=lives;
      r.socket.on('data',chunk=>{
        try {
          buffer=Buffer.concat([buffer,chunk]);
          while(buffer.length>=12){
            const size=buffer.readUInt32LE(4);if(size<12||size>1024*1024)throw Error('事件数据长度异常');if(buffer.length<size)break;
            const p=decodePacket(buffer.subarray(0,size));buffer=buffer.subarray(size);
            if(!p.serverOrigin||p.response)continue;
            r.socket.write(encodePacket((p.sequence|0xc0000000)>>>0,['OK']));
            if(seen.has(p.sequence))continue;seen.add(p.sequence);if(seen.size>4096)seen.delete(seen.values().next().value);
            if(['player.onJoin','player.onLeave','server.onLevelLoaded'].includes(p.words[0])){this.roster=[];this.refreshRoster(r);}
            if(p.words[0]==='player.onTeamChange'){const player=this.roster.find(x=>x.name===p.words[1]);if(player)player.teamId=p.words[2];}
            if(p.words[0]==='player.onSpawn'){const player=this.roster.find(x=>x.name===p.words[1]);if(player)player.teamId=p.words[2];else this.refreshRoster(r);}
            if(Date.now()-this.rosterTime>=12000)lives.clear();
            const kill=lives.accept(p.words,Date.now()-this.rosterTime<12000?this.roster:[]);
            if(kill&&!this.stopped&&generation===this.generation){this.events++;this.receive({...kill,type:'kill',id:session+':'+p.sequence,at:Date.now()});}
            const death=Date.now()-this.rosterTime<12000?ownDeath(p.words,this.roster):null;
            if(death&&!this.stopped&&generation===this.generation)this.receive({...death,type:'death',id:session+':'+p.sequence+':death',at:Date.now()});
          }
        }catch(e){this.state=e.message;r.close();}
      });
      const enabled=await r.command('admin.eventsEnabled','true');if(enabled[0]!=='OK')throw Error('游戏服未接受击杀事件订阅');
      this.state='已连接';
      const refresh=setInterval(()=>this.refreshRoster(r),5000);
      try{await new Promise(resolve=>{if(r.socket.destroyed)resolve();else r.socket.once('close',resolve);});}finally{clearInterval(refresh);}
    }catch(e){this.state=e.message;}
    finally{r?.close();if(this.connection===r)this.connection=null;if(generation===this.generation)this.roster=[];if(!this.stopped&&generation===this.generation){if(this.state==='已连接')this.state='连接中断，正在重连';this.timer=setTimeout(()=>this.run(generation),2500);}}
  }
  async refreshRoster(r){if(this.refreshing||r.socket.destroyed)return;this.refreshing=true;try{const rows=parseRoster(await r.command('admin.listPlayers','all'));if(this.connection===r){this.roster=rows;this.rosterTime=Date.now();this.lives?.reconcile(rows);}}catch(e){this.state=e.message;r.close();}finally{this.refreshing=false;}}
  stop(){this.stopped=true;this.generation=(this.generation||0)+1;clearTimeout(this.timer);this.connection?.close();this.roster=[];this.state='未启用';}
}
