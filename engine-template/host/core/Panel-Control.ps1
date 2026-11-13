param([ValidateSet('Start','Stop','Restart')][string]$Action='Start',[switch]$NoBrowser)
. (Join-Path $PSScriptRoot 'Common.ps1')
. (Join-Path $PSScriptRoot 'Open-PanelBrowser.ps1')
$mutex=$null;$locked=$false
try {
    $settings=Get-Settings;$panel=Join-Path $hostRoot 'panel';$stateFile=Join-Path $hostRoot 'runtime\panel-process.json'
    $mutex=New-Object Threading.Mutex($false,('Local\BF4Panel-'+$settings.installationId));$locked=$mutex.WaitOne(5000)
    if(-not $locked){throw 'Another panel operation is running.'}
    $url='http://127.0.0.1:'+$settings.panelPort
    function Probe-Panel {try{$reply=Invoke-RestMethod -Uri ($url+'/api/bootstrap') -TimeoutSec 2 -UseBasicParsing;return ($reply.app -eq 'bf4-host-panel' -and $reply.installationId -eq $settings.installationId)}catch{return $false}}
    if($Action -in @('Stop','Restart')){
        if(Test-Path -LiteralPath $stateFile){$record=Read-Json $stateFile;$p=Get-Owned $record;if($null -ne $p){Stop-Process -Id $p.Id;if(-not $p.WaitForExit(5000)){throw 'Panel did not stop.'}};Remove-Item -LiteralPath $stateFile}
        & (Join-Path $PSScriptRoot 'Stop-LocalFun.ps1')
        if($Action -eq 'Stop'){Write-Output 'Panel stopped. Game server is unchanged.';exit 0}
    }
    if(Probe-Panel){if(-not $NoBrowser){Open-BF4PanelBrowser $url|Out-Null};Write-Output $url;exit 0}
    if(Test-Port $settings.panelPort){throw "Panel port $($settings.panelPort) belongs to another service. Change panelPort in settings\host-settings.json, or stop that panel."}
    $exe=Get-Node
    $p=Start-Process -FilePath $exe -ArgumentList ('"'+(Join-Path $panel 'main.mjs')+'"') -WorkingDirectory $hostRoot -WindowStyle Hidden -RedirectStandardOutput (Join-Path $hostRoot 'runtime\panel.stdout.log') -RedirectStandardError (Join-Path $hostRoot 'runtime\panel.stderr.log') -PassThru
    Write-Json $stateFile ([pscustomobject]@{pid=$p.Id;path=$exe;startTicks=$p.StartTime.ToUniversalTime().Ticks.ToString()})
    for($i=0;$i -lt 30;$i++){Start-Sleep -Milliseconds 300;if(Probe-Panel){if(-not $NoBrowser){Open-BF4PanelBrowser $url|Out-Null};Write-Output $url;exit 0};$p.Refresh();if($p.HasExited){break}}
    throw 'Panel did not become ready. Inspect runtime\panel.stderr.log.'
} catch { Write-Output ('ERROR: '+$_.Exception.Message);exit 1 } finally {if($locked){$mutex.ReleaseMutex()};if($mutex){$mutex.Dispose()}}
