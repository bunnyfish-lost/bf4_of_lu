export const liveRuleCommands={tickets:'vars.gameModeCounter',roundTime:'vars.roundTimeLimit',health:'vars.soldierHealth',damage:'vars.bulletDamage',respawn:'vars.playerRespawnTime',vehicleDelay:'vars.vehicleSpawnDelay'};
export async function readLiveRules(r){
 const result={values:{},errors:{},serverType:null,preset:null};
 for(const [id,cmd]of Object.entries({serverType:'vars.serverType',preset:'vars.preset',...liveRuleCommands})){
  const reply=await r.command(cmd);
  if(reply[0]!=='OK'){result.errors[id]=reply.join(' ');continue;}
  if(id==='serverType'||id==='preset')result[id]=reply[1];
  else if(reply[1]!==undefined&&Number.isFinite(Number(reply[1])))result.values[id]=Number(reply[1]);
  else result.errors[id]='InvalidResponse';
 }
 return result;
}
