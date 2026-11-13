function Show-HostFolderDialog($Dialog,$Owner) {
    return $Dialog.ShowDialog($Owner)
}

function Select-HostFolder {
    param([AllowEmptyString()][string]$CurrentPath,[string]$Description,$Owner,[bool]$AllowNewFolder=$false)
    $dialog=New-Object Windows.Forms.FolderBrowserDialog
    try {
        $dialog.Description=$Description
        $dialog.ShowNewFolderButton=$AllowNewFolder
        $initial=([string]$CurrentPath).Trim().Trim('"')
        if(-not [string]::IsNullOrWhiteSpace($initial) -and (Test-Path -LiteralPath $initial -PathType Container -ErrorAction SilentlyContinue)) {
            $dialog.SelectedPath=$initial
        }
        if((Show-HostFolderDialog $dialog $Owner) -eq [Windows.Forms.DialogResult]::OK) {
            $selected=[string]$dialog.SelectedPath
            if(-not [string]::IsNullOrWhiteSpace($selected) -and (Test-Path -LiteralPath $selected -PathType Container -ErrorAction SilentlyContinue)) {return $selected}
            throw '所选文件夹不存在，请重新选择。'
        }
        return $null
    } finally {$dialog.Dispose()}
}
