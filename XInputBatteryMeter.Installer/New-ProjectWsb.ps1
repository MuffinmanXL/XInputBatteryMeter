param(
  [switch]$StartAddRemovePrograms,
  [switch]$StartServices,
  [switch]$StartExplorerInProject,
  [switch]$StartExplorerInProgramFiles,
  [switch]$StartExplorerInProgramFilesX86,
  [switch]$StartRegedit
)


function New-ProjectWsb {
  if ($StartAddRemovePrograms -eq $false -and $StartServices -eq $false -and $StartExplorerInProject -eq $false -and $StartExplorerInProgramFiles -eq $false -and $StartExplorerInProgramFilesX86 -eq $false -and $StartRegedit -eq $false) {
    $StartAddRemovePrograms = $true
    $StartServices = $false
    $StartExplorerInProject = $true
    $StartExplorerInProgramFiles = $true
    $StartExplorerInProgramFilesX86 = $false
    $StartRegedit = $false
  }

  if (!(Get-Command git.exe -ErrorAction SilentlyContinue)) {
    Write-Error 'Git command not found. Please install Git for Windows.'
    return
  }

  if (!(git rev-parse --is-inside-work-tree)) {
    Write-Error 'Please run this script from the XInputBatteryMeter repository.'
    return
  }

  $repoRoot = (Get-Item -Path $(git rev-parse --show-toplevel)).FullName
  $slnFile = Join-Path $repoRoot 'XInputBatteryMeter.sln'

  if (!(Test-Path -Path $slnFile)) {
    Write-Error 'Please run this script from the XInputBatteryMeter repository.'
    return
  }

  $projectPath = (Get-Item -Path $PSScriptRoot).FullName
  $projectRelativePath = [System.IO.Path]::GetRelativePath($repoRoot, $projectPath)
  $projectName = (Get-Item -Path $projectPath).Name
  
  $wsbFilename = "$projectName.wsb"
  $wsbFileFullPath = Join-Path $projectPath $wsbFilename
  
  $logonScriptFilename = "$wsbFilename.start.cmd"
  $logonScriptFilePath = Join-Path $projectRelativePath $logonScriptFilename
  $logonScriptFileFullPath = Join-Path $projectPath $logonScriptFilename

  @"
<Configuration>
  <MappedFolders>
    <MappedFolder>
      <HostFolder>$repoRoot</HostFolder>
      <SandboxFolder>C:\XInputBatteryMeter</SandboxFolder>
    </MappedFolder>
  </MappedFolders>
  <LogonCommand>
    <Command>C:\XInputBatteryMeter\$logonScriptFilePath</Command>
  </LogonCommand>
</Configuration>
"@ | Out-File -FilePath $wsbFileFullPath -Encoding UTF8

  Write-Host "$wsbFileFullPath created."

  $LogonCommand = ''
  if ($StartAddRemovePrograms) {
    $LogonCommand += "start appwiz.cpl`n"
  }
  if ($StartServices) {
    $LogonCommand += "start services.msc`n"
  }
  if ($StartExplorerInProject) {
    $LogonCommand += "start `"`" `"$([System.IO.Path]::GetFullPath((Join-Path 'C:\XInputBatteryMeter\' $projectRelativePath)))`" `n"
  }
  if ($StartExplorerInProgramFiles) {
    $LogonCommand += "start `"`" `"C:\Program Files`"`n"
  }
  if ($StartExplorerInProgramFilesX86) {
    $LogonCommand += "start `"`" `"C:\Program Files (x86)`"`n"
  }
  if ($StartRegedit) {
    $LogonCommand += "start regedit`n"
  }

  $LogonCommand | Out-File -FilePath $logonScriptFileFullPath -Encoding UTF8

  Write-Host "$logonScriptFileFullPath created."
}

New-ProjectWsb