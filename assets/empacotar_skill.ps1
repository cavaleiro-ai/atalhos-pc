# Atualiza a skill com a versao atual: programa, instalador, app, sons, documentacao e codigo-fonte. Rode depois de cada compilacao.
# Nunca inclui a chave de assinatura do app.
$ErrorActionPreference = "Stop"
$root = Split-Path $PSScriptRoot
$skill = Join-Path $root ".claude\skills\atalhos-pc"
$assets = Join-Path $skill "assets"
$refs = Join-Path $skill "references"

New-Item -ItemType Directory -Force -Path (Join-Path $assets "sons"), (Join-Path $assets "icones-biblioteca"), $refs | Out-Null
# Da biblioteca vao so os icones: os audios sao de terceiros e tem 1,6 GB.
Copy-Item -Path (Join-Path $root "assets\icones-biblioteca\*.png") -Destination (Join-Path $assets "icones-biblioteca") -Force
Copy-Item -LiteralPath (Join-Path $root "dist\AtalhosPC.exe") -Destination $assets -Force
Copy-Item -LiteralPath (Join-Path $root "dist\Instalar-AtalhosPC.exe") -Destination $assets -Force
Copy-Item -LiteralPath (Join-Path $root "dist\AtalhosPC.apk") -Destination $assets -Force
Copy-Item -Path (Join-Path $root "dist\sons\*") -Destination (Join-Path $assets "sons") -Force
Copy-Item -LiteralPath (Join-Path $root "docs\GUIA-DE-USO.md") -Destination $refs -Force
Copy-Item -LiteralPath (Join-Path $root "docs\COMO-FUNCIONA.md") -Destination $refs -Force

# Codigo-fonte: so o que esta na lista, para nada privado entrar por acidente.
$temp = Join-Path $env:TEMP ("atalhos-fonte-" + [guid]::NewGuid().ToString("N"))
foreach ($item in "android\src", "android\build.ps1", "android\setup-tools.ps1", "windows\src", "windows\setup", "windows\tests", "windows\build.ps1", "windows\app.ico", "assets\make_icons.py", "assets\baixar_sons_exemplo.py", "assets\baixar_icones_biblioteca.py", "assets\icones-biblioteca", "assets\empacotar_skill.ps1", "assets\logo.png", "docs", "README.md", ".gitignore") {
    $destino = Join-Path $temp $item
    New-Item -ItemType Directory -Force -Path (Split-Path $destino) | Out-Null
    Copy-Item -LiteralPath (Join-Path $root $item) -Destination $destino -Recurse -Force
}
$bad = Get-ChildItem -Recurse -Force $temp | Where-Object { $_.Name -match 'keystore|\.senha$' }
if ($bad) { throw "Chave de assinatura entrou no pacote: $($bad.FullName)" }
$zip = Join-Path $assets "codigo-fonte.zip"
if (Test-Path -LiteralPath $zip) { Remove-Item -LiteralPath $zip -Force }
Add-Type -AssemblyName System.IO.Compression.FileSystem
[IO.Compression.ZipFile]::CreateFromDirectory($temp, $zip)
Remove-Item -LiteralPath $temp -Recurse -Force

# Lista de hashes conferida pelo instalar.ps1
$linhas = Get-ChildItem -Recurse -File $assets | Where-Object { $_.Name -ne "SHA256SUMS.txt" } | Sort-Object FullName | ForEach-Object {
    $rel = $_.FullName.Substring($assets.Length + 1)
    "{0}  {1}" -f (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant(), $rel
}
[IO.File]::WriteAllLines((Join-Path $assets "SHA256SUMS.txt"), [string[]]$linhas, (New-Object Text.UTF8Encoding($false)))
"Skill atualizada: $($linhas.Count) arquivos, {0:N1} MB" -f ((Get-ChildItem -Recurse -File $skill | Measure-Object Length -Sum).Sum / 1MB)
