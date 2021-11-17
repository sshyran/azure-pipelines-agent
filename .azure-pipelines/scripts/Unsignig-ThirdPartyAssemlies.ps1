[CmdletBinding()]
param()

. $PSScriptRoot\Get-SigntoolPath.ps1
. $PSScriptRoot\Remove-ThirdPartySignatures.ps1

$signtoolPath = Get-Signtool
if ( -not [string]::IsNullOrEmpty($signtoolPath)) {
    Remove-ThirdPartySignatures -SigntoolPath $signToolPath
}
