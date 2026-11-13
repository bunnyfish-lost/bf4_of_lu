. (Join-Path $PSScriptRoot 'Common.ps1')
try{
    $s=Get-Settings
    $tcp=@(Get-NetTCPConnection -State Listen -ErrorAction Stop|Where-Object LocalPort -in ($s.blazeTcpPorts+@($s.rconPort,$s.panelPort))|ForEach-Object{$p=Get-Process -Id $_.OwningProcess -ErrorAction SilentlyContinue;[pscustomobject]@{port=$_.LocalPort;address=$_.LocalAddress;pid=$_.OwningProcess;name=$p.ProcessName;path=$p.Path}})
    $udp=@(Get-NetUDPEndpoint -ErrorAction Stop|Where-Object LocalPort -eq $s.gamePort|ForEach-Object{$p=Get-Process -Id $_.OwningProcess -ErrorAction SilentlyContinue;[pscustomobject]@{port=$_.LocalPort;address=$_.LocalAddress;pid=$_.OwningProcess;name=$p.ProcessName;path=$p.Path}})
    $report=[pscustomobject]@{time=[DateTime]::UtcNow.ToString('o');hostAddress=$s.advertiseAddress;adapters=@(Get-LocalAddresses);tcp=$tcp;udp=$udp;note='TCP listeners do not validate remote TLS login or UDP gameplay. No process was stopped.'}
    Write-Json (Join-Path $hostRoot 'runtime\host-diagnostic.json') $report
    $report|ConvertTo-Json -Depth 8
    Write-Output 'Saved: runtime\host-diagnostic.json'
}catch{Write-Output ('ERROR: '+$_.Exception.Message);exit 1}
