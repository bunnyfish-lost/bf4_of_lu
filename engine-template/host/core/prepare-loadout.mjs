import {prepareLights} from './bot-lights.mjs';
import {prepareDamage} from './bot-damage.mjs';
import fs from 'node:fs/promises';
import {prepareProgress} from './bot-progress.mjs';
import path from 'node:path';
import {fileURLToPath} from 'node:url';
import {randomUUID,createHash} from 'node:crypto';
import {normalizeLoadout} from './loadout-policy.mjs';
const root=path.resolve(path.dirname(fileURLToPath(import.meta.url)),'..');
const read=async p=>JSON.parse((await fs.readFile(p,'utf8')).replace(/^\uFEFF/,''));
try {
 const s=await read(path.join(root,'settings/host-settings.json'));
 const game=path.join(root,'runtime/GameServer');
 if(path.resolve(s.gameServerDirectory).toLowerCase()!==game.toLowerCase())throw Error('Game path does not match installation');
 let raw;try{raw=await read(path.join(root,'settings/bot-loadout.json'));}catch(e){if(e.code!=='ENOENT')throw e;raw={version:1,weaponIds:[]};}
 const policy=normalizeLoadout(raw),session=randomUUID().replaceAll('-','');
 const vehicleLines=(await fs.readFile(path.join(game,'dinput8_bf4hook.ini'),'utf8')).match(/^[ \t]*VehicleBots[ \t]*=[^\r\n]*/gmi)||[];
 if(vehicleLines.length!==1||!/^[ \t]*VehicleBots[ \t]*=[ \t]*[01][ \t]*(?:;[^\r\n]*)?$/i.test(vehicleLines[0]))throw Error('Invalid Bot vehicle setting');
 const disableVehicles=/=[ \t]*0/.test(vehicleLines[0]);
 const hash=createHash('sha256').update(JSON.stringify({policy,disableVehicles})).digest('hex');
 await fs.writeFile(path.join(root,'runtime/bot-start.json'),JSON.stringify({phase:'loading',session,installationId:s.installationId,policyHash:hash,restricted:disableVehicles||policy.unarmed||policy.weaponIds.length>0,disableVehicles,unarmed:!!policy.unarmed}));
 await fs.writeFile(path.join(game,'bot-selection.ini'),'[BotSelection]\r\nSessionId='+session+'\r\nTrace=0\r\nUnarmed='+(policy.unarmed?'1':'0')+'\r\nDisableVehicles='+(disableVehicles?'1':'0')+'\r\nBlockedWeapons='+policy.weaponIds.join(',')+'\r\n');
 const cfg=path.join(game,'Scripts/Win32Game.cfg');
 const text=(await fs.readFile(cfg,'utf8')).replace(/^\uFEFF/,'');
 if((text.match(/^-PseudoPlayer\.SpawnCount\s+\d+/gm)||[]).length!==1)throw Error('Invalid Bot startup parameter');
 await fs.writeFile(cfg,text.replace(/^-PseudoPlayer\.SpawnCount\s+\d+/m,'-PseudoPlayer.SpawnCount 0'));
 await prepareProgress(root,session);
 await prepareDamage(root,session);
 await prepareLights(root,session);
 console.log('Bot startup configuration prepared.');
}catch(e){console.error(e.message);process.exitCode=1;}
