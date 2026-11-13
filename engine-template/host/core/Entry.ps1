param([ValidateSet('Start','Stop','Panel','RestartPanel','Check')][string]$Action='Panel')
$ErrorActionPreference='Stop'
$ps=if($env:BF4_LAUNCHER_PS){$env:BF4_LAUNCHER_PS}else{Join-Path $env:SystemRoot 'System32\WindowsPowerShell\v1.0\powershell.exe'}
function Run-Script($File,$Arguments){& $ps -NoLogo -NoProfile -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot $File) @Arguments;if($LASTEXITCODE -ne 0){throw "$File failed. Read the ERROR line above."}}
try{
    switch($Action){
        'Start'{Run-Script 'Panel-Control.ps1' @('-Action','Start');Run-Script 'Host-Control.ps1' @('-Action','Start')}
        'Stop'{Run-Script 'Panel-Control.ps1' @('-Action','Stop');Run-Script 'Host-Control.ps1' @('-Action','Stop')}
        'Panel'{Run-Script 'Panel-Control.ps1' @('-Action','Start')}
        'RestartPanel'{Run-Script 'Panel-Control.ps1' @('-Action','Restart')}
        'Check'{Run-Script 'Diagnose-Host.ps1' @()}
    }
}catch{Write-Output ('ERROR: '+$_.Exception.Message);exit 1}
