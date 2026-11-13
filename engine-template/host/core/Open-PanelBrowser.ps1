function Open-BF4PanelBrowser([string]$Url) {
 $uri=$null
 if(-not [Uri]::TryCreate($Url,[UriKind]::Absolute,[ref]$uri) -or $uri.Scheme -ne 'http' -or $uri.Host -ne '127.0.0.1'){throw 'Invalid local panel URL.'}
 try {Start-Process -FilePath $Url -ErrorAction Stop|Out-Null;return $true} catch {}
 $candidates=@()
 foreach($base in @($env:ProgramFiles,[Environment]::GetEnvironmentVariable('ProgramFiles(x86)'),$env:LOCALAPPDATA)){
  if($base){$candidates+=Join-Path $base 'Microsoft\Edge\Application\msedge.exe';$candidates+=Join-Path $base 'Google\Chrome\Application\chrome.exe'}
 }
 foreach($browser in $candidates){if(Test-Path -LiteralPath $browser -PathType Leaf){try{Start-Process -FilePath $browser -ArgumentList $Url -ErrorAction Stop|Out-Null;return $true}catch{}}}
 try {Add-Type -AssemblyName System.Windows.Forms;[Windows.Forms.Clipboard]::SetText($Url);[Windows.Forms.MessageBox]::Show(('无法自动打开浏览器。请把以下地址粘贴到浏览器中：'+[Environment]::NewLine+$Url),'管理网页地址')|Out-Null}catch{Write-Warning ('请在浏览器打开：'+$Url)}
 return $false
}
