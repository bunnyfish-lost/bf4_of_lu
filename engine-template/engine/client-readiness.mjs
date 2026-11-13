export class ClientReadyGate {
 constructor(){this.identity='';this.firstWindowAt=0;}
 observe(value,now=Date.now()){
  const live=value&&Number.isInteger(value.pid)&&value.pid>0&&value.startTicks;
  const identity=live?value.pid+':'+value.startTicks:'';
  if(identity!==this.identity){this.identity=identity;this.firstWindowAt=0;}
  const window=live&&value.hasWindow===true&&value.responding===true&&value.ageSeconds>=8;
  if(!window){this.firstWindowAt=0;return {pid:live?value.pid:0,ready:false,state:!live?'waiting-client':value.hasWindow?'waiting-client-response':'waiting-client-window'};}
  if(!this.firstWindowAt)this.firstWindowAt=now;
  return {pid:value.pid,ready:now-this.firstWindowAt>=1800,state:now-this.firstWindowAt>=1800?'running':'waiting-client-window'};
 }
}
