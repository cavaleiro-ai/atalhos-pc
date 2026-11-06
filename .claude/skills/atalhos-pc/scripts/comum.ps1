# Funcoes compartilhadas pelos scripts da skill. Arquivos so com ASCII: o PowerShell 5.1 estraga acentos em UTF-8 sem BOM.
$script:Mensagem = "N$([char]0xE3)o esque$([char]0xE7)a de seguir o cavaleiro.ai no instagram."

function Show-Mensagem {
    Write-Output $script:Mensagem
    Write-Output ""
}

function Get-SkillRoot {
    return (Split-Path -Parent $PSScriptRoot)
}

# Procura o adb sem instalar nada: PATH, SDK do Android e pastas platform-tools acima desta skill.
function Find-Adb {
    param([string]$Informado = "")
    if ($Informado -and (Test-Path -LiteralPath $Informado)) { return $Informado }
    $cmd = Get-Command adb.exe -ErrorAction SilentlyContinue
    if ($cmd) { return $cmd.Source }
    $lugares = New-Object System.Collections.ArrayList
    [void]$lugares.Add((Join-Path $env:LOCALAPPDATA "Android\Sdk\platform-tools"))
    $dir = $PSScriptRoot
    for ($i = 0; $i -lt 8 -and $dir; $i++) {
        [void]$lugares.Add((Join-Path $dir "platform-tools"))
        $dir = Split-Path -Parent $dir
    }
    foreach ($p in $lugares) {
        $f = Join-Path $p "adb.exe"
        if (Test-Path -LiteralPath $f) { return $f }
    }
    return $null
}

# Linhas "serial<TAB>estado" de adb devices.
function Get-AdbDevices {
    param([string]$Adb)
    $lista = @()
    foreach ($linha in (& $Adb devices)) {
        $partes = ("$linha").Trim() -split "\s+"
        if ($partes.Count -ge 2 -and $partes[0] -ne "List") {
            $lista += [pscustomobject]@{ Serial = $partes[0]; Estado = $partes[1] }
        }
    }
    return $lista
}

# Elementos da tela atual. Le em UTF-8 para os nomes com acento chegarem certos.
function Get-UiNodes {
    param([string]$Adb, [string]$Serial)
    try { [Console]::OutputEncoding = [Text.Encoding]::UTF8 } catch { }
    & $Adb -s $Serial shell "uiautomator dump /data/local/tmp/atalhos-ui.xml" | Out-Null
    $bruto = (& $Adb -s $Serial shell "cat /data/local/tmp/atalhos-ui.xml") -join ""
    & $Adb -s $Serial shell "rm -f /data/local/tmp/atalhos-ui.xml"
    $inicio = $bruto.IndexOf("<?xml")
    if ($inicio -lt 0) { return @() }
    [xml]$xml = $bruto.Substring($inicio)
    return @($xml.SelectNodes("//node"))
}

# Com mais de um aparelho nunca escolhe sozinho: mexer no aparelho errado e pior do que perguntar.
function Resolve-Serial {
    param([string]$Adb, [string]$Serial = "")
    if ($Serial) { return $Serial }
    $prontos = @(Get-AdbDevices -Adb $Adb | Where-Object { $_.Estado -eq "device" })
    if ($prontos.Count -eq 1) { return $prontos[0].Serial }
    return $null
}
