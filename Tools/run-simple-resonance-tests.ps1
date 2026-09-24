param([string]$UnityPath='C:/Program Files/Unity/Hub/Editor/6000.3.14f1/Editor/Unity.exe', [switch]$Performance)
$ErrorActionPreference='Stop'
$taskRepo=Split-Path $PSScriptRoot -Parent
$taskRoot=Join-Path $taskRepo 'Library/VerificationProject'
New-Item -ItemType Directory -Force -Path "$taskRoot/Assets/Editor/EconomyAnalyzer","$taskRoot/Assets/Resources","$taskRoot/Packages","$taskRoot/ProjectSettings","$taskRepo/Logs" | Out-Null
foreach($taskDir in @('Scripts','Plugins','LeanTween','ScriptableObjects','Art/UI/ComicToon','TextMesh Pro/Resources','TextMesh Pro/Shaders','TextMesh Pro/Fonts')) {
 & robocopy ("$taskRepo/Assets/"+$taskDir) ("$taskRoot/Assets/"+$taskDir) /E /R:1 /W:1 /NFL /NDL /NJH /NJS /NP | Out-Null
 if($LASTEXITCODE -gt 7){throw "Copy failed: $taskDir"}
}
Copy-Item -LiteralPath "$taskRepo/Assets/Editor/SimpleResonanceVerification.cs" -Destination "$taskRoot/Assets/Editor/"
if ($Performance) {
 Copy-Item -LiteralPath "$taskRepo/Assets/Editor/PerformanceStressVerification.cs" -Destination "$taskRoot/Assets/Editor/"
 & 'C:/Users/User/.cache/codex-runtimes/codex-primary-runtime/dependencies/python/python.exe' "$taskRepo/Tools/copy-performance-fixture.py"
 if($LASTEXITCODE -ne 0){throw 'Performance fixture copy failed.'}
}
New-Item -ItemType Directory -Force -Path "$taskRoot/Assets/Prefabs/UI" | Out-Null
Copy-Item -LiteralPath "$taskRepo/Assets/Prefabs/UI/Skill Node.prefab" -Destination "$taskRoot/Assets/Prefabs/UI/"
foreach($taskFile in @('EconomyCalculator.cs','EconomySimulation.cs','EconomyBalanceProfileSO.cs')) {
 Copy-Item -LiteralPath "$taskRepo/Assets/Editor/EconomyAnalyzer/$taskFile" -Destination "$taskRoot/Assets/Editor/EconomyAnalyzer/"
}
foreach($taskFile in @('ResonanceRules.asset','ResonanceRules.asset.meta','PlantHealthScaling.asset','PlantHealthScaling.asset.meta','ComicUITheme.asset','ComicUITheme.asset.meta')) {
 Copy-Item -LiteralPath "$taskRepo/Assets/Resources/$taskFile" -Destination "$taskRoot/Assets/Resources/"
}
Copy-Item -LiteralPath "$taskRepo/ProjectSettings/ProjectVersion.txt" -Destination "$taskRoot/ProjectSettings/"
$taskManifest=Get-Content "$taskRepo/Packages/manifest.json" -Raw | ConvertFrom-Json
$taskDeps=[ordered]@{}
foreach($taskDep in $taskManifest.dependencies.PSObject.Properties) {
 if($taskDep.Name.StartsWith('com.unity.modules.') -or $taskDep.Name -in @('com.unity.ugui','com.unity.inputsystem','com.unity.render-pipelines.universal')) { $taskDeps[$taskDep.Name]=$taskDep.Value }
}
@{dependencies=$taskDeps} | ConvertTo-Json -Depth 5 | Set-Content "$taskRoot/Packages/manifest.json"
$taskSuite=if($Performance){'PerformanceStressVerification'}else{'SimpleResonanceVerification'}
$taskArgs=@('-batchmode','-projectPath',('"'+$taskRoot+'"'),'-executeMethod',($taskSuite+'.RunBatch'),'-logFile',('"'+$taskRepo+'/Logs/'+$taskSuite+'.log"'))
$taskProcess=Start-Process -FilePath $UnityPath -ArgumentList $taskArgs -WindowStyle Hidden -PassThru
if(-not $taskProcess.WaitForExit(600000)) {
 # This Process object is the helper launched immediately above, never the user's Editor.
 $taskProcess.Kill()
 throw "Isolated Unity verification timed out. Inspect Logs/$taskSuite.log."
}
foreach($taskFile in @('SimpleResonanceVerification.txt','PerformanceStressVerification.txt','ResonanceBadges.png','ComicPopups.png')) {
 if(Test-Path "$taskRoot/Logs/$taskFile"){Copy-Item -LiteralPath "$taskRoot/Logs/$taskFile" -Destination "$taskRepo/Logs/$taskFile"}
}
if($taskProcess.ExitCode -ne 0){throw "Unity verification failed with exit code $($taskProcess.ExitCode)."}
Get-Content "$taskRepo/Logs/$taskSuite.txt" -TotalCount 1


