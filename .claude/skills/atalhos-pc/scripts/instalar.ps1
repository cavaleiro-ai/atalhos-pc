# Instala o programa no PC (pelo Instalar-AtalhosPC.exe, em modo silencioso) e o app no aparelho a partir de assets\,
# conferindo os hashes. Nunca apaga dados do usuario.
param(
    [string]$Serial = "",
    [string]$Adb = "",
    [string]$Destino = "$env:LOCALAPPDATA\Programs\AtalhosPC",
    [switch]$SoPc,
    [switch]$SoAparelho,
    [switch]$NaoAbrir,
    [switch]$Substituir,
    [string]$Biblioteca = "",
    [switch]$SoBiblioteca
)
$ErrorActionPreference = "Stop"
. (Join-Path $PSScriptRoot "comum.ps1")
Show-Mensagem

$assets = Join-Path (Get-SkillRoot) "assets"
"Conferindo os arquivos da skill"
foreach ($linha in (Get-Content -LiteralPath (Join-Path $assets "SHA256SUMS.txt") -Encoding UTF8)) {
    if (-not $linha.Trim()) { continue }
    $esperado, $nome = $linha -split "  ", 2
    $real = (Get-FileHash -LiteralPath (Join-Path $assets $nome) -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($real -ne $esperado) { throw "Arquivo alterado ou corrompido: $nome. Pare e avise o usuario." }
}
"Hashes ok"

# A skill nao traz os audios da biblioteca: copia de uma pasta do usuario, com uma subpasta por categoria. O original nao muda.
# Usado so por -SoBiblioteca, que nao fecha o programa; na instalacao completa quem copia e o instalador.
function Copy-Biblioteca {
    if (-not (Test-Path -LiteralPath $Biblioteca -PathType Container)) { throw "Pasta da biblioteca nao encontrada: $Biblioteca" }
    $alvoBib = Join-Path $Destino "biblioteca"
    New-Item -ItemType Directory -Force -Path $alvoBib | Out-Null
    $copiados = 0
    foreach ($pasta in Get-ChildItem -LiteralPath $Biblioteca -Directory) {
        & robocopy $pasta.FullName (Join-Path $alvoBib $pasta.Name) *.mp3 *.wav *.wma *.m4a *.aac /E /R:1 /W:1 /NFL /NDL /NJH /NJS /NP | Out-Null
        if ($LASTEXITCODE -ge 8) { throw "Falha ao copiar a categoria $($pasta.Name)" }
        $global:LASTEXITCODE = 0
        $copiados += @(Get-ChildItem -LiteralPath (Join-Path $alvoBib $pasta.Name) -File).Count
    }
    Copy-Item -Path (Join-Path $assets "icones-biblioteca\*.png") -Destination $alvoBib -Force
    "Biblioteca em ${alvoBib}: $copiados arquivos. Abra Biblioteca na aba Sons para conferir."
}

if ($SoBiblioteca) {
    if (-not $Biblioteca) { throw "Informe a pasta com -Biblioteca." }
    Copy-Biblioteca
    return
}

if (-not $SoAparelho) {
    "`n=== PC: $Destino"
    $aberto = Get-Process AtalhosPC -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($aberto -and -not $Substituir) {
        throw "O programa ja esta aberto em $($aberto.Path). Pergunte ao usuario se pode fechar e rode de novo com -Substituir."
    }
    $config = Join-Path $env:APPDATA "AtalhosPC\config.json"
    if (Test-Path -LiteralPath $config) {
        $copia = "$config.antes-$(Get-Date -Format yyyyMMdd-HHmmss)"
        Copy-Item -LiteralPath $config -Destination $copia
        "Copia de seguranca da configuracao: $copia"
    }
    if ($Biblioteca -and -not (Test-Path -LiteralPath $Biblioteca -PathType Container)) { throw "Pasta da biblioteca nao encontrada: $Biblioteca" }
    # O instalador fecha o programa aberto sozinho (pelo sinal, sem forcar), cria os atalhos e a entrada em Aplicativos.
    $argumentos = @("/silencioso", "/pasta=`"$Destino`"")
    if ($Biblioteca) { $argumentos += "/biblioteca=`"$Biblioteca`"" }
    if (-not $NaoAbrir) { $argumentos += "/abrir" }
    # Start-Process -Wait esperaria tambem o programa aberto pelo /abrir: espera so o instalador.
    $proc = Start-Process -FilePath (Join-Path $assets "Instalar-AtalhosPC.exe") -ArgumentList ($argumentos -join " ") -PassThru
    $null = $proc.Handle
    $proc.WaitForExit()
    if ($proc.ExitCode -ne 0) {
        throw "O instalador falhou (codigo $($proc.ExitCode)). Motivo em $env:TEMP\AtalhosPC-instalador.log"
    }
    if (-not (Test-Path -LiteralPath (Join-Path $Destino "AtalhosPC.exe"))) { throw "O instalador terminou, mas o programa nao esta em $Destino" }
    "Instalado em $Destino, com atalhos na area de trabalho e no menu Iniciar e entrada em Aplicativos para desinstalar."
    if ($Biblioteca) { "Biblioteca copiada de $Biblioteca. Abra Biblioteca na aba Sons para conferir." }
    if (-not $NaoAbrir) { "Programa aberto. Se o Windows perguntar sobre a rede, o usuario precisa clicar em Permitir acesso." }
}

if (-not $SoPc) {
    "`n=== APARELHO"
    $adbExe = Find-Adb -Informado $Adb
    $apk = Join-Path $assets "AtalhosPC.apk"
    if (-not $adbExe) {
        "adb nao encontrado. Copie para o aparelho e instale pelo app de arquivos: $apk"
        return
    }
    $alvo = Resolve-Serial -Adb $adbExe -Serial $Serial
    if (-not $alvo) {
        "Aparelho indefinido (nenhum ou mais de um). Rode de novo com -Serial, ou instale manualmente: $apk"
        return
    }
    $ErrorActionPreference = "Continue"
    $saida = (& $adbExe -s $alvo install -r $apk 2>&1 | ForEach-Object { "$_" }) -join " "
    if ($saida -match "Success") {
        "App instalado em $alvo"
        # a instalacao fecha o app; reabrir deixa o aparelho pronto para o proximo passo
        & $adbExe -s $alvo shell "am start -n com.cavaleiro.atalhospc/.MainActivity" | Out-Null
    } elseif ($saida -match "INSTALL_FAILED_UPDATE_INCOMPATIBLE|signatures") {
        "Assinatura diferente da versao instalada. Nao desinstalei: isso apagaria o pareamento. Pergunte ao usuario antes."
    } else {
        "Instalacao recusada: $saida"
        "Peca ao usuario para aprovar na tela do aparelho, ou instale manualmente: $apk"
    }
}
