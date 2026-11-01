param([string]$Version,[string]$OutputDirectory)
$ErrorActionPreference='Stop'
if(!$OutputDirectory){$OutputDirectory=Join-Path $PSScriptRoot 'runtime'}
$metadata=Invoke-RestMethod 'https://builds.dotnet.microsoft.com/dotnet/release-metadata/10.0/releases.json'
if(!$Version){$Version=$metadata.'latest-runtime'}
$release=@($metadata.releases | Where-Object {$_.runtime.version -eq $Version}) | Select-Object -First 1
if(!$release){throw "Runtime $Version was not found in the official .NET 10 release metadata."}
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
Add-Type -AssemblyName System.IO.Compression.FileSystem
foreach($component in @('runtime','windowsdesktop')){
 $file=@($release.$component.files | Where-Object {$_.rid -eq 'win-x64' -and $_.url.EndsWith('.zip')}) | Select-Object -First 1
 if(!$file){throw "No win-x64 ZIP found for $component $Version"}
 $archive=Join-Path $OutputDirectory ($component+'.zip')
 Invoke-WebRequest -Uri $file.url -OutFile $archive -UseBasicParsing
 if((Get-FileHash -LiteralPath $archive -Algorithm SHA512).Hash.ToLowerInvariant() -ne $file.hash.ToLowerInvariant()){throw 'Runtime archive checksum mismatch'}
 $zip=[IO.Compression.ZipFile]::OpenRead($archive)
 try{
  foreach($entry in $zip.Entries){
   if(!$entry.Name){continue}
   $path=[IO.Path]::GetFullPath((Join-Path $OutputDirectory $entry.FullName))
   if(!$path.StartsWith([IO.Path]::GetFullPath($OutputDirectory)+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'Unexpected archive path'}
   New-Item -ItemType Directory -Path (Split-Path $path) -Force | Out-Null
   [IO.Compression.ZipFileExtensions]::ExtractToFile($entry,$path,$true)
   if($entry.FullName -in @('LICENSE.txt','ThirdPartyNotices.txt')){
    $noticeDirectory=Join-Path $OutputDirectory ('licenses\'+$component)
    New-Item -ItemType Directory -Path $noticeDirectory -Force | Out-Null
    [IO.Compression.ZipFileExtensions]::ExtractToFile($entry,(Join-Path $noticeDirectory $entry.Name),$true)
   }
  }
 }finally{$zip.Dispose()}
 Remove-Item -LiteralPath $archive
}
if(!(Test-Path (Join-Path $OutputDirectory 'dotnet.exe'))){throw 'Runtime host missing'}
Write-Host "Runtime $Version ready in $OutputDirectory"
