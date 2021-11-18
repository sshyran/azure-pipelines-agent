[CmdletBinding()]
param(
  [Parameter(Mandatory = $false)]
  [string]$LayoutRoot
)

. $PSScriptRoot\Get-SigntoolPath.ps1
. $PSScriptRoot\RemoveSignatureScript.ps1

$signtoolPath = Get-Signtool
if ( $signtoolPath -ne "" ) {
  Remove-ThirdPartySignatures -SigntoolPath "$signToolPath" -LayoutRoot "$LayoutRoot"
}
