$ErrorActionPreference='Stop'
Set-Location $PSScriptRoot
$target=Join-Path $PSScriptRoot ('build\core-test-'+[Guid]::NewGuid().ToString('N'))
$null=New-Item -ItemType Directory -Force -Path $target
javac -encoding UTF-8 --release 8 -d $target src/ua/ember/EffectEngine.java src/ua/ember/LedProtocol.java src/ua/ember/AudioAnalysis.java src/ua/ember/ReactiveEngine.java src/ua/ember/DeviceProfiles.java src/ua/ember/MicPresets.java src/ua/ember/ConnectionRetry.java tests/CoreTest.java tests/UpdateTest.java tests/ConnectionRetryTest.java
if($LASTEXITCODE -ne 0){throw 'Java tests compilation failed'}
java -cp $target UpdateTest
if($LASTEXITCODE -ne 0){throw 'Update tests failed'}
java -cp $target ConnectionRetryTest
if($LASTEXITCODE -ne 0){throw 'Connection recovery tests failed'}
java -cp $target CoreTest
if($LASTEXITCODE -ne 0){throw 'Java tests failed'}
Push-Location tests
try{node PackTest.cjs;if($LASTEXITCODE -ne 0){throw 'JSON tests failed'}}finally{Pop-Location}
