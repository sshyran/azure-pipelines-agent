function Get-Signtool() {
    $ErrorActionPreference = 'Stop'

    [System.IntPtr]::Size # an integer with platform-specific size.
    $systemBit = "x64"
    $programFiles = ${Env:ProgramFiles(x86)}

    if([System.IntPtr]::Size -eq 4){
        $systemBit = "x86"
        $programFiles = ${Env:ProgramFiles}
    }

    try {
        $windowsSdkPath=Get-ChildItem "$programFiles\Windows Kits\10\bin\1*" | Select-Object FullName | Sort-Object -Descending { [version](Split-Path $_.FullName -leaf)} | Select-Object -first 1

        $signtoolPath = "$($windowsSdkPath.FullName)\x$systemBit\signtool"
        return $signtoolPath
    } catch {
        Write-Warning "Unbable to get signtool"
        return ""
    }
}
