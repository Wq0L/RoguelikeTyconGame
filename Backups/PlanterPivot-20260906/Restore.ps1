$ErrorActionPreference = 'Stop'
$projectDir = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$manifest = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'after-hashes.json') -Raw | ConvertFrom-Json
$targets = foreach ($entry in $manifest) {
    $target = [IO.Path]::GetFullPath((Join-Path $projectDir $entry.path))
    if (-not $target.StartsWith($projectDir + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Proje disinda hedef: $target"
    }
    $source = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot $entry.path))
    if (-not $source.StartsWith($PSScriptRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Yedek disinda kaynak: $source"
    }
    if ((Get-FileHash -LiteralPath $target).Hash -ne $entry.hash) {
        throw "Bu dosya duzeltmeden sonra degistirilmis; yeni calismayi ezmemek icin geri alma durduruldu: $target"
    }
    if ((Get-FileHash -LiteralPath $source).Hash -ne $entry.backupHash) {
        throw "Yedek degismis: $source"
    }
    [PSCustomObject]@{ Source = $source; Target = $target }
}
foreach ($item in $targets) { Copy-Item -LiteralPath $item.Source -Destination $item.Target }
Write-Output '8 dosya duzeltme oncesindeki haline getirildi. Unity yeniden derleyecek.'
