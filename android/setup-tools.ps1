# Baixa o necessario para compilar o APK (cerca de 310 MB) em android\tools e confere cada arquivo pelo hash oficial.
param([string]$Tools = "$PSScriptRoot\tools")
$ErrorActionPreference = "Stop"
$ProgressPreference = "SilentlyContinue"
$pacotes = @(
  @{ Pasta = "jdk-x"; Arq = "jdk17.zip"; Algo = "SHA256"; Hash = "e53a79c3c3d86865bd7e787903884331068e71321714ffd44f145785affc7cb0"
     Url = "https://github.com/adoptium/temurin17-binaries/releases/download/jdk-17.0.20.1%2B1/OpenJDK17U-jdk_x64_windows_hotspot_17.0.20.1_1.zip" },
  @{ Pasta = "bt-x"; Arq = "build-tools.zip"; Algo = "SHA1"; Hash = "62cfde1b6fcc3ad12a4d2ba1b537e752768bfd47"
     Url = "https://dl.google.com/android/repository/build-tools_r34-windows.zip" },
  @{ Pasta = "plat-x"; Arq = "platform.zip"; Algo = "SHA1"; Hash = "1f2e9478d6a7601425ceaa553311dc43191f103d"
     Url = "https://dl.google.com/android/repository/platform-34-ext7_r03.zip" }
)
New-Item -ItemType Directory -Force -Path $Tools | Out-Null
foreach ($p in $pacotes) {
  $destino = Join-Path $Tools $p.Pasta
  if (Test-Path $destino) { Write-Host "ja existe: $($p.Pasta)"; continue }
  $zip = Join-Path $Tools $p.Arq
  Write-Host "baixando $($p.Url)"
  [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
  Invoke-WebRequest -UseBasicParsing -Uri $p.Url -OutFile $zip
  $real = (Get-FileHash $zip -Algorithm $p.Algo).Hash.ToLowerInvariant()
  if ($real -ne $p.Hash) {
    Remove-Item $zip -Force
    throw "Hash nao confere em $($p.Arq): veio $real, esperado $($p.Hash)"
  }
  Write-Host "hash ok, extraindo"
  Expand-Archive -Path $zip -DestinationPath $destino -Force
  Remove-Item $zip -Force
}
Write-Host "Ferramentas prontas em $Tools"
