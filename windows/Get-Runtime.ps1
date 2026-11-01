param([string]$OutputDirectory,[string]$DotnetRoot)
$ErrorActionPreference='Stop'
if(!$OutputDirectory){$OutputDirectory=Join-Path $PSScriptRoot 'runtime'}
if(!$DotnetRoot){
 $command=Get-Command dotnet.exe -CommandType Application -ErrorAction SilentlyContinue | Select-Object -First 1
 if($command){$DotnetRoot=Split-Path $command.Source}elseif($env:DOTNET_ROOT){$DotnetRoot=$env:DOTNET_ROOT}else{$DotnetRoot=Join-Path $env:ProgramFiles 'dotnet'}
}
function Latest([string]$relative){
 $parent=Join-Path $DotnetRoot $relative
 if(!(Test-Path -LiteralPath $parent)){throw "Missing $relative. Install the .NET 10 SDK for Windows x64, or pass a complete runtime to Build-Setup.ps1."}
 $version=(Get-ChildItem -LiteralPath $parent -Directory | Where-Object Name -Match '^10\.0\.\d+$' | Sort-Object {[version]$_.Name} -Descending | Select-Object -First 1).Name
 if(!$version){throw "No .NET 10 runtime in $relative"}
 return $version
}
$core=Latest 'shared\Microsoft.NETCore.App'
$desktop=Latest 'shared\Microsoft.WindowsDesktop.App'
$fxr=Latest 'host\fxr'
$sourceRoot=[IO.Path]::GetFullPath($DotnetRoot).TrimEnd('\')
$destinationRoot=[IO.Path]::GetFullPath($OutputDirectory).TrimEnd('\')
if($destinationRoot.Equals($sourceRoot,[StringComparison]::OrdinalIgnoreCase) -or $destinationRoot.StartsWith($sourceRoot+'\',[StringComparison]::OrdinalIgnoreCase)){throw 'Use a separate output directory; the installed SDK is read-only input.'}
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
foreach($relative in @("host\fxr\$fxr","shared\Microsoft.NETCore.App\$core","shared\Microsoft.WindowsDesktop.App\$desktop")){
 $destination=Join-Path $OutputDirectory $relative
 New-Item -ItemType Directory -Path $destination -Force | Out-Null
 Get-ChildItem -LiteralPath (Join-Path $DotnetRoot $relative) -Force | Copy-Item -Destination $destination -Recurse -Force
}
foreach($name in @('dotnet.exe','LICENSE.txt','ThirdPartyNotices.txt')){Copy-Item -LiteralPath (Join-Path $DotnetRoot $name) -Destination $OutputDirectory -Force}
Write-Host "Runtime ready: Core $core; Desktop $desktop; hostfxr $fxr. Copied from the installed SDK with original notices."