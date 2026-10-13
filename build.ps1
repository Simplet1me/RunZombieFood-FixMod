param(
    [string]$OutputDirectory = (Join-Path $PSScriptRoot 'Run!ZombieFoods!\BepInEx\plugins')
)

$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$managed = Join-Path $root 'Run!ZombieFoods!\Run!ZombieFoods!_Data\Managed'
$core = Join-Path $root 'Run!ZombieFoods!\BepInEx\core'
$output = $OutputDirectory
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v3.5\csc.exe'
foreach ($required in @($compiler, "$core\BepInEx.dll", "$core\0Harmony.dll", "$managed\UnityEngine.dll", "$managed\Assembly-CSharp.dll")) {
    if (-not (Test-Path -LiteralPath $required -PathType Leaf)) { throw "Missing build dependency: $required" }
}
New-Item -ItemType Directory -Force $output | Out-Null
& $compiler /nologo /target:library /optimize+ "/out:$output\HighRefreshFix.dll" "/reference:$core\BepInEx.dll" "/reference:$core\0Harmony.dll" "/reference:$managed\UnityEngine.dll" "/reference:$managed\Assembly-CSharp.dll" (Join-Path $root 'src\HighRefreshFix.cs') (Join-Path $root 'src\CameraFix.cs')
if ($LASTEXITCODE -ne 0) { throw 'Build failed' }
