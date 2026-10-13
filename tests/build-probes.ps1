$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot
$managed = Join-Path $root 'Run!ZombieFoods!\Run!ZombieFoods!_Data\Managed'
$core = Join-Path $root 'Run!ZombieFoods!\BepInEx\core'
$plugins = Join-Path $root 'Run!ZombieFoods!\BepInEx\plugins'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v3.5\csc.exe'
foreach ($name in @('SchedulingProbe', 'CameraProbe')) {
    & $compiler /nologo /target:library "/out:$plugins\$name.dll" "/r:$core\BepInEx.dll" "/r:$managed\UnityEngine.dll" "/r:$managed\Assembly-CSharp.dll" "/r:$plugins\HighRefreshFix.dll" (Join-Path $PSScriptRoot "$name.cs")
    if ($LASTEXITCODE -ne 0) { throw "$name build failed" }
}
