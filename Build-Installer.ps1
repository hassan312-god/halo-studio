$ErrorActionPreference = 'Stop'
& "$PSScriptRoot\Compiler.ps1"
$versionMatch = Select-String -LiteralPath "$PSScriptRoot\BuildInfo.cs" -Pattern 'Version="([^"]+)"'
$version = $versionMatch.Matches[0].Groups[1].Value
$buildDir = Join-Path $PSScriptRoot 'build'
New-Item -ItemType Directory -Path $buildDir -Force | Out-Null
$payload = Join-Path $buildDir 'payload.zip'
if (Test-Path -LiteralPath $payload) { Remove-Item -LiteralPath $payload }
Compress-Archive -LiteralPath @("$PSScriptRoot\Halo.exe", "$PSScriptRoot\Halo.ico", "$PSScriptRoot\logo.png", "$PSScriptRoot\LIRE-MOI.txt") -DestinationPath $payload
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$installer = Join-Path $buildDir "HaloStudio-Setup-$version.exe"
& $compiler /nologo /target:winexe /optimize+ /platform:anycpu /reference:System.Drawing.dll /reference:System.Windows.Forms.dll /reference:System.IO.Compression.dll /reference:Microsoft.CSharp.dll /win32icon:"$PSScriptRoot\Halo.ico" /win32manifest:"$PSScriptRoot\app.manifest" /resource:"$payload,Halo.Payload" /resource:"$PSScriptRoot\Halo.ico,Halo.Icon" /resource:"$PSScriptRoot\logo.png,Halo.Logo" /out:"$installer" "$PSScriptRoot\Installer.cs" "$PSScriptRoot\BuildInfo.cs" "$PSScriptRoot\AssemblyInfo.cs"
if ($LASTEXITCODE -ne 0) { throw "Compilation de l'installateur échouée." }
$hash = (Get-FileHash -LiteralPath $installer -Algorithm SHA256).Hash.ToLowerInvariant()
[IO.File]::WriteAllText("$installer.sha256", "$hash  $([IO.Path]::GetFileName($installer))`n", [Text.Encoding]::ASCII)
Write-Output $installer
