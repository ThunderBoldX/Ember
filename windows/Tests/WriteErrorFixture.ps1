$writes=0
$connects=0
while($null -ne ($line=[Console]::ReadLine())) {
 $request=$line | ConvertFrom-Json
 switch($request.command) {
  'connect' { $connects++;if($connects -eq 2){[Console]::WriteLine('{"ok":false,"error":"GATT: Unreachable","connected":false}')}else{[Console]::WriteLine('{"ok":true,"data":{"uuid":"0000fff3-0000-1000-8000-00805f9b34fb","name":"ELK-BLEDDM Fixture","address":"1","deviceId":"fixture","addressType":0,"status":"Connected","attempts":2}}')} }
  'write' { $writes++; if($writes -eq 1){[Console]::WriteLine('{"ok":false,"error":"Synthetic API failure","connected":true}')}else{[Console]::WriteLine('{"ok":false,"error":"Synthetic link loss","connected":false}')} }
  'exit' { exit }
 }
}
