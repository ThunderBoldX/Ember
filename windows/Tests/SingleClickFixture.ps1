$clock=[Diagnostics.Stopwatch]::StartNew()
$last=-10000.0;$previousRgb=$false;$mode=-1;$mic=$true;$dropped=0
$writes=New-Object 'System.Collections.Generic.List[object]'
while($null -ne ($line=[Console]::ReadLine())){
 $req=$line|ConvertFrom-Json;$data=@{}
 switch($req.command){
  'connect' {$data=@{uuid='0000fff3-0000-1000-8000-00805f9b34fb';name='ELK-BLEDDM Fixture';address='1';deviceId='fixture';addressType=0;status='Connected';attempts=1}}
  'write' {
   $bytes=@($req.bytes);$rgb=$bytes[2] -eq 5;$now=$clock.Elapsed.TotalMilliseconds
   # Reproduce firmware that acknowledges transport but silently drops rapid control writes.
   $accepted=($now-$last) -ge $(if($rgb -and $previousRgb){10}else{250})
   $writes.Add(@{command=$bytes[2];value=$bytes[3];at=$now;accepted=$accepted})
   if(!$accepted){$dropped++}else{
    if($bytes[2] -eq 7){$mic=$bytes[3] -ne 0}
    if($bytes[2] -eq 3 -and $bytes[3] -ge 135){if(!$mic){$mode=$bytes[3]}else{$dropped++}}
    if($rgb -and !$mic){$mode=-1}
   }
   $last=$now;$previousRgb=$rgb;$data=@{sent=$true}
  }
  'selftest' {$data=@{mode=$mode;mic=$mic;dropped=$dropped;writes=$writes.ToArray()}}
  'status' {$data=@{connected=$true}}
  'disconnect' {$data=@{connected=$false}}
  'exit' {exit}
 }
 [Console]::WriteLine((@{ok=$true;data=$data}|ConvertTo-Json -Depth 8 -Compress))
}
