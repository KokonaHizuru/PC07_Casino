param([string]$UnityDataPath = 'C:\Program Files\Unity\Hub\Editor\6000.2.6f2\Editor\Data')
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
Set-Location -LiteralPath $projectRoot
$testOutput = Join-Path $projectRoot 'Temp/HighCardDuelTests'
New-Item -ItemType Directory -Force $testOutput | Out-Null
$arguments = @('-nologo', '-target:exe', '-langversion:9.0', '-nostdlib+', ('-out:"' + $testOutput + '/LogicTests.exe"'))
$frameworkPath = Join-Path $UnityDataPath 'MonoBleedingEdge/lib/mono/4.8-api'
foreach ($name in @('mscorlib.dll', 'System.dll', 'System.Core.dll')) {
    $arguments += '-r:"' + (Join-Path $frameworkPath $name) + '"'
}
$engineDll = Join-Path $UnityDataPath 'Managed/UnityEngine/UnityEngine.CoreModule.dll'
$arguments += '-r:"' + $engineDll + '"'
$arguments += '-r:"' + (Join-Path $UnityDataPath 'MonoBleedingEdge/lib/mono/net_4_x-win32/Facades/netstandard.dll') + '"'
$arguments += @('Assets/MyAssets/Script/MyGame/Common/ICasinoWallet.cs', 'Assets/MyAssets/Script/MyGame/Cards/PlayingCard.cs', 'Assets/MyAssets/Script/MyGame/Cards/CardDeck.cs',
    'Assets/MyAssets/Script/MyGame/HighCardDuel/DuelTypes.cs', 'Assets/MyAssets/Script/MyGame/HighCardDuel/HighCardDealer.cs',
    'Assets/MyAssets/Script/MyGame/HighCardDuel/HighCardDuelGame.cs', 'Tests/HighCardDuel/LogicTests.cs')
$responsePath = Join-Path $testOutput 'tests.rsp'
Set-Content -LiteralPath $responsePath -Value $arguments -Encoding utf8
& (Join-Path $UnityDataPath 'NetCoreRuntime/dotnet.exe') (Join-Path $UnityDataPath 'DotNetSdkRoslyn/csc.dll') "@$responsePath"
if ($LASTEXITCODE -ne 0) { throw 'Test compilation failed.' }
Copy-Item -LiteralPath $engineDll -Destination $testOutput -Force
& (Join-Path $UnityDataPath 'MonoBleedingEdge/bin/mono.exe') (Join-Path $testOutput 'LogicTests.exe')
if ($LASTEXITCODE -ne 0) { throw 'Tests failed.' }
