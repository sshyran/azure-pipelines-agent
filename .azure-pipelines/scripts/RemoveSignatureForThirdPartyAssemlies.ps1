[CmdletBinding()]
param(
  [Parameter(Mandatory = $false)]
  [string]$LayoutRoot
)

. $PSScriptRoot\Get-SigntoolPath.ps1
. $PSScriptRoot\RemoveSignatureScript.ps1

if ( ($signToolPath -ne "") -and (Test-Path -Path $signtoolPath) ) {
  Remove-ThirdPartySignatures -SigntoolPath "$signToolPath"
} else {
	Write-Error "$signToolPath is not a valid path"
	exit 1
}
