import fs from 'node:fs/promises';
import path from 'node:path';
import {fileURLToPath,pathToFileURL} from 'node:url';
import {currentStorage} from './player-profiles.mjs';
const HEADER=128, RECORD=128, COUNT=255, FIRST=133700, SIZE=HEADER+RECORD*COUNT;
export function checksum(b){let h=2166136261;for(let i=0;i<b.length;i++){if(i>=36&&i<40)continue;h=Math.imul(h^b[i],16777619)>>>0;}return h;}
const str=(b,a,n)=>b.subarray(a,a+n).toString('utf8').split('\0')[0];
const integer=(v,label)=>{if(!Number.isSafeInteger(v)||v<0)throw Error('Invalid Bot '+label);return v;};
export function decodeProgress(b,expected={}){
 if(b.length!==SIZE||str(b,0,8)!=='BF4BOT1'||b.readUInt32LE(8)!==1||b.readUInt32LE(12)!==COUNT||checksum(b)!==b.readUInt32LE(36))throw Error('Bot progress file is damaged; restore its backup instead of resetting it');
 const installationId=str(b,40,40),session=str(b,80,40);
 if(!/^[a-f0-9-]{36}$/.test(installationId)||!(/^[a-f0-9]{32}$/.test(session)))throw Error('Invalid Bot progress identity');
 if(expected.installationId&&expected.installationId!==installationId)throw Error('Bot progress installation mismatch');
 if(expected.session&&expected.session!==session)throw Error('Bot progress session mismatch');
 const stamp=Number(b.readBigUInt64LE(24));const updatedAt=stamp?new Date(stamp/10000-11644473600000).toISOString():null;
 const rows=[];for(let i=0;i<COUNT;i++){const p=HEADER+i*RECORD,id=b.readUInt32LE(p),rank=b.readUInt32LE(p+4);if(id!==FIRST+i||rank>140)throw Error('Invalid Bot record');const kills=integer(Number(b.readBigUInt64LE(p+8)),'kills'),deaths=integer(Number(b.readBigUInt64LE(p+16)),'deaths'),experience=integer(Number(b.readBigUInt64LE(p+24)),'experience');rows.push({id,name:str(b,p+32,64)||'Bot_'+id,isBot:true,rank,kills,deaths,experience,active:b.readUInt32LE(p+96)===1,updatedAt,source:'bot-progress'});}
 return {version:1,installationId,session,sequence:Number(b.readBigUInt64LE(16)),pid:b.readUInt32LE(32),updatedAt,players:rows};
}
export function encodeProgress({installationId,session,players=[],sequence=0}){
 if(!/^[a-f0-9-]{36}$/.test(installationId)||!/^[a-f0-9]{32}$/.test(session))throw Error('Invalid Bot progress identity');
 const b=Buffer.alloc(SIZE);b.write('BF4BOT1');b.writeUInt32LE(1,8);b.writeUInt32LE(COUNT,12);b.writeBigUInt64LE(BigInt(sequence),16);b.write(installationId,40,40);b.write(session,80,40);
 const byId=new Map(players.filter(p=>p.isBot).map(p=>[Number(p.id),p]));
 for(let i=0;i<COUNT;i++){const a=HEADER+i*RECORD,p=byId.get(FIRST+i)||{};b.writeUInt32LE(FIRST+i,a);b.writeUInt32LE(Math.min(140,integer(p.rank||0,'rank')),a+4);b.writeBigUInt64LE(BigInt(integer(Math.floor(p.kills||0),'kills')),a+8);b.writeBigUInt64LE(BigInt(integer(Math.floor(p.deaths||0),'deaths')),a+16);b.writeBigUInt64LE(BigInt(integer(Math.floor(p.experience||0),'experience')),a+24);b.write(String(p.name||'').slice(0,32),a+32,63);}
 b.writeUInt32LE(checksum(b),36);return b;
}
export async function progressLocation(root){
 const current=await currentStorage(root),settings=JSON.parse((await fs.readFile(path.join(root,'settings/host-settings.json'),'utf8')).replace(/^\uFEFF/,''));
 if(!/^[a-f0-9-]{36}$/.test(settings.installationId))throw Error('Invalid installation ID');
 const dir=path.join(settings.sharedBlaze?path.join(root,'saves',current.address,'data'):current.data,'bot-progress');try{if((await fs.lstat(dir)).isSymbolicLink())throw Error('Bot progress directory cannot be a link');}catch(e){if(e.code!=='ENOENT')throw e;}
 const file=path.join(dir,settings.installationId+'.bin');try{if((await fs.lstat(file)).isSymbolicLink())throw Error('Bot progress file cannot be a link');}catch(e){if(e.code!=='ENOENT')throw e;}
 return {file,settings,current};
}
export async function readProgress(root){const {file,settings}=await progressLocation(root);try{return decodeProgress(await fs.readFile(file),{installationId:settings.installationId});}catch(e){if(e.code==='ENOENT')return null;throw e;}}
export async function prepareProgress(root,session){
 const {file,settings,current}=await progressLocation(root);const previous=await readProgress(root);let players=previous?.players;
 if(!players){players=[];for(const id of (settings.sharedBlaze?[]:await fs.readdir(path.join(current.data,'players')).catch(e=>{if(e.code==='ENOENT')return [];throw e;}))){if(!/^\d+$/.test(id))continue;const p=JSON.parse((await fs.readFile(path.join(current.data,'players',id,'player.json'),'utf8')).replace(/^\uFEFF/,''));if(p.IsBot===true)players.push({id:Number(p.Id),name:p.Name,isBot:true,rank:Number(p.Stats?.rank)||0,experience:Number(p.Stats?.sc_rank)||0,kills:Number(p.Stats?.c___k_g)||0,deaths:Number(p.Stats?.c___d_g)||0});}}
 await fs.mkdir(path.dirname(file),{recursive:true});if(previous)await fs.copyFile(file,file+'.previous');
 const game=settings.gameServerDirectory;
 if(/[^\x20-\x7e]|[\r\n]/.test(file)||file.length>240)throw Error('Bot progress storage requires an English path shorter than 240 characters');
 await fs.writeFile(path.join(game,'bot-progress-seed.bin'),encodeProgress({installationId:settings.installationId,session,players}));
 await fs.writeFile(path.join(game,'bot-progress.ini'),'[Progress]\r\nSessionId='+session+'\r\nStorePath='+file+'\r\n');
 return {file,imported:!previous};
}
export async function flushProgress(root){
 const s=JSON.parse((await fs.readFile(path.join(root,'runtime/bot-start.json'),'utf8')).replace(/^\uFEFF/,''));
 if(!s.progress)return {skipped:true};
 const {file,settings}=await progressLocation(root);const token=String(Date.now());const request=path.join(settings.gameServerDirectory,'bot-progress-flush.txt'),ack=path.join(settings.gameServerDirectory,'bot-progress-flushed.txt');
 await fs.writeFile(request,token,'ascii');
 for(let i=0;i<80;i++){await new Promise(r=>setTimeout(r,100));const got=await fs.readFile(ack,'ascii').catch(e=>{if(e.code==='ENOENT')return '';throw e;});if(got===token){const result=decodeProgress(await fs.readFile(file),{installationId:settings.installationId,session:s.session});return {saved:true,updatedAt:result.updatedAt};}}
 throw Error('Bot progress save was not confirmed; services remain running. Check bot-progress.log');
}
export async function enrichBotProgress(root,roster){
 const saved=await readProgress(root);if(!saved)return roster;
 const names=new Map();for(const p of saved.players.filter(p=>p.active)){names.set(p.name,(names.get(p.name)||[]).concat(p));}
 return roster.map(p=>{const hits=names.get(p.name);if(p.guid!==''||String(p.type)!=='0'||hits?.length!==1)return p;const b=hits[0];return {...p,botId:b.id,rank:String(b.rank),careerKills:b.kills,careerDeaths:b.deaths,experience:b.experience};});
}
if(typeof process!=='undefined'&&process.argv[1]&&import.meta.url===pathToFileURL(path.resolve(process.argv[1])).href){try{const root=path.resolve(path.dirname(fileURLToPath(import.meta.url)),'..');if(process.argv[2]!=='flush')throw Error('Unknown Bot progress action');console.log(JSON.stringify(await flushProgress(root)));}catch(e){console.error(e.message);process.exitCode=1;}}
