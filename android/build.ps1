# Gera o APK sem Gradle: javac + d8 + aapt2 + zipalign + apksigner.
# As ferramentas ficam em android\tools. Se faltarem, setup-tools.ps1 baixa tudo das fontes oficiais.
param([string]$Tools = "")
# "Continue" de proposito: javac e d8 escrevem avisos no stderr, e cada passo ja confere $LASTEXITCODE.
$ErrorActionPreference = "Continue"
$base = $PSScriptRoot
$root = Split-Path $base
$build = "$base\build"
$minSdk = 21
$targetSdk = 34

if (-not $Tools) {
  $Tools = "$base\tools"
  # reaproveita o que ja existir ao lado (projetos irmaos neste mesmo tablet\)
  $irmao = Join-Path (Split-Path $root) "gamepad-app\tools"
  if (-not (Test-Path "$Tools\jdk-x") -and (Test-Path "$irmao\jdk-x")) { $Tools = $irmao }
}
if (-not (Test-Path "$Tools\jdk-x")) { & "$base\setup-tools.ps1" -Tools $Tools }

function Find-Tool($dir, $name) {
  $f = Get-ChildItem "$Tools\$dir" -Filter $name -Recurse | Select-Object -First 1
  if (-not $f) { throw "Nao achei $name em $Tools\$dir" }
  return $f.FullName
}
$javac = Find-Tool "jdk-x" "javac.exe"
$keytool = Find-Tool "jdk-x" "keytool.exe"
$jar = Find-Tool "jdk-x" "jar.exe"
$aapt2 = Find-Tool "bt-x" "aapt2.exe"
$d8 = Find-Tool "bt-x" "d8.bat"
$zipalign = Find-Tool "bt-x" "zipalign.exe"
$apksigner = Find-Tool "bt-x" "apksigner.bat"
$androidJar = Find-Tool "plat-x" "android.jar"
$jdkBin = Split-Path $javac
$env:JAVA_HOME = Split-Path $jdkBin
$env:PATH = "$jdkBin;$env:PATH"

Remove-Item "$build" -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force -Path "$build\classes", "$build\dex" | Out-Null

Write-Host "=== 1/6 recursos (aapt2 compile) ==="
& $aapt2 compile --dir "$base\src\res" -o "$build\res.zip"
if ($LASTEXITCODE -ne 0) { throw "aapt2 compile falhou" }

Write-Host "=== 2/6 javac ==="
$fontes = Get-ChildItem "$base\src\com" -Filter *.java -Recurse | ForEach-Object { $_.FullName }
& $javac -encoding UTF-8 -source 8 -target 8 -Xlint:-options -cp $androidJar -d "$build\classes" @fontes
if ($LASTEXITCODE -ne 0) { throw "javac falhou" }

Write-Host "=== 3/6 d8 (dex) ==="
& $jar cf "$build\classes.jar" -C "$build\classes" .
if ($LASTEXITCODE -ne 0) { throw "jar falhou" }
& $d8 --release --min-api $minSdk --lib $androidJar --output "$build\dex" "$build\classes.jar"
if ($LASTEXITCODE -ne 0) { throw "d8 falhou" }

Write-Host "=== 4/6 aapt2 link ==="
& $aapt2 link -I $androidJar --manifest "$base\src\AndroidManifest.xml" -o "$build\base.apk" --min-sdk-version $minSdk --target-sdk-version $targetSdk "$build\res.zip"
if ($LASTEXITCODE -ne 0) { throw "aapt2 link falhou" }

Write-Host "=== 5/6 classes.dex no APK ==="
Add-Type -AssemblyName System.IO.Compression.FileSystem
Add-Type -AssemblyName System.IO.Compression
$zip = [System.IO.Compression.ZipFile]::Open("$build\base.apk", [System.IO.Compression.ZipArchiveMode]::Update)
try {
  [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip, "$build\dex\classes.dex", "classes.dex") | Out-Null
} finally { $zip.Dispose() }

Write-Host "=== 6/6 zipalign + assinatura ==="
# A chave fica fora de build\: atualizar o app exige a mesma assinatura. Nao publique este arquivo.
$keystore = "$base\atalhospc.keystore"
if (-not (Test-Path $keystore)) {
  $senha = -join ((48..57) + (97..122) | Get-Random -Count 24 | ForEach-Object { [char]$_ })
  Set-Content -Path "$base\atalhospc.keystore.senha" -Value $senha -Encoding ascii -NoNewline
  & $keytool -genkeypair -keystore $keystore -storepass $senha -keypass $senha -alias atalhos -keyalg RSA -keysize 2048 -validity 10000 -dname "CN=Atalhos PC"
  if ($LASTEXITCODE -ne 0) { throw "keytool falhou" }
}
$senhaArquivo = "$base\atalhospc.keystore.senha"
& $zipalign -f 4 "$build\base.apk" "$build\aligned.apk"
if ($LASTEXITCODE -ne 0) { throw "zipalign falhou" }
& $apksigner sign --ks $keystore --ks-pass "file:$senhaArquivo" --out "$build\AtalhosPC.apk" "$build\aligned.apk"
if ($LASTEXITCODE -ne 0) { throw "apksigner falhou" }

New-Item -ItemType Directory -Force -Path "$root\dist" | Out-Null
Copy-Item "$build\AtalhosPC.apk" "$root\dist\AtalhosPC.apk" -Force
Write-Host ("OK: $root\dist\AtalhosPC.apk ({0:N0} KB)" -f ((Get-Item "$root\dist\AtalhosPC.apk").Length / 1KB))
