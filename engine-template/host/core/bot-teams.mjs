import {parsePlayers,botPlayers} from '../panel/manager.mjs';

export function autoTeamPlan(players,maximum,config) {
  if(!Number.isInteger(maximum)||maximum<1||maximum>64)throw new Error('自动补位仅适用于已验证的 64 人以内服务器');
  if(!Array.isArray(players)||players.some(p=>!p||typeof p.guid!=='string'||!['0','1','2'].includes(String(p.type))))throw new Error('玩家身份字段异常，已停止自动补位');
  const soldiers=players.filter(p=>p.guid&&String(p.type)==='0');
  const spectators=players.filter(p=>p.guid&&String(p.type)!=='0').length;
  const bots=botPlayers(players),target=Math.min(config.target,maximum);
  const desired=Math.max(0,target-config.reserve-soldiers.length);
  const teams={1:0,2:0},humanTeams={1:0,2:0};
  for(const p of bots)if(['1','2'].includes(String(p.teamId)))teams[p.teamId]++;
  for(const p of soldiers)if(['1','2'].includes(String(p.teamId)))humanTeams[p.teamId]++;
  const teamCapacity=Math.ceil(maximum/2);
  const minimum=Math.max(0,desired-Math.max(0,teamCapacity-humanTeams[2]));
  const maximumOne=Math.min(desired,Math.max(0,teamCapacity-humanTeams[1]));
  let best=null;
  for(let one=minimum;one<=maximumOne;one++) {
    const two=desired-one;
    const difference=Math.abs(humanTeams[1]+one-humanTeams[2]-two);
    const changes=Math.abs(teams[1]-one)+Math.abs(teams[2]-two);
    if(!best||difference<best.difference||(difference===best.difference&&changes<best.changes))best={one,two,difference,changes};
  }
  if(!best)throw new Error('无法在队伍容量内分配 Bot，等待玩家人数更新');
  return {humans:soldiers.length,spectators,bots:bots.length,capacity:maximum,target,reserve:config.reserve,desired,
    teams,humanTeams,teamCapacity,desiredTeams:{1:best.one,2:best.two}};
}

export function teamPlan(players,maximum,config) {
  if(!Number.isInteger(maximum)||maximum<1||maximum>64)throw new Error('服务器容量必须为 1～64');
  if(players.some(p=>typeof p.guid!=='string'||!['0','1','2'].includes(String(p.type))))throw new Error('玩家身份字段异常');
  const humans=players.filter(p=>p.guid&&String(p.type)==='0').length;
  const spectators=players.filter(p=>p.guid&&String(p.type)!=='0').length;
  const bots=botPlayers(players),requested=config.team1+config.team2;
  const desired=Math.min(requested,Math.max(0,maximum-humans));
  const one=requested?Math.floor(desired*config.team1/requested):0;
  const desiredTeams={1:one,2:desired-one};
  const teams={1:0,2:0};
  for(const p of bots)if(p.teamId==='1'||p.teamId==='2')teams[p.teamId]++;
  return {humans,spectators,bots:bots.length,capacity:maximum,requested,desired,desiredTeams,teams,limited:desired<requested};
}

export async function arrangeBotTeams(r,config,{limit=8,cancelled=()=>false}={}) {
  let moved=0;
  for(let i=0;i<limit&&!cancelled();i++) {
    const info=await r.command('serverInfo');
    if(info[0]!=='OK'||!info.includes('IN_GAME')||info[4]!=='ConquestLarge0')return {moved};
    const players=parsePlayers(await r.command('admin.listPlayers','all'));
    const plan=config.mode==='teams'?teamPlan(players,Number(info[3]),config):autoTeamPlan(players,Number(info[3]),config);
    if(plan.bots!==plan.desired)return {moved};
    const destination=[1,2].find(t=>plan.teams[t]<plan.desiredTeams[t]);
    if(!destination)return {moved};
    const source=botPlayers(players).find(p=>String(p.teamId)!==String(destination)&&
      (!['1','2'].includes(String(p.teamId))||plan.teams[p.teamId]>plan.desiredTeams[p.teamId]));
    if(!source)return {moved};
    if(!source.name||players.filter(p=>p.name===source.name).length!==1)throw new Error('Bot 名称重复或无效，已暂停自动换队');
    if(cancelled())return {moved};
    const reply=await r.command('admin.movePlayer',source.name,String(destination),'0','true');
    if(reply[0]!=='OK')throw new Error(`Bot 换队失败：${source.name} · ${reply.join(' ')}`);
    moved++;
    // Wait for roster confirmation before issuing another move for this player.
    const observed=parsePlayers(await r.command('admin.listPlayers','all')).filter(p=>p.name===source.name);
    if(observed.length!==1||observed[0].guid!==''||String(observed[0].teamId)!==String(destination))return {moved};
  }
  return {moved};
}
