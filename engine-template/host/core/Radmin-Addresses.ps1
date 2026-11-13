$ErrorActionPreference='Stop'
[Console]::OutputEncoding=New-Object Text.UTF8Encoding($false)
$addresses=@([Net.NetworkInformation.NetworkInterface]::GetAllNetworkInterfaces()|ForEach-Object{
    $adapter=$_
    $_.GetIPProperties().UnicastAddresses|Where-Object{$_.Address.AddressFamily -eq 'InterNetwork'}|ForEach-Object{
        [pscustomobject]@{name=$adapter.Name;description=$adapter.Description;up=($adapter.OperationalStatus -eq 'Up');address=$_.Address.IPAddressToString}
    }
})
ConvertTo-Json -InputObject $addresses -Compress
