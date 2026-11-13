param([ValidateSet('Start','Stop')][string]$Mode='Start',[string]$SnapshotPath,[string]$UiTestReport)
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'Launcher-Paths.ps1')
try {$launcherTemp=Initialize-BF4LauncherTemp -PackageRoot (Split-Path -Parent $PSScriptRoot)} catch {
    Add-Type -AssemblyName System.Windows.Forms
    [Windows.Forms.MessageBox]::Show($_.Exception.Message,'BF4 临时目录错误')|Out-Null;exit 1
}
Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing
. (Join-Path $PSScriptRoot 'Common.ps1')
. (Join-Path $PSScriptRoot 'Folder-Picker.ps1')
. (Join-Path $PSScriptRoot 'Open-PanelBrowser.ps1')
[Windows.Forms.Application]::EnableVisualStyles()
[Windows.Forms.Application]::SetCompatibleTextRenderingDefault($false)
$packageRoot=$hostRoot
$preview=[bool]($SnapshotPath -or $UiTestReport)
$principal=New-Object Security.Principal.WindowsPrincipal([Security.Principal.WindowsIdentity]::GetCurrent())
if(-not $preview -and -not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)){
    try{
        $ps=if($env:BF4_LAUNCHER_PS){$env:BF4_LAUNCHER_PS}else{Join-Path $env:SystemRoot 'System32\WindowsPowerShell\v1.0\powershell.exe'}
        Start-Process -FilePath $ps -ArgumentList ('-NoLogo -NoProfile -STA -ExecutionPolicy Bypass -WindowStyle Hidden -File "'+$PSCommandPath+'" -Mode '+$Mode) -Verb RunAs -WindowStyle Hidden|Out-Null
    }catch{[Windows.Forms.MessageBox]::Show('请允许管理员权限后重试。','BF4 开服助手')|Out-Null}
    exit
}
$script:installed=$false;$script:worker=$null;$script:progressFile=$null;$script:sessionDir=$null;$script:lastState='';$script:opened=$false;$script:url=$null;$script:oldAddress=$null
$script:previousSetup=$null
$script:installRoot=Join-Path ([IO.Path]::GetPathRoot($packageRoot)) 'BF4Host'
$pointerFile=Join-Path $packageRoot 'launcher-state.json'
try{
    if(-not $preview -and (Test-Path -LiteralPath $pointerFile)){$saved=Read-Json $pointerFile;if($saved.version -notin @('0.4.0','0.5.0','0.6.0','0.7.0','0.7.1','0.7.2','0.7.3','0.7.4-test.1','0.7.4beta','0.8.0','0.8.1beta','0.8.1','0.8.1-hotfix.1','0.8.2','0.8.2-hotfix.1','0.8.3')){throw '安装记录版本不匹配。'};if([string]::IsNullOrWhiteSpace([string]$saved.installRoot)){throw '安装记录缺少安装位置。'};$script:installRoot=$saved.installRoot}
    if(-not $preview -and (Test-Path -LiteralPath (Join-Path $script:installRoot 'managed-install.json'))){
        $marker=Read-Json (Join-Path $script:installRoot 'managed-install.json')
        $settings=Read-Json (Join-Path $script:installRoot 'settings\host-settings.json')
        if($marker.version -in @('0.4.0','0.5.0','0.6.0','0.7.0','0.7.1','0.7.2','0.7.3','0.7.4-test.1','0.7.4beta','0.8.0','0.8.1beta','0.8.1','0.8.1-hotfix.1','0.8.2','0.8.2-hotfix.1','0.8.3') -and $marker.installationId -eq $settings.installationId){$script:installed=$true;$script:oldAddress=$settings.advertiseAddress;$script:url='http://127.0.0.1:'+$settings.panelPort+'/'}
    }
}catch{[Windows.Forms.MessageBox]::Show(('无法读取安装记录：'+$_.Exception.Message),'BF4 开服助手')|Out-Null;exit 1}
if($preview){$script:installed=$false;$script:installRoot='D:\BF4Host'}
if($Mode -eq 'Stop' -and -not $script:installed){[Windows.Forms.MessageBox]::Show('尚未找到这套安装。请先运行“开始开服”。','BF4 开服助手')|Out-Null;exit 1}

$form=New-Object Windows.Forms.Form
$form.Text='BF4 开服助手 0.8.3';$form.ClientSize=New-Object Drawing.Size(760,630);$form.StartPosition='CenterScreen';$form.FormBorderStyle='FixedDialog';$form.MaximizeBox=$false
$form.BackColor=[Drawing.ColorTranslator]::FromHtml('#F2F5F8');$form.Font=New-Object Drawing.Font('Microsoft YaHei UI',10)
$form.AutoScaleMode='Dpi'
function Label($Text,$X,$Y,$Width,$Height,$Size=10,$Bold=$false){
    $c=New-Object Windows.Forms.Label;$c.Text=$Text;$c.Location=New-Object Drawing.Point($X,$Y);$c.Size=New-Object Drawing.Size($Width,$Height)
    $style=[Drawing.FontStyle]::Regular;if($Bold){$style=[Drawing.FontStyle]::Bold};$c.Font=New-Object Drawing.Font('Microsoft YaHei UI',$Size,$style);$c.ForeColor=[Drawing.ColorTranslator]::FromHtml('#243B50');$form.Controls.Add($c);return $c
}
function Button($Text,$X,$Y,$Width,$Height){$c=New-Object Windows.Forms.Button;$c.Text=$Text;$c.Location=New-Object Drawing.Point($X,$Y);$c.Size=New-Object Drawing.Size($Width,$Height);$c.FlatStyle='Flat';$c.FlatAppearance.BorderColor=[Drawing.ColorTranslator]::FromHtml('#BBCAD7');$c.BackColor=[Drawing.Color]::White;$form.Controls.Add($c);return $c}
function TextBox($Text,$X,$Y,$Width){$c=New-Object Windows.Forms.TextBox;$c.Text=$Text;$c.Location=New-Object Drawing.Point($X,$Y);$c.Size=New-Object Drawing.Size($Width,32);$form.Controls.Add($c);return $c}
$null=Label 'BF4 开服助手' 30 25 480 43 24 $true
$resetSetup=Button '重新设置' 610 32 118 35
$resetSetup.Visible=($script:installed -and $Mode -eq 'Start')
$null=Label '原版 Server 文件夹' 32 94 450 26 10 $true
$sourceBox=TextBox '' 32 123 564
$browse=Button '选择文件夹' 610 119 118 35
$null=Label '游玩方式' 32 178 120 25 10 $true
$playModeBox=New-Object Windows.Forms.ComboBox;$playModeBox.DropDownStyle='DropDownList';$playModeBox.Location=New-Object Drawing.Point(160,174);$playModeBox.Size=New-Object Drawing.Size(260,32);$form.Controls.Add($playModeBox)
$null=$playModeBox.Items.Add('Radmin 联机');$null=$playModeBox.Items.Add('仅本机游玩');$null=$playModeBox.Items.Add('自定义主机 IP')
$playModeBox.SelectedIndex=0;if($script:installed -and $script:oldAddress -eq '127.0.0.1'){$playModeBox.SelectedIndex=1}
$null=Label '服务器名称' 32 230 350 25 10 $true
$nameBox=TextBox 'BF4 LAN SERVER' 32 259 388
$addressLabel=Label '主机 Radmin IP' 448 230 220 25 10 $true
$addressBox=New-Object Windows.Forms.ComboBox;$addressBox.DropDownStyle='DropDownList';$addressBox.Location=New-Object Drawing.Point(448,259);$addressBox.Size=New-Object Drawing.Size(280,32);$form.Controls.Add($addressBox)
$addressNote=Label '' 448 294 280 38 9
$installLabel=Label '安装位置' 32 330 150 23 10 $true
$installText=Label $script:installRoot 32 359 560 29 10
$changeLocation=Button '更改位置' 610 352 118 35
$openPanel=Button '打开管理网页' 32 410 150 44
$advanced=Button '高级工具 ▾' 194 410 132 44
$startButton=Button '一键开服' 476 406 252 48
$startButton.BackColor=[Drawing.ColorTranslator]::FromHtml('#1268A5');$startButton.ForeColor=[Drawing.Color]::White;$startButton.FlatAppearance.BorderSize=0;$startButton.Font=New-Object Drawing.Font('Microsoft YaHei UI',12,[Drawing.FontStyle]::Bold)
$bar=New-Object Windows.Forms.ProgressBar;$bar.Location=New-Object Drawing.Point(32,474);$bar.Size=New-Object Drawing.Size(696,6);$form.Controls.Add($bar)
$statusTitle=Label '就绪' 32 493 690 26 11 $true
$statusDetail=Label '' 32 524 690 42 10
$null=Label '感谢msk对战地游戏社区的巨大贡献。本软件完全免费。' 32 576 696 22 9
$null=Label '附加功能仅供本地娱乐，请勿用于线上模式。' 32 600 696 22 9
$openPanel.Enabled=$false
if($script:installed -and -not $preview){try{$boot=Invoke-RestMethod -Uri ($script:url+'api/bootstrap') -TimeoutSec 1 -UseBasicParsing;$openPanel.Enabled=($boot.installationId -eq $settings.installationId)}catch{}}
$browse.Add_Click({
    try {
        $selected=Select-HostFolder -CurrentPath $sourceBox.Text -Description '选择原版 Server 文件夹或其上一级文件夹。' -Owner $form
        if($selected){$sourceBox.Text=$selected;$statusTitle.Text='就绪';$statusDetail.Text=''}
    } catch {$statusTitle.Text='无法选择文件夹';$statusDetail.Text=$_.Exception.Message}
})
$changeLocation.Add_Click({
    try {
        $selected=Select-HostFolder -CurrentPath (Split-Path -Parent $script:installRoot) -Description '选择安装位置，将创建 BF4Host 文件夹。' -Owner $form -AllowNewFolder $true
        if($selected){$script:installRoot=Join-Path $selected 'BF4Host';$installText.Text=$script:installRoot;$statusTitle.Text='就绪';$statusDetail.Text=''}
    } catch {$statusTitle.Text='无法选择安装位置';$statusDetail.Text=$_.Exception.Message}
})
function Detect-Address {
    $previous=$addressBox.Text;$addressBox.Items.Clear();$addressBox.DropDownStyle='DropDownList'
    if($playModeBox.SelectedIndex -eq 2){
        $addressBox.DropDownStyle='DropDown';$addressBox.Enabled=($Mode -eq 'Start');$addressLabel.Text='自定义主机 IPv4';$addressNote.Text='填写主机组网网卡或局域网 IPv4';$statusDetail.Text=''
        if(-not $preview){foreach($adapter in [Net.NetworkInformation.NetworkInterface]::GetAllNetworkInterfaces()){if($adapter.OperationalStatus -ne 'Up'){continue};foreach($item in $adapter.GetIPProperties().UnicastAddresses){if($item.Address.AddressFamily -eq 'InterNetwork' -and $item.Address.IPAddressToString -notmatch '^(127\.|169\.254\.|0\.)'){$null=$addressBox.Items.Add($item.Address.IPAddressToString)}}}}
        if($previous -and $previous -ne '127.0.0.1'){$addressBox.Text=$previous}elseif($script:oldAddress -and $script:oldAddress -ne '127.0.0.1'){$addressBox.Text=$script:oldAddress};return
    }
    if($playModeBox.SelectedIndex -eq 1){
        $null=$addressBox.Items.Add('127.0.0.1');$addressBox.SelectedIndex=0;$addressBox.Enabled=$false
        $addressLabel.Text='本机 Blaze IP';$addressNote.Text='无需 Radmin，启动器填写此地址';$statusDetail.Text='';return
    }
    $addressLabel.Text='主机 Radmin IP';$addressBox.Enabled=($Mode -eq 'Start');$statusDetail.Text=''
    if($previous -eq '127.0.0.1' -and $script:oldAddress -ne '127.0.0.1'){$previous=$script:oldAddress}
    if($preview){$null=$addressBox.Items.Add('26.1.2.3')}else{
        foreach($adapter in [Net.NetworkInformation.NetworkInterface]::GetAllNetworkInterfaces()){
            if($adapter.OperationalStatus -ne 'Up' -or ($adapter.Name+' '+$adapter.Description) -notmatch '(?i)radmin|famatech'){continue}
            foreach($item in $adapter.GetIPProperties().UnicastAddresses){if($item.Address.AddressFamily -eq 'InterNetwork' -and $item.Address.IPAddressToString -notmatch '^(127\.|169\.254\.|0\.)'){$null=$addressBox.Items.Add($item.Address.IPAddressToString)}}
        }
    }
    if($addressBox.Items.Count -eq 1){$addressBox.SelectedIndex=0;$addressNote.Text=''}
    elseif($addressBox.Items.Count -gt 1){if($addressBox.Items.Contains($previous)){$addressBox.SelectedItem=$previous};$addressNote.Text='请选择主机地址'}
    else{$addressNote.Text='未检测到已连接的 Radmin 网卡';$statusDetail.Text='请确认 Radmin 在线，然后在“高级工具”中点击“重新检测 IP”。'}
}
$playModeBox.Add_SelectedIndexChanged({Detect-Address})
Detect-Address
if($script:installed -and $script:oldAddress -ne '127.0.0.1' -and -not $addressBox.Items.Contains($script:oldAddress)){$playModeBox.SelectedIndex=2;Detect-Address}
if($script:installed){$sourceBox.Text='已安装';$sourceBox.Enabled=$false;$browse.Enabled=$false;$changeLocation.Enabled=$false;$nameBox.Text=$settings.serverName;$nameBox.Enabled=$false;$startButton.Text='开始开服';$statusTitle.Text='就绪';$statusDetail.Text=''}
elseif(-not $preview){foreach($candidate in @((Join-Path (Split-Path -Parent $packageRoot) 'Server'),(Join-Path $packageRoot 'Server'))){if(Test-Path -LiteralPath (Join-Path $candidate 'BF4BlazeServer.exe')){$sourceBox.Text=$candidate;break}}}
$openPanel.Add_Click({if($script:url){Open-BF4PanelBrowser $script:url|Out-Null}})
$menu=New-Object Windows.Forms.ContextMenuStrip
$recheck=$menu.Items.Add('重新检测 IP');$recheck.Add_Click({Detect-Address})
$copyAddress=$menu.Items.Add('复制 Blaze IP');$copyAddress.Add_Click({if($addressBox.Text){[Windows.Forms.Clipboard]::SetText($addressBox.Text);$statusDetail.Text='Blaze IP 已复制'}})
$openFolder=$menu.Items.Add('打开安装文件夹');$openFolder.Add_Click({if(Test-Path -LiteralPath $script:installRoot){Start-Process explorer.exe -ArgumentList ('"'+$script:installRoot+'"')}})
$logs=$menu.Items.Add('查看本次诊断记录');$logs.Add_Click({if($script:sessionDir){Start-Process explorer.exe -ArgumentList ('"'+$script:sessionDir+'"')}else{$statusDetail.Text='暂无诊断记录'}})
$diagnose=$menu.Items.Add('检测端口');$diagnose.Add_Click({if($script:installed){Start-Process -FilePath (Join-Path $script:installRoot 'advanced\05-Check-Host.cmd') -WorkingDirectory $script:installRoot -WindowStyle Normal}else{$statusDetail.Text='请先完成安装'}})
$restartPanel=$menu.Items.Add('重启管理网页');$restartPanel.Add_Click({if($script:installed){Start-Process -FilePath (Join-Path $script:installRoot 'advanced\08-Restart-Panel.cmd') -WorkingDirectory $script:installRoot -WindowStyle Normal}})
$tools=$menu.Items.Add('打开高级工具文件夹');$tools.Add_Click({$folder=Join-Path $packageRoot 'advanced';if($script:installed){$folder=Join-Path $script:installRoot 'advanced'};Start-Process explorer.exe -ArgumentList ('"'+$folder+'"')})
$advanced.Add_Click({$menu.Show($advanced,(New-Object Drawing.Point(0,$advanced.Height)))})
function Set-Busy($Busy){$startButton.Enabled=-not $Busy;$playModeBox.Enabled=(-not $Busy -and $Mode -eq 'Start');$addressBox.Enabled=(-not $Busy -and $Mode -eq 'Start' -and $playModeBox.SelectedIndex -ne 1);$changeLocation.Enabled=(-not $Busy -and -not $script:installed -and $Mode -eq 'Start');$browse.Enabled=$changeLocation.Enabled;$sourceBox.Enabled=$changeLocation.Enabled;$nameBox.Enabled=$changeLocation.Enabled;$recheck.Enabled=(-not $Busy -and $Mode -eq 'Start');$resetSetup.Enabled=(-not $Busy -and $Mode -eq 'Start')}
function Get-NewInstallRoot {
    $base=Join-Path ([IO.Path]::GetPathRoot($packageRoot)) 'BF4Host'
    for($n=1;$n -le 999;$n++){
        $candidate=$base;if($n -gt 1){$candidate=$base+'-'+$n}
        if(-not (Test-Path -LiteralPath $candidate)){return $candidate}
    }
    throw '请手动选择空的安装位置。'
}
function Reset-Setup {
    if($Mode -ne 'Start' -or ($script:worker -and -not $script:worker.HasExited)){return}
    if($script:previousSetup){
        $previous=$script:previousSetup
        $script:installRoot=$previous.root;$script:oldAddress=$previous.address;$script:url=$previous.url;$script:installed=$true
        $playModeBox.SelectedIndex=$previous.playModeIndex;Detect-Address;$sourceBox.Text=$previous.source;$nameBox.Text=$previous.name;$openPanel.Enabled=$previous.panelEnabled
        $script:previousSetup=$null;$resetSetup.Text='重新设置';$startButton.Text='开始开服';$statusTitle.Text='就绪';$statusDetail.Text=''
    }elseif($script:installed){
        $nextRoot=Get-NewInstallRoot
        $script:previousSetup=[pscustomobject]@{root=$script:installRoot;address=$script:oldAddress;url=$script:url;source=$sourceBox.Text;name=$nameBox.Text;panelEnabled=$openPanel.Enabled;playModeIndex=$playModeBox.SelectedIndex}
        $script:installed=$false;$script:installRoot=$nextRoot;$script:oldAddress=$null;$script:url=$null
        $sourceBox.Clear();$nameBox.Text='BF4 LAN SERVER';$openPanel.Enabled=$false
        $resetSetup.Text='返回';$startButton.Text='一键开服';$statusTitle.Text='重新设置';$statusDetail.Text='将安装到新的目录，原安装保留。'
    }
    $installText.Text=$script:installRoot;Set-Busy $false
}
$resetSetup.Add_Click({try{Reset-Setup}catch{$statusTitle.Text='操作失败';$statusDetail.Text=$_.Exception.Message}})
function Show-UpgradeDialog([string]$TargetVersion) {
    [Windows.Forms.MessageBox]::Show('将备份并更新开服工具，保留服务器设置、禁用规则和玩家存档。旧倍率值超出有效范围时会调整到边界值并记录。更新会重启服务器，是否继续？',('更新到 '+$TargetVersion),'YesNo','Question')
}
function Launch-Worker {
    try{
        if($script:worker -and -not $script:worker.HasExited){return}
        if($Mode -eq 'Start' -and $playModeBox.SelectedIndex -eq 1){Detect-Address}
        if($Mode -eq 'Start' -and -not $addressBox.Text){throw '请选择游玩方式，并填写或选择主机 IPv4。'}
        if($Mode -eq 'Start' -and -not $script:installed -and [string]::IsNullOrWhiteSpace($sourceBox.Text)){throw '请先选择原版补丁的 Server 文件夹。'}
        $allowRestart=$false
        if($Mode -eq 'Stop'){
            if([Windows.Forms.MessageBox]::Show('关闭这套服务器？正在游玩的玩家会断开连接。','关闭服务器','YesNo','Question') -ne 'Yes'){return}
        }elseif($script:installed -and $script:oldAddress -ne $addressBox.Text){
            if([Windows.Forms.MessageBox]::Show('游玩方式或主机地址已改变，需要重启登录服务和游戏服，当前玩家会断开。启动器须使用新地址。是否继续？','更新主机地址','YesNo','Question') -ne 'Yes'){return};$allowRestart=$true
        }
        $node=Get-Node
        $script:sessionDir=New-BF4LauncherSession -TempRoot $launcherTemp.root
        Write-Json (Join-Path $script:sessionDir 'launcher-paths.json') $launcherTemp
        $allowUpgrade=$false
        if($Mode -eq 'Start' -and $script:installed){
            $currentMarker=Read-Json (Join-Path $script:installRoot 'managed-install.json')
            $targetVersion=(Read-Json (Join-Path $packageRoot 'package.json')).version
            if($currentMarker.version -ne $targetVersion){
                if((Show-UpgradeDialog -TargetVersion $targetVersion) -ne 'Yes'){Set-Busy $false;return}
                $allowUpgrade=$true
            }
        }
        $script:progressFile=Join-Path $script:sessionDir 'progress.json';$requestFile=Join-Path $script:sessionDir 'request.json'
        Write-Json $requestFile ([pscustomobject]@{mode=$Mode;packageRoot=$packageRoot;installRoot=$script:installRoot;freshInstall=(-not $script:installed);source=$sourceBox.Text;serverName=$nameBox.Text;playMode=$(if($playModeBox.SelectedIndex -eq 1){'local'}elseif($playModeBox.SelectedIndex -eq 2){'custom'}else{'lan'});address=$addressBox.Text;allowAddressRestart=$allowRestart;allowUpgrade=$allowUpgrade;progressFile=$script:progressFile;logDir=$script:sessionDir})
        $script:opened=$false;$script:lastState='';Set-Busy $true;$statusTitle.Text='正在准备';$statusDetail.Text='';$bar.Value=2
        $script:worker=Start-Process -FilePath $node -ArgumentList ('"'+(Join-Path $PSScriptRoot 'easy-worker.mjs')+'" "'+$requestFile+'"') -WorkingDirectory $packageRoot -WindowStyle Hidden -RedirectStandardOutput (Join-Path $script:sessionDir 'worker.out.log') -RedirectStandardError (Join-Path $script:sessionDir 'worker.err.log') -PassThru
    }catch{Set-Busy $false;$statusTitle.Text='操作失败';$statusDetail.Text=$_.Exception.Message}
}
$startButton.Add_Click({Launch-Worker})
if($Mode -eq 'Stop'){$startButton.Text='关闭服务器';$statusTitle.Text='关闭本安装的服务器';$statusDetail.Text='会停止游戏服、登录服务和管理面板。';$sourceBox.Enabled=$false;$browse.Enabled=$false;$nameBox.Enabled=$false;$playModeBox.Enabled=$false;$addressBox.Enabled=$false;$changeLocation.Enabled=$false}
$timer=New-Object Windows.Forms.Timer;$timer.Interval=350
$timer.Add_Tick({
    if(-not $script:worker){return}
    try{
        if(Test-Path -LiteralPath $script:progressFile){
            $progress=Read-Json $script:progressFile;$signature=$progress|ConvertTo-Json -Compress
            if($signature -ne $script:lastState){$script:lastState=$signature;$statusTitle.Text=$progress.title;$statusDetail.Text=$progress.detail;$bar.Value=[Math]::Max(0,[Math]::Min(100,[int]$progress.percent))}
            if($progress.state -in @('done','waiting','error')){
                Set-Busy $false
                if($Mode -eq 'Stop' -and $progress.state -eq 'done'){$openPanel.Enabled=$false}
                if($progress.url){$script:url=$progress.url;$openPanel.Enabled=$true;if(-not $script:opened){$script:opened=$true;Open-BF4PanelBrowser $script:url|Out-Null}}
                if($progress.state -in @('done','waiting') -and $Mode -eq 'Start'){$script:installed=$true;$script:oldAddress=$progress.address;$sourceBox.Enabled=$false;$browse.Enabled=$false;$nameBox.Enabled=$false;$changeLocation.Enabled=$false;$startButton.Text='开始开服';$statusDetail.Text=$progress.detail+'  Blaze IP：'+$progress.address}
            }
        }
        if($script:worker.HasExited){
            if(-not(Test-Path -LiteralPath $script:progressFile) -or (Read-Json $script:progressFile).state -eq 'running'){$statusTitle.Text='启动程序意外退出';$statusDetail.Text='请打开“高级工具 → 查看本次诊断记录”。';Set-Busy $false}
            if(Test-Path -LiteralPath (Join-Path $script:installRoot 'managed-install.json')){
                $savedSettings=Read-Json (Join-Path $script:installRoot 'settings\host-settings.json')
                $script:installed=$true;$script:oldAddress=$savedSettings.advertiseAddress;$nameBox.Text=$savedSettings.serverName;$sourceBox.Text='已安装';$nameBox.Enabled=$false;$sourceBox.Enabled=$false;$browse.Enabled=$false;$changeLocation.Enabled=$false
                $script:previousSetup=$null;$resetSetup.Text='重新设置';$resetSetup.Visible=($Mode -eq 'Start');$startButton.Text=if($Mode -eq 'Start'){'开始开服'}else{'关闭服务器'}
            }
            $script:worker.Dispose();$script:worker=$null
        }
    }catch{}
})
$form.Add_FormClosing({param($sender,$eventArgs) if($script:worker -and -not $script:worker.HasExited){$eventArgs.Cancel=$true;$statusDetail.Text='当前操作尚未结束，请等待完成后关闭窗口。'}})
if($UiTestReport){
    . (Join-Path $packageRoot 'tests\wizard-ui.ps1') -ReportPath $UiTestReport
    $timer.Dispose();$form.Dispose();exit 0
}
if($SnapshotPath){
    $form.Show();[Windows.Forms.Application]::DoEvents();$bitmap=New-Object Drawing.Bitmap($form.Width,$form.Height);$form.DrawToBitmap($bitmap,(New-Object Drawing.Rectangle(0,0,$form.Width,$form.Height)));$bitmap.Save($SnapshotPath,[Drawing.Imaging.ImageFormat]::Png);$bitmap.Dispose();$form.Close();$form.Dispose();exit 0
}
$timer.Start();[void]$form.ShowDialog();$timer.Stop();$timer.Dispose();$form.Dispose()
