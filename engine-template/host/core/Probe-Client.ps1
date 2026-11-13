param([Parameter(Mandatory=$true)][string]$ExpectedPath)
$ErrorActionPreference='Stop'
[Console]::OutputEncoding=New-Object Text.UTF8Encoding($false)
try{
 $expected=[IO.Path]::GetFullPath($ExpectedPath)
 $clients=@(Get-Process -Name 'bf4_no_drm' -ErrorAction SilentlyContinue)
 $found=$null;$unreadable=$false
 foreach($process in $clients){
  try{
   if($process.HasExited -or $process.Threads.Count -le 0){continue}
   if($process.MainModule.FileName -ne $expected){continue}
   $found=@{pid=$process.Id;startTicks=$process.StartTime.ToUniversalTime().Ticks.ToString();ageSeconds=([DateTime]::UtcNow-$process.StartTime.ToUniversalTime()).TotalSeconds;hasWindow=($process.MainWindowHandle -ne [IntPtr]::Zero);responding=$process.Responding}
   break
  }catch{$unreadable=$true}finally{$process.Dispose()}
 }
 if($found){$found|ConvertTo-Json -Compress}else{@{pid=0;reason=if($unreadable){'permission'}else{'not-running'}}|ConvertTo-Json -Compress}
 exit 0
}catch{[Console]::Error.WriteLine($_.Exception.ToString());exit 1}
