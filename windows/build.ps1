# Compila o AtalhosPC.exe, o instalador e os testes com o compilador que ja vem no Windows (.NET Framework 4.x).
# Nao precisa instalar nada. O app Android (dist\AtalhosPC.apk) precisa existir antes, porque vai dentro do instalador.
$ErrorActionPreference = "Stop"
$base = $PSScriptRoot
$root = Split-Path $base
$csc = "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if (-not (Test-Path $csc)) { $csc = "$env:WINDIR\Microsoft.NET\Framework\v4.0.30319\csc.exe" }
if (-not (Test-Path $csc)) { throw "csc.exe nao encontrado: instale o .NET Framework 4.5 ou mais novo" }

$dist = "$root\dist"
New-Item -ItemType Directory -Force -Path $dist, "$base\build" | Out-Null
$refs = @("System.dll", "System.Core.dll", "System.Drawing.dll", "System.Windows.Forms.dll", "System.Security.dll", "System.Web.Extensions.dll") | ForEach-Object { "/r:$_" }
$common = @("/nologo", "/codepage:65001", "/optimize+", "/warn:4") + $refs

# Sair do AtalhosPC.exe em execucao antes de recompilar, senao o Windows nao deixa substituir o arquivo.
& $csc @common /target:winexe "/win32icon:$base\app.ico" "/out:$dist\AtalhosPC.exe" "$base\src\*.cs"
if ($LASTEXITCODE -ne 0) { throw "csc falhou" }

# O teste usa as mesmas fontes, menos o Main.
$fontes = "Proto", "Store", "Shell", "Bluetooth", "Sound", "Server", "Library", "Sys", "MainForm", "LibraryForm", "AppInfo", "Greeting" | ForEach-Object { "$base\src\$_.cs" }
& $csc @common /target:exe "/out:$base\build\ProtocolTest.exe" "$base\tests\ProtocolTest.cs" @fontes
if ($LASTEXITCODE -ne 0) { throw "csc do teste falhou" }

# Instalador: um exe so, com o programa, o app e os sons de exemplo dentro.
# A biblioteca (1,6 GB, de terceiros, sem licenca conferida) nao entra: o instalador copia de uma pasta, se o usuario quiser.
$apk = "$dist\AtalhosPC.apk"
if (-not (Test-Path $apk)) { throw "Falta $apk. Compile o app antes com android\build.ps1, ou copie o AtalhosPC.apk da pasta assets da skill." }
if (-not (Test-Path "$dist\sons\*.mp3")) { throw "Falta $dist\sons. Copie a pasta assets\sons da skill, ou rode assets\baixar_sons_exemplo.py." }
$payload = @("/resource:$dist\AtalhosPC.exe,payload/AtalhosPC.exe", "/resource:$apk,payload/AtalhosPC.apk")
foreach ($f in Get-ChildItem "$dist\sons" -File) { $payload += "/resource:$($f.FullName),payload/sons/$($f.Name)" }
foreach ($f in Get-ChildItem "$root\assets\icones-biblioteca" -Filter *.png) { $payload += "/resource:$($f.FullName),icones-biblioteca/$($f.Name)" }
$payload += "/resource:$root\assets\logo.png,logo.png"
$setup = "$dist\Instalar-AtalhosPC.exe"
& $csc @common /target:winexe "/win32icon:$base\app.ico" "/out:$setup" @payload "$base\setup\*.cs" "$base\src\AppInfo.cs" "$base\src\Greeting.cs"
if ($LASTEXITCODE -ne 0) { throw "csc do instalador falhou" }

# O teste do instalador usa o proprio instalador como biblioteca, entao ele precisa estar ao lado.
Copy-Item -LiteralPath $setup -Destination "$base\build\" -Force
& $csc @common /target:exe "/r:$base\build\Instalar-AtalhosPC.exe" "/out:$base\build\InstaladorTest.exe" "$base\tests\InstallerTest.cs"
if ($LASTEXITCODE -ne 0) { throw "csc do teste do instalador falhou" }

Write-Host ("OK: $dist\AtalhosPC.exe ({0:N0} KB)" -f ((Get-Item "$dist\AtalhosPC.exe").Length / 1KB))
Write-Host ("OK: $setup ({0:N0} KB)" -f ((Get-Item $setup).Length / 1KB))
