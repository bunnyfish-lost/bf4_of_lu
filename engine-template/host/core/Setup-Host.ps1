param([string]$SourceServer,[string]$Address,[string]$ServerName='BF4 LAN SERVER',[ValidateSet('local','lan')][string]$PlayMode,[switch]$CheckOnly)
. (Join-Path $PSScriptRoot 'Common.ps1')
try {
    if (-not [Environment]::Is64BitProcess) { throw 'Run the 64-bit Windows PowerShell entry.' }
    if ($hostRoot -notmatch '^[A-Za-z]:\\[A-Za-z0-9_ .\\-]+$') { throw 'Extract into an English path, for example D:\BF4Host. Avoid Chinese characters and special symbols.' }
    $null=Get-Node
    if (-not $SourceServer) { $SourceServer=(Read-Host 'Original patch Server folder (contains BF4BlazeServer.exe)').Trim('"') }
    $source=[IO.Path]::GetFullPath($SourceServer)
    if (-not (Test-Path -LiteralPath $source -PathType Container)) { throw 'Source Server folder not found.' }
    if ($source.StartsWith($hostRoot+'\',[StringComparison]::OrdinalIgnoreCase) -or $source -eq $hostRoot) { throw 'Choose the original patch outside this installation.' }
    $compat=Read-Json (Join-Path $hostRoot 'templates\compatibility.json')
    foreach ($entry in $compat.files.PSObject.Properties) {
        $file=Join-Path $source $entry.Name
        if (-not (Test-Path -LiteralPath $file) -or (Get-FileHash -LiteralPath $file -Algorithm SHA256).Hash -ne $entry.Value) { throw "Unsupported or missing file: $($entry.Name). Use BF4 LAN BOT ALPHA v0.1 (2026-10-15)." }
    }
    $copyFiles=@('bf4_Server_Final.exe','dinput8.dll','xinput9_1_0.dll','Engine.BuildInfo_Win64_final_Server.dll','GBClient.dll','navigation.bf4pack','ProviderID.dat','builtLevels.json','ServerOptions.cfg')
    foreach($file in $copyFiles+@('Data\initfs_DedicatedServer','Data\layout.toc','Data\sanity.toc')) { if(-not(Test-Path -LiteralPath (Join-Path $source $file) -PathType Leaf)){throw "Missing source file: $file"} }
    foreach($dir in @('Data\DedicatedServer','assets')) { if(-not(Test-Path -LiteralPath (Join-Path $source $dir) -PathType Container)){throw "Missing source folder: $dir"} }
    foreach($dir in @('Data','assets')) {
        $items=@(Get-Item -LiteralPath (Join-Path $source $dir))+@(Get-ChildItem -LiteralPath (Join-Path $source $dir) -Recurse -Force)
        if(@($items | Where-Object { $_.Attributes -band [IO.FileAttributes]::ReparsePoint }).Count){throw "Source $dir contains a link; use an extracted original copy."}
    }
    if($PlayMode -eq 'local'){$Address='127.0.0.1'}
    if($Address -ne '127.0.0.1'){
        $adapters=Get-LocalAddresses
        $localIps=@($adapters|Select-Object -ExpandProperty Address)
        if(-not $Address){$adapters|Format-Table; $Address=Read-Host 'Host virtual IPv4 (Radmin / LAN; not a guest address)'}
        $ip=$null
        if($Address -notmatch '^(\d{1,3}\.){3}\d{1,3}$' -or -not [Net.IPAddress]::TryParse($Address,[ref]$ip) -or $Address -notin $localIps){throw ("Address '$Address' must be a current local IPv4. Available: "+($localIps -join ', '))}
    }
    if([string]::IsNullOrWhiteSpace($ServerName) -or $ServerName.Length -gt 60 -or $ServerName -match '["\\\x00-\x1f]'){throw 'Invalid server name.'}
    if($CheckOnly){Write-Output 'CHECK PASSED: compatible source, resources, local address and Node. No files were imported.';exit 0}
    if((Test-Path -LiteralPath (Join-Path $hostRoot 'runtime')) -or (Test-Path -LiteralPath (Join-Path $hostRoot 'settings'))){throw 'This copy is already initialized or has a partial runtime. Use a fresh extracted folder; existing data was not overwritten.'}
    $runtime=Join-Path $hostRoot 'runtime';$game=Join-Path $runtime 'GameServer';$blaze=Join-Path $runtime 'Blaze';$settingsDir=Join-Path $hostRoot 'settings'
    New-Item -ItemType Directory -Path $game,$blaze,(Join-Path $game 'Scripts'),(Join-Path $game 'Instance\Admin'),$settingsDir -Force | Out-Null
    Write-Output 'Copying original server resources (about 2.3 GB)...'
    foreach($file in $copyFiles){Copy-Item -LiteralPath (Join-Path $source $file) -Destination (Join-Path $game $file)}
    Copy-Item -LiteralPath (Join-Path $source 'Data') -Destination (Join-Path $game 'Data') -Recurse
    Copy-Item -LiteralPath (Join-Path $source 'assets') -Destination (Join-Path $blaze 'assets') -Recurse
    Copy-Item -LiteralPath (Join-Path $source 'BF4BlazeServer.exe') -Destination $blaze
    $rng=[Security.Cryptography.RandomNumberGenerator]::Create();$bytes=New-Object byte[] 8;$rng.GetBytes($bytes);$rng.Dispose();$password=([BitConverter]::ToString($bytes)).Replace('-','').ToLowerInvariant()
    Write-Json (Join-Path $runtime 'private.json') ([pscustomobject]@{rconPassword=$password})
    $startup=[regex]::Replace([IO.File]::ReadAllText((Join-Path $hostRoot 'templates\Startup.txt')),'(?m)^vars.serverName[^\r\n]*',[Text.RegularExpressions.MatchEvaluator]{param($match) 'vars.serverName "'+$ServerName+'"'})
    [IO.File]::WriteAllText((Join-Path $game 'Instance\Admin\Startup.txt'),($startup+"admin.password $password`r`n"),$utf8)
    $mapList=[IO.File]::ReadAllText((Join-Path $hostRoot 'templates\MapList.txt')).TrimStart([char]0xFEFF)
    [IO.File]::WriteAllText((Join-Path $game 'Instance\Admin\MapList.txt'),$mapList,$utf8)
    foreach($name in @('BanList.txt','ReservedSlotsList.txt')){[IO.File]::WriteAllText((Join-Path $game ('Instance\Admin\'+$name)),'',$utf8)}
    Copy-Item -LiteralPath (Join-Path $hostRoot 'templates\Win32Game.cfg') -Destination (Join-Path $game 'Scripts\Win32Game.cfg')
    Copy-Item -LiteralPath (Join-Path $hostRoot 'templates\dinput8_bf4hook.ini') -Destination $game
    $config=[IO.File]::ReadAllText((Join-Path $hostRoot 'templates\BlazeConfig.txt')) -replace '(?m)^ServerIP=[^\r\n]*',('ServerIP='+$Address)
    $listenAddress=if($Address -eq '127.0.0.1'){'127.0.0.1'}else{'0.0.0.0'}
    $config=$config -replace '(?m)^ListenAddress=[^\r\n]*',('ListenAddress='+$listenAddress)
    [IO.File]::WriteAllText((Join-Path $blaze 'config.txt'),$config,$utf8)
    $settings=[pscustomobject]@{installationId=[guid]::NewGuid().ToString();version='0.8.3';serverName=$ServerName;advertiseAddress=$Address;gamePort=25205;rconPort=25210;panelPort=8787;maxSoldiers=64;initialBots=63;serverDirectory=$blaze;gameServerDirectory=$game;blazeTcpPorts=@(80,443,10040,10041,4217,42127,42230)}
    Write-Json (Join-Path $settingsDir 'host-settings.json') $settings
    Write-Json (Join-Path $settingsDir 'bot-loadout.json') ([pscustomobject]@{version=1;weaponIds=@()})
    Write-Json (Join-Path $settingsDir 'bot-damage.json') ([pscustomobject]@{version=1;percent=100})
    Write-Json (Join-Path $runtime 'processes.json') ([pscustomobject]@{processes=@()})
    Write-Json (Join-Path $hostRoot 'panel\bot-fill.json') ([pscustomobject]@{enabled=$true;target=64;reserve=0})
    Write-Output 'SETUP COMPLETE. Run 02-Start-Host.cmd. The source folder was not modified.'
} catch { Write-Output ('ERROR: '+$_.Exception.Message);exit 1 }
