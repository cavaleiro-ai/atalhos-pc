# Confere se as teclas continuam na tela depois de trocar de aba e de voltar de Ajustes.
# So toca nas abas e em Ajustes, nunca em tecla: nao abre programa nem toca som. O app precisa estar aberto e pareado.
# Liga e desliga a opcao de nomes para remontar a tela, e ela termina como estava.
param([string]$Serial = "", [string]$Adb = "")
$ErrorActionPreference = "Continue"
. (Join-Path $PSScriptRoot "comum.ps1")
Show-Mensagem
$adbExe = Find-Adb -Informado $Adb
if (-not $adbExe) { "adb nao encontrado: confira na tela, trocando de aba e voltando de Ajustes."; exit 3 }
$alvo = Resolve-Serial -Adb $adbExe -Serial $Serial
if (-not $alvo) { "Aparelho indefinido: use -Serial."; exit 3 }
$cfg = Join-Path $env:APPDATA "AtalhosPC\config.json"
if (-not (Test-Path -LiteralPath $cfg)) { "Sem configuracao neste PC: abra o programa e pareie antes."; exit 3 }
$c = Get-Content -LiteralPath $cfg -Raw -Encoding UTF8 | ConvertFrom-Json
$atalhos = @($c.shortcuts | ForEach-Object { $_.name })
$sons = @($c.sounds | ForEach-Object { $_.name })

function Get-Topo {
    return "" + (& $adbExe -s $alvo shell "dumpsys activity activities 2>/dev/null" | Select-String -Pattern 'topResumedActivity' | Select-Object -First 1)
}

$tocar = Join-Path $PSScriptRoot "tocar_ui.ps1"
function Toca($t) {
    & powershell -NoProfile -ExecutionPolicy Bypass -File $tocar -Serial $alvo -Adb $adbExe -Texto $t | Out-Null
    Start-Sleep -Seconds 3
}

$script:falhas = 0
# Tecla com nome e tecla vazia sao os botoes da pagina: se ha itens sobrando, a primeira pagina precisa estar cheia.
function Confere($acao, $nomes) {
    $nos = @(Get-UiNodes -Adb $adbExe -Serial $alvo)
    $descricoes = @($nos | Where-Object { $_.clickable -eq "true" } | ForEach-Object { $_.GetAttribute("content-desc") })
    $comNome = @($descricoes | Where-Object { $nomes -contains $_ }).Count
    $vazias = @($descricoes | Where-Object { $_ -like "Tecla vazia*" }).Count
    $esperado = [Math]::Min($nomes.Count, $comNome + $vazias)
    $ok = ($comNome -eq $esperado) -and (($comNome + $vazias) -gt 0)
    if (-not $ok) { $script:falhas++ }
    "{0,-40} {1} teclas com nome, esperado {2}: {3}" -f $acao, $comNome, $esperado, $(if ($ok) { "ok" } else { "FALHOU" })
}

if ((Get-Topo) -notmatch 'atalhospc/\.MainActivity') {
    & $adbExe -s $alvo shell "am start -n com.cavaleiro.atalhospc/.MainActivity" | Out-Null
    Start-Sleep -Seconds 4
}
if ((Get-Topo) -notmatch 'atalhospc/\.MainActivity') { "O app nao esta na tela. A tela esta ligada e desbloqueada?"; exit 4 }

Toca "Atalhos"; Confere "aba Atalhos" $atalhos
Toca "Sons"; Confere "aba Sons" $sons
Toca "Atalhos"; Confere "volta para Atalhos" $atalhos
Toca "Ajustes"
# Sem a tela de Ajustes aberta, o Voltar sairia do app e os proximos toques cairiam em outra tela.
if ((Get-Topo) -notmatch 'SettingsActivity') { "Ajustes nao abriu: parei aqui."; exit 4 }
& $adbExe -s $alvo shell "input keyevent KEYCODE_BACK"
Start-Sleep -Seconds 3
Confere "Ajustes e volta, sem mudar nada" $atalhos

# Mudar um ajuste remonta a tela. Liga e desliga a opcao de nomes, deixando como estava.
Toca "Ajustes"
if ((Get-Topo) -notmatch 'SettingsActivity') { "Ajustes nao abriu: parei aqui."; exit 4 }
$opcao = @(Get-UiNodes -Adb $adbExe -Serial $alvo | Where-Object { "$($_.text)" -like "Mostrar o nome*" }) | Select-Object -First 1
if (-not $opcao) { "Nao achei a opcao de nomes em Ajustes."; exit 4 }
$m = [regex]::Matches($opcao.bounds, "\d+")
$x = [int](([int]$m[0].Value + [int]$m[2].Value) / 2); $y = [int](([int]$m[1].Value + [int]$m[3].Value) / 2)
& $adbExe -s $alvo shell "input tap $x $y; sleep 1; input tap $x $y; sleep 1; input keyevent KEYCODE_BACK"
Start-Sleep -Seconds 3
Confere "muda um ajuste e volta" $atalhos

if ($script:falhas -eq 0) { "RESULTADO: teclas ok" } else { "RESULTADO: $($script:falhas) FALHA(S)"; exit 1 }
