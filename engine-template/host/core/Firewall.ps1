param([switch]$Remove)
. (Join-Path $PSScriptRoot 'Common.ps1')
try{
    $principal=New-Object Security.Principal.WindowsPrincipal([Security.Principal.WindowsIdentity]::GetCurrent())
    if(-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)){throw 'Run 06-Firewall.cmd as administrator.'}
    $s=Get-Settings;$group='BF4 Host '+$s.installationId
    if($Remove){Get-NetFirewallRule -Group $group -ErrorAction SilentlyContinue|Remove-NetFirewallRule;Write-Output 'Removed only rules for this installation.';exit 0}
    if($s.advertiseAddress -eq '127.0.0.1'){
        Get-NetFirewallRule -Group $group -ErrorAction SilentlyContinue|Remove-NetFirewallRule
        Write-Output 'Local play: removed only this installation rules; no inbound network access was added.';exit 0
    }
    $adapter=@(Get-NetIPAddress -AddressFamily IPv4 -IPAddress $s.advertiseAddress -ErrorAction Stop)
    if($adapter.Count -ne 1){throw 'Host virtual address must identify exactly one adapter.'}
    $iface=$adapter[0].InterfaceAlias
    $tcp=@(80,443,10040,4217,42127,42230)
    $gameExe=Join-Path $s.gameServerDirectory 'bf4_Server_Final.exe';$blazeExe=Join-Path $s.serverDirectory 'BF4BlazeServer.exe'
    foreach($rule in @(@('Blaze',$blazeExe,'TCP',$tcp),@('Game',$gameExe,'UDP',@($s.gamePort)))){
        $name=$group+' '+$rule[0]
        Get-NetFirewallRule -DisplayName $name -ErrorAction SilentlyContinue|Remove-NetFirewallRule
        New-NetFirewallRule -DisplayName $name -Group $group -Direction Inbound -Action Allow -Program $rule[1] -Protocol $rule[2] -LocalPort $rule[3] -LocalAddress $s.advertiseAddress -InterfaceAlias $iface -Profile Any|Out-Null
    }
    Write-Output "Allowed Blaze and game ports only on $iface / $($s.advertiseAddress). Firewall remains enabled. Panel and RCON were not opened."
}catch{Write-Output ('ERROR: '+$_.Exception.Message);exit 1}
