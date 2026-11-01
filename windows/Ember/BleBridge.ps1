# Windows PowerShell 5.1 provides the built-in WinRT projection. JSON lines over stdin/stdout.
$ErrorActionPreference = 'Stop'
[Console]::InputEncoding = New-Object System.Text.UTF8Encoding($false)
[Console]::OutputEncoding = New-Object System.Text.UTF8Encoding($false)
Add-Type -AssemblyName System.Runtime.WindowsRuntime
$source = [IO.File]::ReadAllText((Join-Path $PSScriptRoot 'Bridge\BleDiscovery.cs'))
$compiler=New-Object Microsoft.CSharp.CSharpCodeProvider
$parameters=New-Object System.CodeDom.Compiler.CompilerParameters
$parameters.GenerateInMemory=$true
foreach($reference in @('System.dll','System.Core.dll','System.Windows.Forms.dll','System.Drawing.dll',"$env:windir\System32\WinMetadata\Windows.Devices.winmd","$env:windir\System32\WinMetadata\Windows.Foundation.winmd","$env:windir\System32\WinMetadata\Windows.Storage.winmd","$env:windir\Microsoft.NET\Framework64\v4.0.30319\System.Runtime.WindowsRuntime.dll","$env:windir\Microsoft.NET\Framework64\v4.0.30319\System.Runtime.dll","$env:windir\Microsoft.NET\Framework64\v4.0.30319\System.Runtime.InteropServices.WindowsRuntime.dll")) { $null=$parameters.ReferencedAssemblies.Add($reference) }
$policy=[IO.File]::ReadAllText((Join-Path $PSScriptRoot 'Bridge\ConnectionPolicy.cs'))
$compiled=$compiler.CompileAssemblyFromSource($parameters,[string[]]@($source,$policy))
if($compiled.Errors.HasErrors){[Console]::WriteLine((@{ok=$false;error=($compiled.Errors | Out-String)} | ConvertTo-Json -Compress));exit 1}
$null = [Windows.Devices.Bluetooth.BluetoothLEDevice,Windows.Devices.Bluetooth,ContentType=WindowsRuntime]
$null = [Windows.Devices.Bluetooth.Advertisement.BluetoothLEAdvertisementWatcher,Windows.Devices.Bluetooth,ContentType=WindowsRuntime]
$null = [Windows.Devices.Bluetooth.GenericAttributeProfile.GattDeviceServicesResult,Windows.Devices.Bluetooth,ContentType=WindowsRuntime]
$null = [Windows.Devices.Bluetooth.GenericAttributeProfile.GattCharacteristicsResult,Windows.Devices.Bluetooth,ContentType=WindowsRuntime]
$null = [Windows.Storage.Streams.DataWriter,Windows.Storage.Streams,ContentType=WindowsRuntime]
$script:device = $null; $script:characteristic = $null; $script:services = @(); $script:session = $null
$asTask = [System.WindowsRuntimeSystemExtensions].GetMethods() | Where-Object { $_.Name -eq 'AsTask' -and $_.IsGenericMethod -and $_.GetParameters().Count -eq 1 -and $_.GetParameters()[0].ParameterType.Name -eq 'IAsyncOperation`1' } | Select-Object -First 1
function Await-Operation($operation, [Type]$resultType, [int]$timeoutMs=20000) {
 $task = $asTask.MakeGenericMethod($resultType).Invoke($null,@($operation))
 if (-not $task.Wait($timeoutMs)) { throw 'Bluetooth operation timed out.' }
 return $task.Result
}
function Connection-Budget($watch, [int]$maximum) {
 $remaining=68000-[int]$watch.Elapsed.TotalMilliseconds
 if($remaining -lt 1000){throw 'Bluetooth connection timed out.'}
 return [math]::Min($maximum,$remaining)
}
function Release-Device {
 $script:characteristic = $null
 if($null -ne $script:session){try{$script:session.MaintainConnection=$false;$script:session.Dispose()}catch{};$script:session=$null}
 foreach($service in $script:services) { if($null -ne $service) { $service.Dispose() } }
 $script:services = @()
 if($null -ne $script:device) { $script:device.Dispose(); $script:device = $null }
}
while($null -ne ($line = [Console]::ReadLine())) {
 try {
  $req = $line | ConvertFrom-Json
  $value = $null
  switch($req.command) {
   'scan' {
    $radio=[EmberWinRt]::RadioState()
    if($radio -eq 'Missing'){throw 'Bluetooth-адаптер не знайдено у Windows.'}
    if($radio -in @('Off','Disabled')){throw 'Bluetooth вимкнений у Windows. Увімкни його в параметрах Bluetooth.'}
    $scanner=New-Object EmberScanner
    try {
     $scanner.Start(); Start-Sleep -Seconds 12
     if($scanner.Status() -eq 'Aborted' -or $scanner.Error){throw ('Пошук Bluetooth перерваний Windows: '+$scanner.Error)}
     $scanner.Stop()
     $items=foreach($entry in $scanner.Snapshot()){
      $address=$entry.Address
      $mac=if($address){([UInt64]::Parse($address)).ToString('X12') -replace '(.{2})(?=.)','$1:'}else{''}
      $name=if([string]::IsNullOrWhiteSpace($entry.Name)){'BLE без назви'}else{$entry.Name.Trim()}
      $likely=$entry.ServiceHint -or ($name -match 'ELK|MELK|LED|Lamp|Triones|RGB|Light|Neon|Strip|BT[-_ ]?LED')
      @{address=$address;name=$name;rssi=$entry.Rssi;deviceId=$entry.DeviceId;addressType=$entry.AddressType;mac=$mac;source=$(if($entry.Advertising){'advertisement'}else{'windows'});likely=$likely;paired=$entry.Paired;connected=$entry.Connected}
     }
     $value=@{devices=@($items | Sort-Object @{Expression='likely';Descending=$true},@{Expression={$_.source -eq 'advertisement'};Descending=$true},@{Expression='rssi';Descending=$true});advertisedCount=$scanner.Found.Count;knownCount=$scanner.Known.Count;radio=$radio;enumerationError=$scanner.EnumerationError}
    }finally{$scanner.Stop()}
   }
   'connect' {
    Release-Device
    $address=if($req.address){[UInt64]::Parse($req.address)}else{[UInt64]0}
    $deviceId=[string]$req.deviceId
    $addressType=if($null -ne $req.addressType){[int]$req.addressType}else{2}
    $radio=[EmberWinRt]::RadioState()
    if($radio -eq 'Missing'){throw 'Bluetooth-адаптер не знайдено.'}
    if($radio -in @('Off','Disabled')){throw 'Bluetooth вимкнений у Windows. Увімкни його та повтори підключення.'}
    $scanner=New-Object EmberScanner
    $trace=New-Object 'System.Collections.Generic.List[string]'
    $watch=[Diagnostics.Stopwatch]::StartNew()
    $lastStatus='NotFound';$success=$false;$attempt=0;$signal='невідомий'
    try {
     $scanner.Start();Start-Sleep -Milliseconds 2200
     $fresh=@($scanner.Snapshot() | Where-Object {($address -ne 0 -and $_.Address -eq $address.ToString()) -or ($deviceId -and $_.DeviceId -eq $deviceId)} | Sort-Object @{Expression='Advertising';Descending=$true} | Select-Object -First 1)
     if($fresh.Count -gt 0){
      if($fresh[0].DeviceId){$deviceId=$fresh[0].DeviceId}
      if($fresh[0].Advertising){$addressType=$fresh[0].AddressType;$signal=[string]$fresh[0].Rssi+' dBm'}
      if($address -eq 0 -and $fresh[0].Address){$address=[UInt64]::Parse($fresh[0].Address)}
     }
     foreach($target in [EmberConnectionPolicy]::Plan($address,$deviceId,$addressType)){
      if($watch.Elapsed.TotalSeconds -gt 65){break}
      Release-Device
      if($attempt -gt 0){Start-Sleep -Milliseconds 650}
      $attempt++
      try {
       $script:device=[EmberWinRt]::OpenBounded($target.DeviceId,$address,$target.AddressType,(Connection-Budget $watch 8000))
       if($null -eq $script:device){$lastStatus='NotFound';$trace.Add($target.Label+': '+$lastStatus);continue}
       $script:session=[EmberWinRt]::Maintain($script:device,(Connection-Budget $watch 5000))
       $result=[EmberWinRt]::Discover($script:device,(Connection-Budget $watch 12000))
       $lastStatus=$result.Status.ToString()
       $script:services=@($result.Services)
       if($lastStatus -ne 'Success'){
        $trace.Add($target.Label+': '+$lastStatus)
        if(![EmberConnectionPolicy]::Retry($lastStatus)){break}
        continue
       }
       foreach($service in $script:services){
        $serviceId=$service.Uuid.ToString()
        $expected=switch($serviceId){
         '0000fff0-0000-1000-8000-00805f9b34fb' {'0000fff3-0000-1000-8000-00805f9b34fb'}
         '0000ffe0-0000-1000-8000-00805f9b34fb' {'0000ffe1-0000-1000-8000-00805f9b34fb'}
         '0000ffe5-0000-1000-8000-00805f9b34fb' {'0000ffe9-0000-1000-8000-00805f9b34fb'}
         '0000ffd5-0000-1000-8000-00805f9b34fb' {'0000ffd9-0000-1000-8000-00805f9b34fb'}
         default {''}
        }
        if(!$expected){continue}
        $chars=Await-Operation ($service.GetCharacteristicsAsync([Windows.Devices.Bluetooth.BluetoothCacheMode]::Uncached)) ([Windows.Devices.Bluetooth.GenericAttributeProfile.GattCharacteristicsResult]) (Connection-Budget $watch 10000)
        if($chars.Status.ToString() -ne 'Success'){$lastStatus=$chars.Status.ToString();continue}
        foreach($ch in $chars.Characteristics){
         if($ch.Uuid.ToString() -eq $expected -and (($ch.CharacteristicProperties -band [Windows.Devices.Bluetooth.GenericAttributeProfile.GattCharacteristicProperties]::WriteWithoutResponse) -ne 0 -or ($ch.CharacteristicProperties -band [Windows.Devices.Bluetooth.GenericAttributeProfile.GattCharacteristicProperties]::Write) -ne 0)){$script:characteristic=$ch;break}
        }
        if($null -ne $script:characteristic){break}
       }
       if($null -eq $script:characteristic -and $lastStatus -eq 'Success'){$lastStatus='Unsupported';$trace.Add($target.Label+': Unsupported');break}
       $wait=[Diagnostics.Stopwatch]::StartNew()
       while($script:device.ConnectionStatus.ToString() -ne 'Connected' -and $wait.Elapsed.TotalMilliseconds -lt 1500 -and $watch.Elapsed.TotalMilliseconds -lt 68000){Start-Sleep -Milliseconds 100}
       if([EmberConnectionPolicy]::Confirmed($lastStatus,$script:device.ConnectionStatus.ToString(),$null -ne $script:characteristic)){$success=$true;break}
       if($lastStatus -eq 'Success'){$lastStatus='Disconnected'}
       $trace.Add($target.Label+': '+$lastStatus)
       if(![EmberConnectionPolicy]::Retry($lastStatus)){break}
      }catch{
       $lastStatus=$_.Exception.GetBaseException().Message
       $trace.Add($target.Label+': '+$lastStatus)
       # A timed-out WinRT operation may still be queued by Windows. Do not pile up more work.
       if($lastStatus -match 'timed out|denied|access'){break}
      }
     }
     if(!$success){
      Release-Device
      $detail=($trace.ToArray() -join '; ')
      if($lastStatus -eq 'AccessDenied'){throw ('Windows заборонив доступ до Bluetooth. Перевір дозволи Bluetooth для програм. '+$detail)}
      if($lastStatus -eq 'Unsupported'){throw 'Зʼєднання встановлено, але протокол цієї стрічки поки не підтримується.'}
      throw ('Не вдалося встановити Bluetooth-зʼєднання зі стрічкою. Перевір, чи стрічка має живлення. Якщо увімкнена, закрий програми стрічки на телефоні або вимкни Bluetooth телефона; перезапусти живлення стрічки та повтори. GATT: '+$lastStatus+' · сигнал: '+$signal+' · спроби: '+$attempt+'. '+$detail)
     }
     $value=@{name=$script:device.Name;uuid=$script:characteristic.Uuid.ToString();status=$script:device.ConnectionStatus.ToString();address=$script:device.BluetoothAddress.ToString();deviceId=$script:device.DeviceId;addressType=[int]$script:device.BluetoothAddressType;attempts=$attempt;connectionMs=[int]$watch.Elapsed.TotalMilliseconds;recovery=($trace.ToArray() -join '; ')}
    }finally{$scanner.Stop()}
   }
   'write' {
    if($null -eq $script:characteristic -or $script:device.ConnectionStatus.ToString() -ne 'Connected') { throw 'Bluetooth disconnected.' }
    $status=[EmberWinRt]::Write($script:characteristic,[byte[]]$req.bytes)
    if($status -ne 'Success') { throw ('GATT write: ' + $status) }
    $value=@{sent=$true}
   }
   'selftest' {
    $sample=[byte[]]@(126,7,5,3,255,90,31,16,239)
    $roundtrip=[EmberWinRt]::BufferRoundTrip($sample)
    $value=@{buffer=[BitConverter]::ToString($roundtrip).Replace('-','');withResponse=[EmberWinRt]::WriteOption([Windows.Devices.Bluetooth.GenericAttributeProfile.GattCharacteristicProperties]::Write).ToString();withoutResponse=[EmberWinRt]::WriteOption([Windows.Devices.Bluetooth.GenericAttributeProfile.GattCharacteristicProperties]::WriteWithoutResponse).ToString()}
   }
   'radio' { $value=@{radio=[EmberWinRt]::RadioState()} }
   'status' { $value = @{connected=($null -ne $script:device -and $script:device.ConnectionStatus.ToString() -eq 'Connected')} }
   'disconnect' { Release-Device; $value = @{connected=$false} }
   'exit' { Release-Device; exit }
   default { throw 'Unknown bridge command.' }
  }
  [Console]::WriteLine((@{ok=$true;data=$value} | ConvertTo-Json -Depth 8 -Compress))
 } catch { $link=$false;try{$link=($null -ne $script:device -and $script:device.ConnectionStatus.ToString() -eq 'Connected')}catch{}; [Console]::WriteLine((@{ok=$false;error=$_.Exception.Message;connected=$link} | ConvertTo-Json -Compress)) }
}
Release-Device
