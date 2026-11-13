export class RconPool {
  constructor(connect,{idleMs=30000,maxIdle=2}={}){this.connect=connect;this.idleMs=idleMs;this.maxIdle=maxIdle;this.idle=[];this.all=new Set();this.closed=false;this.key=null;}
  discard(entry){clearTimeout(entry.timer);this.idle=this.idle.filter(e=>e!==entry);this.all.delete(entry);entry.connection.close();}
  async run(key,create,fn){
    if(this.closed)throw Error('管理连接已关闭');
    if(this.key!==key){this.key=key;for(const entry of [...this.idle])this.discard(entry);}
    let entry;
    while(this.idle.length&&!entry){const candidate=this.idle.pop();clearTimeout(candidate.timer);if(candidate.key===key&&!candidate.connection.socket?.destroyed)entry=candidate;else this.discard(candidate);}
    if(!entry){const connection=await create(this.connect);entry={connection,key,timer:null};if(this.closed){connection.close();throw Error('管理连接已关闭');}this.all.add(entry);}
    try{
      const value=await fn(entry.connection);
      if(this.closed||this.key!==key||entry.connection.socket?.destroyed||this.idle.length>=this.maxIdle)this.discard(entry);
      else {this.idle.push(entry);entry.timer=setTimeout(()=>this.discard(entry),this.idleMs);entry.timer.unref?.();}
      return value;
    }catch(error){this.discard(entry);throw error;}
  }
  close(){this.closed=true;for(const entry of [...this.all])this.discard(entry);}
}
