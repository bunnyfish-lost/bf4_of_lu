$ErrorActionPreference = 'Stop'
if ($PSVersionTable.PSEdition -eq 'Desktop') {
    $utilityManifest = Join-Path $PSHOME 'Modules\Microsoft.PowerShell.Utility\Microsoft.PowerShell.Utility.psd1'
    Import-Module -Name $utilityManifest -ErrorAction Stop
}
[Console]::OutputEncoding = New-Object Text.UTF8Encoding($false)
$OutputEncoding = [Console]::OutputEncoding
$hostRoot = [IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot))
$utf8 = New-Object Text.UTF8Encoding($false)
$processEnvironment=[Environment]::GetEnvironmentVariables('Process')
foreach($group in @($processEnvironment.Keys|Group-Object { $_.ToUpperInvariant() }|Where-Object Count -gt 1)) {
    $key=[string]$group.Group[0];$value=$processEnvironment[$key]
    foreach($duplicate in $group.Group){[Environment]::SetEnvironmentVariable($duplicate,$null,'Process')}
    [Environment]::SetEnvironmentVariable($key,$value,'Process')
}
function Read-Json([string]$Path) { Get-Content -LiteralPath $Path -Raw -Encoding UTF8 | ConvertFrom-Json }
function Write-Json([string]$Path,$Value) {
    $tmp = $Path + '.tmp'
    [IO.File]::WriteAllText($tmp,(ConvertTo-Json -InputObject $Value -Depth 12),$utf8)
    Move-Item -LiteralPath $tmp -Destination $Path -Force
}
function Get-Settings {
    $s = Read-Json (Join-Path $hostRoot 'settings\host-settings.json')
    foreach ($pair in @(@('serverDirectory','runtime\Blaze'),@('gameServerDirectory','runtime\GameServer'))) {
        if (-not [string]::Equals([IO.Path]::GetFullPath($s.($pair[0])),(Join-Path $hostRoot $pair[1]),[StringComparison]::OrdinalIgnoreCase)) { throw 'Installation path changed. Use Check and Repair Runtime in the launcher.' }
    }
    if ($s.installationId -notmatch '^[a-f0-9-]{36}$') { throw 'Invalid installation identity.' }
    return $s
}
function Get-Owned($Record) {
    $expectedStart=0L
    if (-not $Record.path -or -not [IO.Path]::IsPathRooted($Record.path) -or -not [long]::TryParse([string]$Record.startTicks,[ref]$expectedStart) -or $expectedStart -le 0) { throw "Invalid process identity record for PID $($Record.pid); no process was stopped." }
    $p = Get-Process -Id $Record.pid -ErrorAction SilentlyContinue
    if ($null -eq $p) { return $null }
    if (-not $p.Path) { throw "Cannot verify process identity for PID $($Record.pid); no process was stopped." }
    # A reused PID is a stale record, never permission to operate the new process.
    if (-not [string]::Equals([IO.Path]::GetFullPath($p.Path),[IO.Path]::GetFullPath($Record.path),[StringComparison]::OrdinalIgnoreCase) -or $p.StartTime.ToUniversalTime().Ticks -ne $expectedStart) { return $null }
    return $p
}
function Get-Node {
    $exe = Join-Path $hostRoot 'node\node.exe'
    if (-not (Test-Path -LiteralPath $exe)) { $found=Get-Command node.exe -ErrorAction SilentlyContinue; if($found){$exe=$found.Source}else{throw 'Node.js 24 x64 missing. Extract the complete package again or see the tutorial.'} }
    $version = & $exe --version
    if ($LASTEXITCODE -ne 0 -or $version -notmatch '^v24\.') { throw 'Node.js 24 x64 is required.' }
    return $exe
}
function Test-Port([int]$Port) {
    $c=New-Object Net.Sockets.TcpClient
    try { return ($c.ConnectAsync('127.0.0.1',$Port).Wait(400) -and $c.Connected) } catch { return $false } finally { $c.Dispose() }
}
function Get-LocalAddresses {
    @([Net.NetworkInformation.NetworkInterface]::GetAllNetworkInterfaces() | Where-Object OperationalStatus -eq 'Up' | ForEach-Object {
        $nic=$_
        $_.GetIPProperties().UnicastAddresses | Where-Object { $_.Address.AddressFamily -eq 'InterNetwork' } | ForEach-Object {
            [pscustomobject]@{Name=$nic.Name;Address=$_.Address.IPAddressToString}
        }
    })
}
