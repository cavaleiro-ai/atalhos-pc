# Mede o pico da saida de audio padrao do Windows por fora do app, para provar que o som saiu de verdade.
# Sem -TocarTexto: mede por -Segundos enquanto o usuario toca um pad.
# Com -TocarTexto: toca sozinho o pad com esse nome no aparelho enquanto mede.
param([double]$Segundos = 5, [string]$TocarTexto = "", [string]$Serial = "", [string]$Adb = "", [string]$Rotulo = "medicao")
$ErrorActionPreference = "Continue"
. (Join-Path $PSScriptRoot "comum.ps1")
Show-Mensagem
Add-Type -TypeDefinition @"
using System; using System.Runtime.InteropServices;
public static class MedidorPico {
  [ComImport, Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")] class En {}
  [ComImport, Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
  interface IEn { int N(); [PreserveSig] int GetDefaultAudioEndpoint(int f, int r, out IDev d); }
  [ComImport, Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
  interface IDev { [PreserveSig] int Activate(ref Guid i, int c, IntPtr p, [MarshalAs(UnmanagedType.IUnknown)] out object s); }
  [ComImport, Guid("C02216F6-8C67-4B5B-9D00-D008E73E0064"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
  interface IM { [PreserveSig] int GetPeakValue(out float p); }
  static IM m;
  public static float Ler() {
    if (m == null) { IDev d; if (((IEn)new En()).GetDefaultAudioEndpoint(0, 1, out d) != 0) throw new Exception("sem saida de audio"); Guid g = typeof(IM).GUID; object o; d.Activate(ref g, 23, IntPtr.Zero, out o); m = (IM)o; }
    float p; m.GetPeakValue(out p); return p;
  }
}
"@
try { $repouso = 0.0; 1..15 | ForEach-Object { $repouso = [Math]::Max($repouso, [MedidorPico]::Ler()); Start-Sleep -Milliseconds 30 } }
catch { "Sem saida de audio neste PC: nao ha como medir ($_)"; exit 2 }
if ($repouso -gt 0.01) { "Aviso: ja ha som saindo no PC (pico $([Math]::Round($repouso, 3))). Feche o que estiver tocando para medir direito." }
if ($TocarTexto) {
    $adbExe = Find-Adb -Informado $Adb
    $alvo = if ($adbExe) { Resolve-Serial -Adb $adbExe -Serial $Serial } else { $null }
    if (-not $alvo) { "Aparelho indefinido: use -Serial, ou rode sem -TocarTexto."; exit 3 }
    $tocar = Join-Path $PSScriptRoot "tocar_ui.ps1"
    Start-Job -ScriptBlock { param($f, $t, $s, $a) & powershell -NoProfile -ExecutionPolicy Bypass -File $f -Texto $t -Serial $s -Adb $a } -ArgumentList $tocar, $TocarTexto, $alvo, $adbExe | Out-Null
}
$pico = 0.0; $primeiro = -1; $amostras = 0
$sw = [Diagnostics.Stopwatch]::StartNew()
while ($sw.Elapsed.TotalSeconds -lt $Segundos) {
    $p = [MedidorPico]::Ler()
    if ($p -gt $pico) { $pico = $p }
    if ($p -gt ($repouso + 0.01)) { $amostras++; if ($primeiro -lt 0) { $primeiro = $sw.ElapsedMilliseconds } }
    Start-Sleep -Milliseconds 15
}
$toque = ""
if ($TocarTexto) { $toque = (Get-Job | Wait-Job -Timeout 15 | Receive-Job -ErrorAction SilentlyContinue | ForEach-Object { "$_" }) -join " " }
Get-Job | Remove-Job -Force -ErrorAction SilentlyContinue
$veredito = if ($amostras -gt 0) { "SOM DETECTADO" } else { "SILENCIO" }
# silencio so conta como resultado se o toque aconteceu de verdade
if ($TocarTexto -and $amostras -eq 0 -and $toque -notmatch "Toquei") { $veredito = "INCONCLUSIVO, o toque falhou: $toque" }
"{0}: {1} | repouso={2:N3} pico={3:N3} primeiro_som={4}ms amostras={5}" -f $Rotulo, $veredito, $repouso, $pico, $primeiro, $amostras
