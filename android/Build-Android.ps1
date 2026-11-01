param([string]$SdkRoot,[switch]$Unsigned,[string]$KeystorePath,[string]$KeyAlias='ember')
$ErrorActionPreference='Stop'
function Run([string]$exe,[string[]]$arguments){& $exe @arguments;if($LASTEXITCODE -ne 0){throw "$exe failed ($LASTEXITCODE)"}}
if(!$SdkRoot){$SdkRoot=$env:ANDROID_SDK_ROOT};if(!$SdkRoot){$SdkRoot=$env:ANDROID_HOME}
if(!$SdkRoot){throw 'Set ANDROID_SDK_ROOT or pass -SdkRoot. Install Android SDK platform 35 and build-tools 35.0.0.'}
$androidJar=Join-Path $SdkRoot 'platforms\android-35\android.jar';$buildTools=Join-Path $SdkRoot 'build-tools\35.0.0'
if(!(Test-Path $androidJar) -or !(Test-Path (Join-Path $buildTools 'aapt2.exe'))){throw 'Android SDK platform 35 and build-tools 35.0.0 are required.'}
if(!$Unsigned){
 if(!$KeystorePath -or !(Test-Path -LiteralPath $KeystorePath)){throw 'Pass a private -KeystorePath, or use -Unsigned for a compile-only build.'}
 if(!$env:EMBER_KEYSTORE_PASSWORD){throw 'Set EMBER_KEYSTORE_PASSWORD in your local environment. Never commit credentials.'}
 if(!$env:EMBER_KEY_PASSWORD){$env:EMBER_KEY_PASSWORD=$env:EMBER_KEYSTORE_PASSWORD}
}
$stage=Join-Path $PSScriptRoot ('build\'+[Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Force -Path $stage,(Join-Path $stage 'generated'),(Join-Path $stage 'classes'),(Join-Path $stage 'dex') | Out-Null
Run (Join-Path $buildTools 'aapt2.exe') @('compile','--no-crunch','--dir',(Join-Path $PSScriptRoot 'res'),'-o',(Join-Path $stage 'res.zip'))
Run (Join-Path $buildTools 'aapt2.exe') @('link','-o',(Join-Path $stage 'base.apk'),'-I',$androidJar,'--manifest',(Join-Path $PSScriptRoot 'AndroidManifest.xml'),'--java',(Join-Path $stage 'generated'),'-A',(Join-Path $PSScriptRoot 'assets'),'--min-sdk-version','26','--target-sdk-version','35',(Join-Path $stage 'res.zip'))
$sources=@(Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'src'),(Join-Path $stage 'generated') -Filter '*.java' -Recurse | ForEach-Object FullName)
Run 'javac' (@('-encoding','UTF-8','--release','8','-classpath',$androidJar,'-d',(Join-Path $stage 'classes'))+$sources)
Run 'jar' @('cf',(Join-Path $stage 'classes.jar'),'-C',(Join-Path $stage 'classes'),'.')
Run 'java' @('-cp',(Join-Path $buildTools 'lib\d8.jar'),'com.android.tools.r8.D8','--lib',$androidJar,'--min-api','26','--output',(Join-Path $stage 'dex'),(Join-Path $stage 'classes.jar'))
Add-Type -AssemblyName System.IO.Compression,System.IO.Compression.FileSystem
$zip=[IO.Compression.ZipFile]::Open((Join-Path $stage 'base.apk'),[IO.Compression.ZipArchiveMode]::Update)
try{foreach($dex in Get-ChildItem (Join-Path $stage 'dex') -Filter '*.dex'){$null=[IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip,$dex.FullName,$dex.Name)}}finally{$zip.Dispose()}
Run (Join-Path $buildTools 'zipalign.exe') @('-f','-p','4',(Join-Path $stage 'base.apk'),(Join-Path $stage 'aligned.apk'))
$output=Join-Path (Split-Path $PSScriptRoot) 'artifacts\releases';New-Item -ItemType Directory -Force -Path $output | Out-Null
if($Unsigned){$apk=Join-Path $output 'Ember-Android-v0.5.3-unsigned.apk';Copy-Item -LiteralPath (Join-Path $stage 'aligned.apk') -Destination $apk -Force;Write-Host "Unsigned compile-check APK: $apk";return}
$apk=Join-Path $output 'Ember-Android-v0.5.3.apk'
Run 'java' @('-jar',(Join-Path $buildTools 'lib\apksigner.jar'),'sign','--ks',$KeystorePath,'--ks-key-alias',$KeyAlias,'--ks-pass','env:EMBER_KEYSTORE_PASSWORD','--key-pass','env:EMBER_KEY_PASSWORD','--out',$apk,(Join-Path $stage 'aligned.apk'))
Run 'java' @('-jar',(Join-Path $buildTools 'lib\apksigner.jar'),'verify','--verbose',$apk)
Write-Host "Signed APK ready: $apk"
