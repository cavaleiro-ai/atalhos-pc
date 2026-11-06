# Levanta os fatos do PC e do aparelho e sugere o caminho de conexao. So le: nao muda nada em nenhum dos dois.
param([string]$Serial = "", [string]$Adb = "")
$ErrorActionPreference = "Continue"
. (Join-Path $PSScriptRoot "comum.ps1")
Show-Mensagem

function Same-Subnet {
    param([string]$A, [string]$B, [int]$Prefixo)
    $x = ([Net.IPAddress]::Parse($A)).GetAddressBytes()
    $y = ([Net.IPAddress]::Parse($B)).GetAddressBytes()
    for ($bit = 0; $bit -lt $Prefixo; $bit++) {
        $mask = 0x80 -shr ($bit % 8)
        if (($x[[int][Math]::Floor($bit / 8)] -band $mask) -ne ($y[[int][Math]::Floor($bit / 8)] -band $mask)) { return $false }
    }
    return $true
}

"=== PC"
$os = Get-CimInstance Win32_OperatingSystem
"Windows: $($os.Caption) $($os.Version)"
$rel = (Get-ItemProperty 'HKLM:\SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full' -ErrorAction SilentlyContinue).Release
if ($rel -ge 378389) { ".NET Framework: ok (release $rel)" } else { ".NET Framework: FALTA 4.5 ou mais novo (release '$rel')" }

$bt = @(Get-PnpDevice -Class Bluetooth -PresentOnly -ErrorAction SilentlyContinue | Where-Object { $_.FriendlyName -notmatch 'Enumerator|RFCOMM|Personal Area|Device \(' })
if ($bt.Count -gt 0) { "Bluetooth: adaptador encontrado ($($bt[0].FriendlyName))" } else { "Bluetooth: nenhum adaptador neste PC" }
$temBt = $bt.Count -gt 0

$audio = @(Get-CimInstance Win32_SoundDevice -ErrorAction SilentlyContinue | Where-Object { $_.Status -eq 'OK' })
"Audio: $($audio.Count) dispositivo(s) OK"

$pcIps = @(Get-NetIPAddress -AddressFamily IPv4 -ErrorAction SilentlyContinue | Where-Object { $_.IPAddress -ne '127.0.0.1' -and $_.InterfaceAlias -notmatch 'vEthernet|Loopback' -and $_.AddressState -eq 'Preferred' })
foreach ($i in $pcIps) { "Rede: $($i.InterfaceAlias) = $($i.IPAddress)/$($i.PrefixLength)" }
Get-NetConnectionProfile -ErrorAction SilentlyContinue | ForEach-Object { "Perfil: $($_.InterfaceAlias) = $($_.NetworkCategory)" }

$proc = Get-Process AtalhosPC -ErrorAction SilentlyContinue | Select-Object -First 1
if ($proc) { "Programa: aberto ($($proc.Path))" } else { "Programa: fechado" }

$regras = (& netsh advfirewall firewall show rule name=all dir=in verbose) -join "`n"
$blocos = ($regras -replace "`r", "") -split "`n`n" | Where-Object { $_ -match 'AtalhosPC\.exe' }
if ($blocos) {
    foreach ($b in $blocos) {
        $prog = [regex]::Match($b, '[A-Za-z]:\\[^\r\n]*AtalhosPC\.exe', 'IgnoreCase').Value
        $acao = if ($b -match 'Allow|Permitir') { "permite" } else { "BLOQUEIA" }
        "Firewall: $acao | $prog"
    }
} else { "Firewall: nenhuma regra para AtalhosPC.exe" }

$cfg = Join-Path $env:APPDATA "AtalhosPC\config.json"
if (Test-Path -LiteralPath $cfg) {
    $c = Get-Content -LiteralPath $cfg -Raw -Encoding UTF8 | ConvertFrom-Json
    "Dados existentes: $(@($c.shortcuts).Count) atalho(s), $(@($c.sounds).Count) som(ns), $(@($c.devices).Count) aparelho(s) pareado(s). Preservar."
} else { "Dados existentes: nenhum (primeira instalacao)" }

"`n=== ADB"
$adbExe = Find-Adb -Informado $Adb
if (-not $adbExe) {
    "adb: nao encontrado. Sem problema: a instalacao manual funciona. Veja references/aparelhos.md."
    "`n=== SUGESTAO"
    if ($temBt) { "Sem adb, peca ao usuario o modelo, a versao do Android e o IP do aparelho. Caminhos possiveis: Ancoragem USB, Wi-Fi ou Bluetooth." }
    else { "Sem adb, peca ao usuario o modelo, a versao do Android e o IP do aparelho. Caminhos possiveis: Ancoragem USB ou Wi-Fi." }
    return
}
"adb: $adbExe"
$lista = @(Get-AdbDevices -Adb $adbExe)
if ($lista.Count -eq 0) { "Nenhum aparelho no adb. Ligue a depuracao USB, ou siga pela instalacao manual." }
foreach ($d in $lista) { "Aparelho: $($d.Serial) estado=$($d.Estado)" }

$alvo = Resolve-Serial -Adb $adbExe -Serial $Serial
if (-not $alvo) {
    if (@($lista | Where-Object { $_.Estado -eq "device" }).Count -gt 1) { "`nMais de um aparelho pronto: pergunte qual e o alvo e rode de novo com -Serial." }
    "`n=== SUGESTAO"
    "Sem aparelho definido. Depois de saber o alvo, rode: diagnostico.ps1 -Serial SERIAL"
    return
}

"`n=== APARELHO $alvo"
$props = & $adbExe -s $alvo shell "getprop ro.product.manufacturer; getprop ro.product.model; getprop ro.build.version.release; getprop ro.build.version.sdk; getprop ro.product.cpu.abi; wm size | tail -n 1; wm density | tail -n 1; settings get global animator_duration_scale; settings get global bluetooth_on; dumpsys battery | grep level"
"Fabricante: $($props[0])"; "Modelo: $($props[1])"; "Android: $($props[2]) (API $($props[3]))"; "ABI: $($props[4])"; "Tela: $($props[5]) | $($props[6])"
"Escala de animacao: $($props[7])"; "Bluetooth ligado: $($props[8])"; "Bateria: $($props[9])"
if ([int]$props[3] -lt 21) { "ATENCAO: Android abaixo de 5.0: o app nao instala." }

$pkg = & $adbExe -s $alvo shell "dumpsys package com.cavaleiro.atalhospc | grep -m1 versionName"
if ("$pkg".Trim()) { "App instalado:$pkg" } else { "App: nao instalado" }

$devIps = @()
foreach ($l in (& $adbExe -s $alvo shell "ip -4 -o addr show")) {
    if ("$l" -match '^\d+:\s+(\S+)\s+inet\s+([\d.]+)/(\d+)' -and $Matches[1] -ne 'lo') {
        $devIps += [pscustomobject]@{ Nome = $Matches[1]; Ip = $Matches[2]; Prefixo = [int]$Matches[3] }
        "Rede: $($Matches[1]) = $($Matches[2])/$($Matches[3])"
    }
}

"`n=== SUGESTAO"
$usb = $false; $wifi = $false
foreach ($d in $devIps) {
    foreach ($p in $pcIps) {
        if (Same-Subnet -A $d.Ip -B $p.IPAddress -Prefixo $d.Prefixo) {
            if ($d.Nome -match '^(usb|rndis|ncm)') { $usb = $true; "Ancoragem USB ativa e na mesma rede do PC: $($d.Ip) <-> $($p.IPAddress)" }
            else { $wifi = $true; "Mesma rede: $($d.Nome) $($d.Ip) <-> $($p.InterfaceAlias) $($p.IPAddress)" }
        }
    }
}
if ($usb) { "Recomendado: USB por Ancoragem, ja ativa. Em celular com plano de dados, prefira USB por adb." }
else { "Ha depuracao ligada, entao ha cabo: recomendado USB por adb, ou Ancoragem USB." }
if ($wifi) { "Sem cabo: Wi-Fi possivel pela mesma sub-rede. Nunca testado de ponta a ponta: confirme antes. Rede de convidados ou isolamento de clientes bloqueiam." }
else { "Por Wi-Fi, PC e aparelho estao em redes diferentes: nao funciona assim." }
if ($temBt) { "Bluetooth e possivel, mas nunca foi testado de ponta a ponta: avise o usuario." }
