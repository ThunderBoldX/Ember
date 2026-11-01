param([string]$OutputDirectory,[string]$RuntimeDirectory)
$ErrorActionPreference='Stop'
$source=$PSScriptRoot;$root=Split-Path $source
if(!$OutputDirectory){$OutputDirectory=Join-Path $root 'artifacts\releases'}
if(!$RuntimeDirectory){$RuntimeDirectory=Join-Path $source 'runtime'}
if(!(Test-Path (Join-Path $RuntimeDirectory 'dotnet.exe'))){& (Join-Path $source 'Get-Runtime.ps1') -OutputDirectory $RuntimeDirectory}
$OutputDirectory=[IO.Path]::GetFullPath($OutputDirectory)
$stage=Join-Path $source ('outputs\setup-'+[Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $stage,(Join-Path $stage 'app'),$OutputDirectory -Force | Out-Null
$build=Join-Path $source 'Ember\bin\Release\net10.0-windows'
foreach($name in @('Ember.dll','Ember.deps.json','Ember.runtimeconfig.json','BleBridge.ps1')){Copy-Item -LiteralPath (Join-Path $build $name) -Destination (Join-Path $stage 'app')}
Copy-Item -LiteralPath (Join-Path $build 'Bridge') -Destination (Join-Path $stage 'app') -Recurse
Copy-Item -LiteralPath $RuntimeDirectory -Destination (Join-Path $stage 'runtime') -Recurse
foreach($name in @('LICENSE','THIRD_PARTY_NOTICES.md')){Copy-Item -LiteralPath (Join-Path $root $name) -Destination $stage}
New-Item -ItemType Directory -Path (Join-Path $stage 'docs') -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $root 'docs\third-party') -Destination (Join-Path $stage 'docs') -Recurse
Copy-Item -LiteralPath (Join-Path $root 'docs\INSTALL.md') -Destination (Join-Path $stage 'INSTALL.md')
$csc=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
& $csc /nologo /target:winexe /platform:x64 /reference:System.Windows.Forms.dll "/win32icon:$source\Ember\Assets\ember.ico" "/out:$stage\Ember.exe" (Join-Path $source 'Launcher.cs')
if($LASTEXITCODE -ne 0){throw 'Launcher compilation failed'}
$payload=Join-Path (Split-Path $stage) ('payload-'+[Guid]::NewGuid().ToString('N')+'.zip')
Add-Type -AssemblyName System.IO.Compression.FileSystem
[IO.Compression.ZipFile]::CreateFromDirectory($stage,$payload,[IO.Compression.CompressionLevel]::Optimal,$false)
$setup=Join-Path $OutputDirectory 'Ember-Windows-v0.5.3-Setup.exe'
& $csc /nologo /target:winexe /platform:x64 /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:Microsoft.CSharp.dll /reference:System.Core.dll /reference:System.IO.Compression.dll /reference:System.IO.Compression.FileSystem.dll "/resource:$payload,Ember.payload" "/resource:$source\Ember\Assets\ember.ico,Ember.icon" "/win32icon:$source\Ember\Assets\ember.ico" "/out:$setup" (Join-Path $source 'Setup.cs')
if($LASTEXITCODE -ne 0){throw 'Setup compilation failed'}
Get-FileHash -LiteralPath $setup -Algorithm SHA256
