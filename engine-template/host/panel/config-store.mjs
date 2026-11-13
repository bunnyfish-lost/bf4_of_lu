import fs from 'node:fs/promises';
import path from 'node:path';
import { createHash, randomUUID } from 'node:crypto';
import {weaponGroups,weaponPresets,normalizeLoadout} from '../core/loadout-policy.mjs';
import { fields, mapCatalog, validateConfig } from './schema.mjs';
import {readServerPreset,renderServerPreset} from './server-presets.mjs';

const hash = value => createHash('sha256').update(value??'<missing>').digest('hex');
const escape = value => value.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
function getLine(text, key, equals = false) {
  const separator=equals?'[ \t]*=[ \t]*':'[ \t]+';
  return text.match(new RegExp('^[ \t]*'+escape(key)+separator+'([^\r\n]*)','mi'))?.[1]?.trim();
}
export function setLine(text, key, value, equals = false) {
  const newline = text.includes('\r\n') ? '\r\n' : '\n';
  const line = `${key}${equals ? '=' : ' '}${value}`;
  const pattern = new RegExp(`^[ \\t]*${escape(key)}${equals ? '[ \\t]*=[^\\r\\n]*' : '[ \\t]+[^\\r\\n]*'}`, 'gmi');
  let seen = false;
  const next = text.replace(pattern, () => { if (seen) throw new Error(`存在重复参数 ${key}，请先检查配置`); seen = true; return line; });
  return seen ? next : next + (next.endsWith('\n') ? '' : newline) + line + newline;
}
export function parseRotation(text) {
  return text.split(/\r?\n/).map(l => l.trim()).filter(l => l && !l.startsWith('#') && !l.startsWith('//')).map(l => {
    const [map, mode, rounds] = l.split(/\s+/); return { map, mode, rounds: Number(rounds) };
  });
}
export class ConfigStore {
  constructor(phaseRoot, panelRoot) { this.phaseRoot = phaseRoot; this.panelRoot = panelRoot; }
  async snapshot() {
    const settingsPath = path.join(this.phaseRoot, 'host-settings.json');
    const settingsText = await fs.readFile(settingsPath, 'utf8');
    const settings = JSON.parse(settingsText.replace(/^\uFEFF/, ''));
    const game = settings.gameServerDirectory || settings.serverDirectory;
    const files = { settings: settingsPath, startup: path.join(game, 'Instance/Admin/Startup.txt'), engine: path.join(game, 'Scripts/Win32Game.cfg'), bot: path.join(game, 'dinput8_bf4hook.ini'), maps: path.join(game, 'Instance/Admin/MapList.txt'), blaze: path.join(settings.serverDirectory, 'config.txt'), loadout:path.join(this.phaseRoot,'bot-loadout.json') };
    const texts = Object.fromEntries(await Promise.all(Object.entries(files).map(async ([k,p]) => [k, await fs.readFile(p, 'utf8').catch(e=>{if(k==='loadout'&&e.code==='ENOENT')return null;throw e;})])));
    const loadout=normalizeLoadout(texts.loadout===null?{version:1,weaponIds:[]}:JSON.parse(texts.loadout.replace(/^\uFEFF/,'')));
    const mirrors = {};
    if (path.resolve(game).toLowerCase() !== path.resolve(settings.serverDirectory).toLowerCase()) {
      for (const [key, relative] of Object.entries({startup:'Instance/Admin/Startup.txt',engine:'Scripts/Win32Game.cfg',bot:'dinput8_bf4hook.ini',maps:'Instance/Admin/MapList.txt'})) {
        const p = path.join(settings.serverDirectory, relative);
        try { mirrors[key] = { path: p, text: await fs.readFile(p, 'utf8') }; } catch(e) { if(e.code !== 'ENOENT') throw e; }
      }
    }
    const values = {};
    for (const f of fields) {
      let value = f.source === 'virtual' ? f.defaultValue : f.source === 'settings' ? settings[f.key] : getLine(texts[f.source], f.key, f.source === 'bot');
      if (value === undefined && f.defaultValue !== undefined) value=f.defaultValue;
      if (value === undefined) throw new Error(`找不到参数 ${f.key}，未自动填写默认值`);
      if (f.type === 'boolean') {value = ['true','1'].includes(String(value).toLowerCase());if(f.inverted)value=!value;}
      else if (f.type === 'number' || (f.type === 'select' && f.options.every(o=>typeof o.value==='number'))) value = Number(value);
      else value = String(value).replace(/^"(.*)"$/, '$1');
      values[f.id] = value;
    }
    values.serverPreset=readServerPreset(texts.startup);
    const ammoMigration=values.infiniteAmmo&&values.infiniteMags;
    values.ammoMode=values.infiniteAmmo?2:values.infiniteMags?1:0;
    values.infiniteMags=values.ammoMode===1;
    const revision = hash(JSON.stringify({texts, mirrors}));
    return { settings, files, texts, mirrors, values, loadout, revision, ammoMigration, rotation: parseRotation(texts.maps), unlockMode: getLine(texts.startup, 'vars.unlockMode') };
  }
  async publicConfig() {
    const s = await this.snapshot();
    return { values: s.values, rotation: s.rotation, revision: s.revision, fields, mapCatalog, unlockMode: s.unlockMode, ammoMigration:s.ammoMigration, paths: {game:s.settings.gameServerDirectory,blaze:s.settings.serverDirectory}, note:'参数保存到文件，重启后应用；Procon 的实时修改不一定同步到文件。' };
  }
  async save(input) {
    input={...input,values:{...input.values}};
    validateConfig(input.values, input.rotation);
    input.values.infiniteAmmo=input.values.ammoMode===2;input.values.infiniteMags=input.values.ammoMode===1;
    const s = await this.snapshot();
    if (s.settings.sharedBlaze && input.values.advertiseAddress !== s.settings.advertiseAddress) throw new Error('此实例共用登录服务，请统一修改所有实例的 Blaze 地址');
    if (input.revision !== s.revision) throw new Error('配置已被其他程序修改，请重新读取后再保存');
    const loadout=normalizeLoadout(input.loadout??s.loadout);
    const v = input.values;
    if(loadout.unarmed&&v.botVehicles)throw new Error('空手人机模式需关闭人机载具；退出空手预设后可重新开启');
    const settings = {...s.settings, serverName:v.serverName.trim(),advertiseAddress:v.advertiseAddress,gamePort:v.gamePort,rconPort:v.rconPort,maxSoldiers:v.maxPlayers,initialBots:v.initialBots};
    if(v.advertiseAddress!==s.settings.advertiseAddress&&!settings.legacyProfileAddress)settings.legacyProfileAddress=s.settings.advertiseAddress;
    const transform = (key, text) => {
      if(key==='loadout')return JSON.stringify(loadout,null,2)+'\n';
      for (const f of fields.filter(f=>f.source===key)) {
        let value = f.inverted ? !v[f.id] : v[f.id];
        if (f.type==='boolean') value = key==='bot' ? (value?'1':'0') : String(value);
        text = setLine(text,f.key,value,key==='bot');
      }
      if (key==='startup') {
        text=setLine(text,'vars.OutHighFrequency',v.tickRate);
        text=setLine(text,'vars.teamKillCountForKick',5);
        text=setLine(text,'vars.teamKillKickForBan',3);
        text=setLine(text,'vars.teamKillValueForKick',0);
        text=setLine(text,'vars.teamKillValueIncrease',1);
        text=setLine(text,'vars.teamKillValueDecreasePerSecond','0.05');
        text = setLine(text,'vars.serverType','UNRANKED');

        text = setLine(text,'vars.serverName',`"${settings.serverName}"`);
        // Keep both unlock mechanisms consistent when this field is edited.
        text = setLine(text,'vars.unlockMode',v.allUnlocks?'all':'stats');
        text=renderServerPreset(text,v.serverPreset);
      }
      if (key==='maps') text = input.rotation.map(r=>`${r.map} ${r.mode} ${r.rounds}`).join('\r\n')+'\r\n';
      if (key==='engine') {
        text = setLine(text,'-PseudoPlayer.SpawnCount',v.initialBots);
        text = setLine(text,'-Game.MaxPlayerCount',v.maxPlayers+6);
        text = setLine(text,'-Network.MaxClientCount',v.maxPlayers+6);
        text = setLine(text,'-Server.OutgoingHighFrequencyRconCap',v.tickRate);
        text = setLine(text,'-Server.OutgoingHighFrequency',v.tickRate);
        text = setLine(text,'-Server.HighFrequencyUpdateEnabled','true');
      }
      if (key==='blaze') {text = setLine(text,'ServerIP',v.advertiseAddress,true);text = setLine(text,'ListenAddress',v.advertiseAddress==='127.0.0.1'?'127.0.0.1':'0.0.0.0',true);}
      return text;
    };
    const changes = [];
    for (const [key,p] of Object.entries(s.files)) {
      const next = key==='settings' ? JSON.stringify(settings,null,2)+'\n' : transform(key,s.texts[key]);
      if(next!==s.texts[key]) changes.push({path:p,before:s.texts[key],after:next});
    }
    for(const [key,m] of Object.entries(s.mirrors)) { const after=transform(key,m.text); if(after!==m.text) changes.push({path:m.path,before:m.text,after}); }
    if(!changes.length) return {changed:0,backup:null,restartRequired:false};
    const stamp = new Date().toISOString().replace(/[:.]/g,'-')+'-'+randomUUID().slice(0,8);
    const backup = path.join(this.panelRoot,'backups',stamp);
    await fs.mkdir(backup,{recursive:true});
    for(let i=0;i<changes.length;i++) if(changes[i].before!==null)await fs.writeFile(path.join(backup,`${i}.bak`),changes[i].before,'utf8');
    await fs.writeFile(path.join(backup,'manifest.json'),JSON.stringify(changes.map((c,i)=>({path:c.path,file:c.before===null?null:`${i}.bak`,beforeHash:hash(c.before),afterHash:hash(c.after)})),null,2));
    const written=[];
    try {
      for(const c of changes) {
        if(await fs.readFile(c.path,'utf8').catch(e=>{if(e.code==='ENOENT')return null;throw e;})!==c.before) throw new Error('保存过程中配置发生变化，已停止写入');
        await atomicWrite(c.path,c.after); written.push(c);
      }
    } catch(e) {
      const failures=[];
      for(const c of written.reverse()) try { if(await fs.readFile(c.path,'utf8')===c.after) {if(c.before===null)await fs.unlink(c.path);else await atomicWrite(c.path,c.before);} else failures.push(c.path); } catch {failures.push(c.path);}
      if(failures.length) throw new Error(`${e.message}；部分文件需从备份恢复：${backup}`);
      throw e;
    }
    return {changed:changes.length,backup,restartRequired:true};
  }
  async publicLoadout(){const s=await this.snapshot();return {policy:s.loadout,weaponGroups,weaponPresets,botVehicles:s.values.botVehicles,revision:s.revision};}
  async saveLoadout(input){
    if(typeof input.botVehicles!=='boolean')throw Error('人机载具开关格式错误');
    const policy=normalizeLoadout(input.policy);
    const s=await this.snapshot();
    return this.save({revision:input.revision,values:{...s.values,botVehicles:input.botVehicles},rotation:s.rotation,loadout:policy});
  }
  async secrets() {
    const settings=JSON.parse((await fs.readFile(path.join(this.phaseRoot,'host-settings.json'),'utf8')).replace(/^\uFEFF/,''));
    const secret=JSON.parse((await fs.readFile(path.join(path.dirname(settings.serverDirectory),'private.json'),'utf8')).replace(/^\uFEFF/,''));
    return {settings,password:secret.rconPassword};
  }
}
async function atomicWrite(p,text) {
  const tmp=p+'.panel-'+randomUUID()+'.tmp';
  try {await fs.writeFile(tmp,text,'utf8');await fs.rename(tmp,p);} finally {await fs.rm(tmp,{force:true}).catch(()=>{});}
}
