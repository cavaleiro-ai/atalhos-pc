# Dirige o aparelho pelo texto do que esta na tela, sem depender de coordenadas, que mudam de um modelo para outro.
# Exemplos: tocar_ui.ps1 -Listar | tocar_ui.ps1 -Texto "Sons" | tocar_ui.ps1 -Texto "Cachorro latindo" -Longo | tocar_ui.ps1 -Print C:\temp\a.png
param([string]$Texto = "", [switch]$Longo, [switch]$Listar, [string]$Print = "", [string]$Serial = "", [string]$Adb = "")
$ErrorActionPreference = "Continue"
. (Join-Path $PSScriptRoot "comum.ps1")
Show-Mensagem
try { [Console]::OutputEncoding = [Text.Encoding]::UTF8 } catch { }
$OutputEncoding = [Text.Encoding]::UTF8
if (-not $Listar -and -not $Print -and -not $Texto) { "Informe -Texto, -Listar ou -Print."; exit 2 }
$adbExe = Find-Adb -Informado $Adb
if (-not $adbExe) { "adb nao encontrado."; exit 3 }
$alvo = Resolve-Serial -Adb $adbExe -Serial $Serial
if (-not $alvo) { "Aparelho indefinido: use -Serial."; exit 3 }

if ($Print) {
    # adb exec-out redirecionado pelo PowerShell corrompe PNG: grava no aparelho e puxa
    & $adbExe -s $alvo shell "screencap -p /data/local/tmp/atalhos-print.png"
    & $adbExe -s $alvo pull /data/local/tmp/atalhos-print.png $Print | Out-Null
    & $adbExe -s $alvo shell "rm -f /data/local/tmp/atalhos-print.png"
    "Print: $Print"
    exit 0
}

$nos = @(Get-UiNodes -Adb $adbExe -Serial $alvo)
if ($nos.Count -eq 0) { "Nao consegui ler a tela. Ela esta ligada e desbloqueada?"; exit 4 }

if ($Listar) {
    foreach ($n in $nos) {
        $t = if ($n.text) { $n.text } else { $n.GetAttribute("content-desc") }
        if ($t) { "$t  $($n.bounds)" }
    }
    exit 0
}

# Primeiro o texto exato; depois parte do texto, porque acentos podem se perder na linha de comando.
$achado = $null
$exatos = @($nos | Where-Object { $_.text -eq $Texto -or $_.GetAttribute("content-desc") -eq $Texto })
# O nome do som selecionado tambem aparece como legenda do fader: prefere o que aceita toque.
$clicaveis = @($exatos | Where-Object { $_.clickable -eq "true" })
if ($clicaveis.Count -gt 0) { $achado = $clicaveis[0] } elseif ($exatos.Count -gt 0) { $achado = $exatos[0] }
if (-not $achado) {
    $parciais = @($nos | Where-Object { "$($_.text)|$($_.GetAttribute('content-desc'))".Contains($Texto) })
    if ($parciais.Count -gt 1) {
        "Mais de um item contem '$Texto': seja mais especifico para nao tocar no errado."
        foreach ($p in $parciais) { "  $($p.text)$($p.GetAttribute('content-desc'))" }
        exit 5
    }
    if ($parciais.Count -eq 1) { $achado = $parciais[0] }
}
if (-not $achado) {
    "Nao achei '$Texto' na tela. Use -Listar para ver o que existe."
    exit 1
}
$m = [regex]::Matches($achado.bounds, "\d+")
$x = [int](([int]$m[0].Value + [int]$m[2].Value) / 2)
$y = [int](([int]$m[1].Value + [int]$m[3].Value) / 2)
$saida = if ($Longo) { & $adbExe -s $alvo shell "input swipe $x $y $x $y 900" 2>&1 } else { & $adbExe -s $alvo shell "input tap $x $y" 2>&1 }
$resposta = ($saida | ForEach-Object { "$_" }) -join " "
# alguns aparelhos recusam toque simulado, como Xiaomi sem a opcao de seguranca da depuracao
if ($resposta -match "Exception|Error|INJECT_EVENTS") {
    "O aparelho recusou o toque simulado: $resposta"
    "Veja references/aparelhos.md, ou peca ao usuario para tocar na tela."
    exit 6
}
"Toquei em '$Texto' ($x, $y)"
exit 0
