import {readFileSync} from 'node:fs';
const number = (id, label, group, source, key, min, max, unit = '') => ({ id, label, group, source, key, type: 'number', min, max, unit });
const toggle = (id, label, group, source, key) => ({ id, label, group, source, key, type: 'boolean' });
const factions = [{value:0,label:'美国'},{value:1,label:'俄罗斯'},{value:2,label:'中国'}];
export const fields = [
  {"id": "serverPreset", "label": "服务端规则预设", "group": "服务端预设", "source": "virtual", "type": "select", "options": [{"value": "CUSTOM", "label": "自定义（CUSTOM）"}, {"value": "NORMAL", "label": "普通（NORMAL）"}, {"value": "HARDCORE", "label": "硬核（HARDCORE）"}], "defaultValue": "CUSTOM", "hint": "普通／硬核使用游戏预设规则；自定义使用下方细项设置。保存后重启游戏服生效。"},
  { id: 'serverName', label: '服务器名称', group: '连接与人数', source: 'settings', key: 'serverName', type: 'text', maxLength: 60 },
  { id: 'advertiseAddress', label: '主机 IPv4', group: '连接与人数', source: 'settings', key: 'advertiseAddress', type: 'ipv4', hint: '仅本机游玩填 127.0.0.1；联机填主机组网网卡或局域网 IP。修改后需重启全部服务。' },
  number('gamePort', '游戏端口 UDP', '连接与人数', 'settings', 'gamePort', 1024, 65535),
  number('rconPort', '管理端口 TCP', '连接与人数', 'settings', 'rconPort', 1024, 65535),
  {...number('maxPlayers', '对战人数上限', '连接与人数', 'startup', 'vars.maxPlayers', 64, 64),locked:true,hint:'固定为 64 人，暂时不支持调整。'},
  number('initialBots', '开服 Bot 数量', '连接与人数', 'settings', 'initialBots', 0, 63),
  number('maxSpectators', '观战席位', '连接与人数', 'startup', 'vars.maxSpectators', 0, 4),
  number('startPlayers', '开局人数阈值', '连接与人数', 'startup', 'vars.roundStartPlayerCount', 0, 64),
  toggle('skipPreround', '跳过预备阶段', '连接与人数', 'engine', '-Game.DisablePreRound'),
  {id:'tickRate',label:'服务器刷新率',group:'连接与人数',source:'engine',key:'-MaxTickRate',type:'select',options:[{value:30,label:'30 Hz（默认）'},{value:60,label:'60 Hz'}],defaultValue:30,hint:'保存并重启后生效。60 Hz 会增加主机 CPU 和网络负载。'},
  {id:'team1Faction',label:'队伍 1 阵营',group:'阵营',source:'startup',key:'vars.teamFactionOverride 1',type:'select',options:factions,defaultValue:0,hint:'保存并重启后生效。'},
  {id:'team2Faction',label:'队伍 2 阵营',group:'阵营',source:'startup',key:'vars.teamFactionOverride 2',type:'select',options:factions,defaultValue:2,hint:'保存并重启后生效。'},
  number('tickets', '兵力值倍率', '对局规则', 'startup', 'vars.gameModeCounter', 1, 500, '%'),
  number('roundTime', '回合时间倍率', '对局规则', 'startup', 'vars.roundTimeLimit', 0, 900, '%'),
  number('health', '士兵血量倍率', '对局规则', 'startup', 'vars.soldierHealth', 1, 300, '%'),
  number('damage', '全局子弹伤害', '对局规则', 'startup', 'vars.bulletDamage', 1, 300, '%'),
  number('respawn', '玩家重生时间倍率', '对局规则', 'startup', 'vars.playerRespawnTime', 1, 300, '%'),
  number('vehicleDelay', '载具重生时间倍率', '对局规则', 'startup', 'vars.vehicleSpawnDelay', 1, 500, '%'),
  toggle('friendlyFire', '友军伤害', '对局规则', 'startup', 'vars.friendlyFire'),
  {...toggle('teamKillBan', '友军击杀封禁', '对局规则', 'engine', '-PointSystem.IgnoreTeamKill'),inverted:true,defaultValue:'false',hint:'开启后：累计击杀 5 名队友踢出，因误杀被踢 3 次封禁。关闭后不因误杀自动踢出或封禁，仍保留友军伤害。保存并重启后生效；已有封禁需另行解除。'},
  {...toggle('infiniteMags', '旧版无限备弹', '无限火力', 'engine', '-Game.HasUnlimitedMags'),defaultValue:'false',hidden:true},
  {...toggle('infiniteAmmo', '无限火力（免换弹）', '弹药', 'engine', '-Game.HasUnlimitedAmmo'),defaultValue:'false',hidden:true},
  {id:'ammoMode',label:'弹药模式',group:'弹药',source:'virtual',type:'select',options:[{value:0,label:'普通弹药（默认）'},{value:1,label:'无限备弹（需要换弹）'},{value:2,label:'无限火力（免换弹）'}],defaultValue:0,hint:'全服生效，保存并重启后应用。无限备弹仍需换弹；该模式的满员人机表现待 beta 实测。Bot 独立备弹设置不受影响。'},
  toggle('regen', '自动回血', '对局规则', 'startup', 'vars.regenerateHealth'),
  toggle('vehicles', '生成载具', '对局规则', 'startup', 'vars.vehicleSpawnAllowed'),
  toggle('autoBalance', '自动平衡队伍', '对局规则', 'startup', 'vars.autoBalance'),
  toggle('commander', '允许指挥官', '对局规则', 'startup', 'vars.commander'),
  toggle('killCam', '击杀镜头', '显示与解锁', 'startup', 'vars.killCam'),
  toggle('miniMap', '小地图', '显示与解锁', 'startup', 'vars.miniMap'),
  toggle('hud', 'HUD', '显示与解锁', 'startup', 'vars.hud'),
  toggle('spotting', '3D 标记', '显示与解锁', 'startup', 'vars.3dSpotting'),
  {...toggle('allUnlocks', '武器全解', '显示与解锁', 'engine', '-SyncedBFSettings.AllUnlocksUnlocked'),hint:'开启后在本服务器开放武器与配件；关闭后按玩家档案解锁。保存并重启游戏服生效，不改变等级和经验。'},
  {...toggle('botVehicles', '人机使用载具', 'Bot 基础开关', 'bot', 'VehicleBots'),hint:'关闭后人机以步兵部署，不选择载具；真人仍可使用。保存并重启后生效。'},
  toggle('botRevive', 'Bot 医疗兵救援', 'Bot 基础开关', 'bot', 'BotMedicRevive'),
  toggle('botReserve', 'Bot 无限备弹（保留换弹）', 'Bot 基础开关', 'bot', 'BotInfiniteReserve'),
  toggle('botGrenades', 'Bot 无限手雷', 'Bot 基础开关', 'bot', 'BotInfiniteGrenades'),
  toggle('botChat', 'Bot 对局聊天', 'Bot 基础开关', 'bot', 'BotMatchChat'),
  toggle('botRoundChat', 'Bot 回合结束聊天', 'Bot 基础开关', 'bot', 'BotRoundChat'),
  {...toggle('botRandomRank','人机随机等级','Bot 基础开关','bot','BotRandomRank'),defaultValue:true,hint:'随机显示人机等级，不改变真人档案。'},
  {id:'externalIp',label:'上报地址（留空自动）',group:'高级连接',source:'bot',key:'ExternalIP',type:'ipv4optional',defaultValue:'',hint:'本机离线保留空白。'},
  {...number('externalPort','上报端口（0自动）','高级连接','bot','ExternalPort',0,65535),defaultValue:0}
];
export const ruleFields=fields.filter(f=>['tickets','roundTime','health','damage','respawn','vehicleDelay'].includes(f.id));
for(const f of ruleFields)f.hint='100% 为默认值；保存并重启后，可在下方核对实际生效值。';
export const mapCatalog=JSON.parse(readFileSync(new URL('../../engine/map-capabilities.json',import.meta.url),'utf8'));
export function validMapMode(map,mode){return mapCatalog.some(m=>m.id===map&&m.modes.includes(mode));}
export function validateConfig(values, rotation) {
  if (!values || typeof values !== 'object' || Array.isArray(values)) throw new Error('配置格式错误');
  const known = new Set(fields.map(f => f.id));
  for (const key of Object.keys(values)) if (!known.has(key)) throw new Error(`未知参数：${key}`);
  for (const f of fields) {
    const value = values[f.id];
    if(f.locked&&value!==64)throw new Error('对战人数上限固定为 64 人，暂时不支持调整');
    if (f.type === 'boolean' && typeof value !== 'boolean') throw new Error(`${f.label}必须是开关值`);
    if (f.type === 'select' && !f.options.some(o=>o.value===value)) throw new Error(`${f.label}请选择列表中的选项`);
    if (f.type === 'number' && (!Number.isInteger(value) || value < f.min || value > f.max)) throw new Error(`${f.label}请输入 ${f.min}～${f.max} 的整数`);
    if (f.type === 'text' && (typeof value !== 'string' || !value.trim() || value.length > f.maxLength || /["\\\r\n\x00-\x1f]/.test(value))) throw new Error(`${f.label}不能为空，且不能含引号、反斜杠或控制字符`);
    if(f.type==='ipv4optional'&&(typeof value!=='string'||value!==''&&(!/^(?:\d{1,3}\.){3}\d{1,3}$/.test(value)||value.split('.').some(n=>Number(n)>255))))throw new Error('上报地址请留空或填写 IPv4');
    if (f.type === 'ipv4' && (typeof value !== 'string' || !/^(?:\d{1,3}\.){3}\d{1,3}$/.test(value) || value.split('.').some(n => Number(n) > 255) || ['0.0.0.0', '255.255.255.255'].includes(value))) throw new Error('请输入有效的主机 IPv4');
  }
  if (values.startPlayers > values.maxPlayers) throw new Error('开局人数不能超过对战人数上限');
  if (values.initialBots >= values.maxPlayers) throw new Error('Bot 数量须小于对战人数上限，至少保留一个玩家席位');
  if ([80,443,10040,10041,4217,42127,42230].includes(values.rconPort)) throw new Error('管理端口与 Blaze 服务端口冲突');
  if (!Array.isArray(rotation) || !rotation.length || rotation.length > 32) throw new Error('地图轮换需要 1～32 项');
  for (const row of rotation) {
    if (!row || !validMapMode(row.map,row.mode) || !Number.isInteger(row.rounds) || row.rounds < 1 || row.rounds > 10) throw new Error('请选择 v2 支持的地图与模式组合，回合数为 1～10');
  }
  return values;
}
