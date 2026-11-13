param([ValidateSet('Start','Stop','Restart','RestartGame','Procon')][string]$Action)
$ErrorActionPreference='Stop'
. (Join-Path (Split-Path -Parent $PSScriptRoot) 'core\Common.ps1')
try {
    if($Action -eq 'Procon'){
        $dir=Join-Path $PSScriptRoot 'procon-local';$exe=Join-Path $dir 'PRoCon.exe'
        if(-not(Test-Path -LiteralPath $exe)){throw 'Import Procon with 07-Import-Procon.cmd first.'}
        $existing=@(Get-Process -Name PRoCon -ErrorAction SilentlyContinue|Where-Object{$_.Path -and [string]::Equals($_.Path,$exe,[StringComparison]::OrdinalIgnoreCase)})
        if(-not $existing.Count){Start-Process -FilePath $exe -WorkingDirectory $dir -WindowStyle Normal|Out-Null}
        Write-Output 'Opened Procon.';exit 0
    }
    $ps=if($env:BF4_LAUNCHER_PS){$env:BF4_LAUNCHER_PS}else{Join-Path $env:SystemRoot 'System32\WindowsPowerShell\v1.0\powershell.exe'}
    & $ps -NoLogo -NoProfile -NonInteractive -ExecutionPolicy Bypass -File (Join-Path (Split-Path -Parent $PSScriptRoot) 'core\Host-Control.ps1') -Action $Action
    exit $LASTEXITCODE
} catch { Write-Error $_.Exception.Message;exit 1 }
