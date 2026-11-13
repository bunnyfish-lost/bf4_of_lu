$ErrorActionPreference='Stop'

function Test-BF4WritableDirectory([string]$Path) {
    if([string]::IsNullOrWhiteSpace($Path) -or -not [IO.Path]::IsPathRooted($Path) -or -not [IO.Directory]::Exists($Path)){throw "Temporary directory does not exist: $Path"}
    $probe=[IO.Path]::Combine($Path,('bf4-write-'+[guid]::NewGuid().ToString('N')+'.tmp'))
    try {[IO.File]::WriteAllText($probe,'test');[IO.File]::Delete($probe)}
    finally {if([IO.File]::Exists($probe)){[IO.File]::Delete($probe)}}
}

function ConvertTo-BF4LongDirectory([string]$Path) {
    if([string]::IsNullOrWhiteSpace($Path) -or -not [IO.Path]::IsPathRooted($Path)){throw 'Temporary directory must be an absolute path.'}
    $full=[IO.Path]::GetFullPath($Path)
    $buffer=New-Object Text.StringBuilder 32768
    $length=[BF4LauncherPaths]::GetLongPathName($full,$buffer,32768)
    if($length -eq 0 -or $length -ge 32768){throw "Cannot resolve temporary directory: $Path"}
    return $buffer.ToString()
}

function Initialize-BF4LauncherTemp([string]$PackageRoot) {
    $originalTemp=$env:TEMP;$originalTmp=$env:TMP
    $failures=New-Object 'Collections.Generic.List[string]'
    $fallbacks=@()
    $local=[Environment]::GetFolderPath([Environment+SpecialFolder]::LocalApplicationData)
    if(-not [string]::IsNullOrWhiteSpace($local)){$fallbacks+=([IO.Path]::Combine($local,'Temp'))}
    if(-not [string]::IsNullOrWhiteSpace($PackageRoot)){$fallbacks+=([IO.Path]::Combine($PackageRoot,'.bf4-launcher-temp'))}
    $bootstrap=$null
    foreach($candidate in $fallbacks){
        try {
            [IO.Directory]::CreateDirectory($candidate)|Out-Null
            Test-BF4WritableDirectory $candidate
            $bootstrap=[IO.Path]::GetFullPath($candidate);break
        } catch {$failures.Add($candidate+': '+$_.Exception.Message)}
    }
    if(-not $bootstrap){throw ('无法创建启动临时目录。'+($failures -join ' | '))}
    try {
        $env:TEMP=$bootstrap;$env:TMP=$bootstrap
        if(-not ('BF4LauncherPaths' -as [type])){
            Add-Type -TypeDefinition @'
using System;
using System.Text;
using System.Runtime.InteropServices;
public static class BF4LauncherPaths {
    [DllImport("kernel32.dll", CharSet=CharSet.Unicode, SetLastError=true)]
    public static extern uint GetLongPathName(string path, StringBuilder buffer, uint length);
}
'@
        }
        $chosen=$null
        foreach($candidate in @($originalTemp,$originalTmp)+$fallbacks){
            if([string]::IsNullOrWhiteSpace($candidate)){continue}
            try {
                $resolved=ConvertTo-BF4LongDirectory $candidate
                Test-BF4WritableDirectory $resolved
                $chosen=$resolved;break
            } catch {$failures.Add($candidate+': '+$_.Exception.Message)}
        }
        if(-not $chosen){throw ('无法使用启动临时目录。'+($failures -join ' | '))}
        $env:TEMP=$chosen;$env:TMP=$chosen
        return [pscustomobject]@{root=$chosen;originalTemp=$originalTemp;originalTmp=$originalTmp;diagnostics=@($failures.ToArray())}
    } catch {
        $env:TEMP=$originalTemp;$env:TMP=$originalTmp
        throw
    }
}

function New-BF4LauncherSession([string]$TempRoot) {
    Test-BF4WritableDirectory $TempRoot
    $session=[IO.Path]::Combine($TempRoot,('BF4HostLauncher-'+[guid]::NewGuid().ToString('N')))
    [IO.Directory]::CreateDirectory($session)|Out-Null
    return $session
}
