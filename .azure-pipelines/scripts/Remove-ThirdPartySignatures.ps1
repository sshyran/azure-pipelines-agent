function Remove-ThirdPartySignatures() {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string]$SigntoolPath)

    $ErrorActionPreference = 'Stop'
    foreach ($tree in Get-ChildItem -Path '${{ parameters.layoutRoot }}/bin' -Filter "*.dll" -Recurse | select FullName) {
        try {
        & "$SigntoolPath" remove /s /q "$($tree.FullName)" 2>&1
        if ($lastexitcode -ne 0) {
            $failedToUnsign.Add("$($tree.FullName)")
        } else {
            $succesfullyUnsigned.Add("$($tree.FullName)")
        }
        } catch {
        $failedToUnsign.Add("$($tree.FullName)")
        }
    }
    foreach ($f in $failedToUnsign) {
        Write-Warning "Something went wrong, failed to process $f file in catch"
    }
    foreach ($s in $success) {
        Write-Host "Signature succefully removed for $s file"
    }
}
