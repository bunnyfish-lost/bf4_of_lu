import {execFile} from 'node:child_process';
import {promisify} from 'node:util';
import path from 'node:path';
const run=promisify(execFile);
export async function runtimeProcesses(){
 if(process.platform!=='win32')return [];
 const script=`$ErrorActionPreference='Stop';[Console]::OutputEncoding=[Text.UTF8Encoding]::new($false);if($PSVersionTable.PSEdition -eq 'Desktop'){Import-Module (Join-Path $PSHOME 'Modules\\Microsoft.PowerShell.Utility\\Microsoft.PowerShell.Utility.psd1')};$items=@();foreach($name in @('BF4BlazeServer','bf4_Server_Final','bf4_no_drm','BF4Launcher')){foreach($p in [Diagnostics.Process]::GetProcessesByName($name)){try{if($p.HasExited){continue};$image=$p.MainModule.FileName;if([string]::IsNullOrWhiteSpace($image)){throw 'Process path unavailable'};$items+=[pscustomobject]@{pid=$p.Id;path=$image;unreadable=$false}}catch{$items+=[pscustomobject]@{pid=$p.Id;path='';unreadable=$true}}finally{$p.Dispose()}}};ConvertTo-Json -InputObject $items -Compress`;
 const environment=Object.fromEntries(Object.entries(process.env).filter(([key])=>key.toUpperCase()!=='PSMODULEPATH'));
 const ps=process.env.BF4_LAUNCHER_PS||path.join(process.env.SystemRoot||'C:/Windows','System32/WindowsPowerShell/v1.0/powershell.exe');
 const result=await run(ps,['-NoLogo','-NoProfile','-NonInteractive','-EncodedCommand',Buffer.from(script,'utf16le').toString('base64')],{windowsHide:true,env:environment,timeout:10000});
 const rows=JSON.parse(result.stdout.trim().replace(/^\uFEFF/,''));return Array.isArray(rows)?rows:[rows];
}
