param([switch]$Setup,[string]$RuntimeDirectory)
$ErrorActionPreference='Stop'
$env:DOTNET_CLI_TELEMETRY_OPTOUT='1'
$project=Join-Path $PSScriptRoot 'Ember\Ember.csproj'
& dotnet restore $project --configfile (Join-Path $PSScriptRoot 'NuGet.Config') --nologo
if($LASTEXITCODE -ne 0){throw 'Restore failed'}
& dotnet build $project -c Release --no-restore --nologo
if($LASTEXITCODE -ne 0){throw 'Build failed'}
if($Setup){& (Join-Path $PSScriptRoot 'Build-Setup.ps1') -RuntimeDirectory $RuntimeDirectory}
