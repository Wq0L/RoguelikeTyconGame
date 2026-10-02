# İzole doğrulama: projeyi Library/VerificationProject kopyasına eşitler, Tools/Verification/Editor içindeki test
# kaynaklarını oraya koyar ve Unity'yi batch modda çalıştırır. Açık editöre ve sahneye dokunmaz.
# Örnek:
#   Tools/run-isolated-verification.ps1 -Method RunPrototypeVerification.RunBatch -Full
#   Tools/run-isolated-verification.ps1 -Method RunSimulator.RunPrototypeBatch
param(
    [Parameter(Mandatory = $true)][string]$Method,
    [switch]$Full,
    [int]$TimeoutSec = 1200,
    # Bilgisayar bu sırada kullanılabilsin: batch Unity düşük öncelikle ve yalnız son $MaxCores mantıksal çekirdekte çalışır
    # (alt süreçleri de bunu devralır). 0: çekirdek sınırı yok. Sonuçlar değişmez, yalnız süre uzar.
    [int]$MaxCores = 4,
    [switch]$NormalPriority,
    [string]$UnityPath = 'C:\Program Files\Unity\Hub\Editor\6000.3.14f1\Editor\Unity.exe'
)
$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
$root = Join-Path $repo 'Library\VerificationProject'

$dirs = @('Scripts', 'ScriptableObjects', 'Resources', 'Editor', 'Plugins', 'LeanTween', 'TextMesh Pro', 'Art\UI\ComicToon')
if ($Full) { $dirs += @('Scenes', 'Prefabs', 'Materials', '3D Assets', 'Settings', 'ProjectSettings', 'Art', 'Shaders') }
foreach ($d in $dirs) {
    $src = Join-Path "$repo\Assets" $d
    if (!(Test-Path $src)) { continue }
    & robocopy $src (Join-Path "$root\Assets" $d) /E /R:1 /W:1 /NFL /NDL /NJH /NJS /NP | Out-Null
    if ($LASTEXITCODE -gt 7) { throw "Copy failed: $d" }
}
Copy-Item -Path (Join-Path $repo 'Tools\Verification\Editor\*.cs') -Destination (Join-Path $root 'Assets\Editor') -Force

New-Item -ItemType Directory -Force -Path (Join-Path $root 'Logs') | Out-Null
$log = Join-Path $root ('Logs\' + ($Method -replace '\.', '_') + '.log')
if (Test-Path -LiteralPath $log) {
    Move-Item -LiteralPath $log -Destination ($log + '.previous-' + [DateTime]::UtcNow.ToString('yyyyMMddHHmmssfff'))
}
$unityArgs = @('-batchmode', '-projectPath', ('"' + $root + '"'), '-executeMethod', $Method, '-logFile', ('"' + $log + '"'))
$process = Start-Process -FilePath $UnityPath -ArgumentList $unityArgs -WindowStyle Hidden -PassThru
try {
    if (-not $NormalPriority) { $process.PriorityClass = 'BelowNormal' }
    $cores = [Environment]::ProcessorCount
    if ($MaxCores -gt 0 -and $MaxCores -lt $cores) {
        $process.ProcessorAffinity = [IntPtr]((([long]1 -shl $MaxCores) - 1) -shl ($cores - $MaxCores))
    }
} catch { Write-Warning "Could not limit the batch Unity process: $($_.Exception.Message)" }
if (-not $process.WaitForExit($TimeoutSec * 1000)) { $process.Kill(); throw "Timed out. See $log" }
"exit $($process.ExitCode) · log $log"
if (!(Test-Path -LiteralPath $log)) { throw "Unity did not create a fresh log (exit $($process.ExitCode))." }
if ($process.ExitCode -ne 0) { throw "Verification failed with exit $($process.ExitCode). See $log" }
