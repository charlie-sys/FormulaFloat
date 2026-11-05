param([switch]$SelfContained)
$ErrorActionPreference = 'Stop'
$sdk = Join-Path $PSScriptRoot '.tools/dotnet/dotnet.exe'
if (!(Test-Path -LiteralPath $sdk)) { $sdk = 'dotnet' }
$target = Join-Path $PSScriptRoot 'release/FormulaFloat'
& $sdk publish (Join-Path $PSScriptRoot 'FormulaFloat/FormulaFloat.csproj') -c Release -r win-x64 --self-contained $SelfContained.IsPresent.ToString().ToLowerInvariant() -p:PublishSingleFile=true -p:DebugType=None -o $target
if ($LASTEXITCODE -ne 0) { throw '构建失败' }
Write-Host "构建完成：$target/FormulaFloat.exe"
