# İzole doğrulama: projeyi Library/VerificationProject kopyasına eşitler, Tools/Verification/Editor içindeki test
# kaynaklarını oraya koyar ve Unity'yi batch modda çalıştırır. Açık editöre ve sahneye dokunmaz.
# Örnek:
#   Tools/run-isolated-verification.ps1 -Method RunPrototypeVerification.RunBatch -Full
#   Tools/run-isolated-verification.ps1 -Method RunSimulator.RunPrototypeBatch
param(
    [Parameter(Mandatory = $true)][string]$Method,
    [switch]$Full,
    [int]$TimeoutSec = 1200,
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
$unityArgs = @('-batchmode', '-projectPath', ('"' + $root + '"'), '-executeMethod', $Method, '-logFile', ('"' + $log + '"'))
$process = Start-Process -FilePath $UnityPath -ArgumentList $unityArgs -WindowStyle Hidden -PassThru
if (-not $process.WaitForExit($TimeoutSec * 1000)) { $process.Kill(); throw "Timed out. See $log" }
"exit $($process.ExitCode) · log $log"
