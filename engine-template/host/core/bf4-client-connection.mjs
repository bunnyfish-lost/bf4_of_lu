import fs from 'node:fs/promises';import path from 'node:path';
import {execFile} from 'node:child_process';import {promisify} from 'node:util';
const run=promisify(execFile),normalize=p=>String(p||'').replaceAll('\\','/').toLowerCase();
export const experimentalClient='';
export const experimentalEntry='从管理网页启动游戏';
export function describeClientConnection({clients,expectedPath=experimentalClient,worker}){
 const base={expectedPath,launchEntry:experimentalEntry,clients,connected:false};
 if(!clients.length)return {...base,state:'stopped',message:'未检测到 BF4；请用下方管理网页启动'};
 const matching=clients.filter(p=>normalize(p.path)===normalize(expectedPath));
 if(!matching.length)return {...base,state:clients.some(p=>p.path)?'wrong-client':'unknown',message:clients.some(p=>p.path)?'当前运行原目录或其他目录的 BF4，实验辅助没有连接；请退出当前 BF4 和它的启动器，再用网页的启动游戏按钮启动，EA 可保留':'检测到 BF4，但暂时无法读取路径，尚未确认辅助连接'};
 const connected=worker?.ready===true&&matching.some(p=>p.pid===worker.gamePid);
 return {...base,state:connected?'connected':'waiting-worker',connected,message:connected?'已连接MOD客户端；固定1秒补丁状态见服务概览':'已找到实验客户端，辅助模块尚未连接'+(worker?.message?'：'+worker.message:'')};
}
async function probe(){
 const script=`[Console]::OutputEncoding=[Text.UTF8Encoding]::new($false);$ErrorActionPreference='Stop';$ProgressPreference='SilentlyContinue';$items=@(Get-CimInstance Win32_Process -Filter "Name='bf4_no_drm.exe'"|ForEach-Object{[pscustomobject]@{pid=$_.ProcessId;path=$_.ExecutablePath}});ConvertTo-Json -Compress -InputObject $items`;
 const {stdout}=await run((process.env.BF4_LAUNCHER_PS||'C:/Windows/System32/WindowsPowerShell/v1.0/powershell.exe'),['-NoLogo','-NoProfile','-NonInteractive','-EncodedCommand',Buffer.from(script,'utf16le').toString('base64')],{windowsHide:true,timeout:7000});const result=JSON.parse(stdout.replace(/^\uFEFF/,''));if(!Array.isArray(result))throw Error('进程路径回读无效');return result;
}
export class ClientConnection {
 constructor(options={}){this.root=options.root;this.probe=options.probe||probe;this.clock=options.clock||Date.now;this.at=0;this.clients=null;this.pending=null;}
 async snapshot(worker){try{if(this.clients===null||this.clock()-this.at>5000){if(!this.pending)this.pending=this.probe().then(clients=>{this.clients=clients;this.at=this.clock();}).finally(()=>{this.pending=null;});await this.pending;}const config=await fs.readFile(path.join(this.root,'settings/client-game.json'),'utf8').then(JSON.parse);return describeClientConnection({clients:this.clients,expectedPath:path.join(config.directory,'bf4_no_drm.exe'),worker});}catch(e){return {state:'unknown',connected:false,clients:[],expectedPath:experimentalClient,launchEntry:experimentalEntry,message:'暂时无法检测游戏路径：'+e.message};}}
}
