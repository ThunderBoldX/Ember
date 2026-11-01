using Ember;
using System;
using System.Linq;
using System.Text.Json;
public static class UpdateTests
{
 public static void Run(Action<bool,string> check)
 {
  check(DeviceProfiles.Detect("ELK-BLEDOB","0000fff3")==0,"automatic BLEDOB detection");
  check(DeviceProfiles.Detect("MELK-123","0000ffe1")==1,"automatic MELK detection");
  check(DeviceProfiles.Detect("ELK-BLEDOM","0000ffe9")==2,"alternate ELK characteristic");
  check(DeviceProfiles.Detect("ELK-BLEDDM    ","0000fff3-0000-1000-8000-00805f9b34fb")==2,"user ELK-BLEDDM controller detected");
  var targets=EmberConnectionPolicy.Plan(18838586676582UL,"cached-windows-id",0);
  check(targets[0].DeviceId==""&&targets[0].AddressType==0,"live MAC connection precedes Windows cache");
  check(targets[1].DeviceId==""&&targets[1].AddressType==0,"unreachable MAC gets a fresh retry");
  check(targets[2].DeviceId=="cached-windows-id"&&targets[3].AddressType==2,"Windows ID and inferred address fallback retained");
  targets=EmberConnectionPolicy.Plan(1UL,"",2);
  check(targets.Select(t=>t.AddressType).SequenceEqual(new[]{2,0,1}),"unknown address type supports public and random devices");
  check(EmberConnectionPolicy.Plan(0UL,"id",2).Length==2&&EmberConnectionPolicy.Plan(0UL,"",2).Length==0,"ID-only retry and empty target handling");
  check(new[]{"Unreachable","ProtocolError","Disconnected","NotFound"}.All(EmberConnectionPolicy.Retry),"transient GATT failures retry");
  check(!EmberConnectionPolicy.Retry("AccessDenied")&&!EmberConnectionPolicy.Retry("Unsupported"),"permission and unsupported protocol failures stop");
  check(!EmberConnectionPolicy.Confirmed("Success","Disconnected",true),"cached services alone do not imply connection");
  check(!EmberConnectionPolicy.Confirmed("Success","Connected",false)&&!EmberConnectionPolicy.Confirmed("Unreachable","Connected",true),"connected status still requires usable GATT");
  check(EmberConnectionPolicy.Confirmed("Success","Connected",true),"connected writable GATT confirmed");
  check(DeviceProfiles.Detect("Triones","0000ffd9")==3,"Triones detection");
  bool rejected=false;try{DeviceProfiles.Detect("Unknown","0000fff3");}catch(NotSupportedException){rejected=true;}check(rejected,"unknown controller is not assigned a guessed protocol");
  var adapter=new ProtocolAdapter{Profile=3};
  check(Convert.ToHexString(adapter.Convert(Protocol.Color(255,90,31)))=="56FF5A1F00F0AA","Triones RGB packet");
  check(Convert.ToHexString(adapter.Convert(Protocol.Brightness(50)))=="567F2D0F00F0AA","Triones brightness scales original RGB");
  check(Convert.ToHexString(adapter.Convert(Protocol.Power(false)))=="CC2433","Triones power packet");
  check(adapter.Convert(Protocol.StripMicPower(false)).Length==0,"unsupported mic disable does not send a foreign command");
  rejected=false;try{adapter.Convert(Protocol.StripMicPower(true));}catch(NotSupportedException){rejected=true;}check(rejected,"Triones mic unsupported explicitly");
  check(Enumerable.Range(135,15).All(n=>ProtocolAdapter.MapMode(n)>=0x25&&ProtocolAdapter.MapMode(n)<=0x38),"Triones uses documented mode range");
  check(ModeCatalog.StripMicrophone.Length==16&&ModeCatalog.StripMicrophone.Select(p=>p.Id).Distinct().Count()==16,"sixteen distinct strip microphone presets");
  check(Enumerable.Range(0,16).Select(i=>(ModeCatalog.MicModes[i],ModeCatalog.MicSensitivity[i])).Distinct().Count()==16,"sixteen different native mode and sensitivity combinations");
  check(ReactiveEffects.Music.Length==16,"sixteen system music effects");
  var settings=new Settings{Address="1",DeviceId="one",Name="First"};settings.Remember();settings.Address="2";settings.DeviceId="two";settings.Name="Second";settings.Remember();settings.Remember();
  var restored=JsonSerializer.Deserialize<Settings>(JsonSerializer.Serialize(settings))!;
  check(restored.Devices.Count==2&&restored.Devices[0].Address=="2","multiple saved devices survive JSON round trip and deduplicate");
  var analysis=new AudioAnalyzer();AudioFrame? frame=null;for(int i=0;i<3;i++)frame=analysis.Push(new float[480],48000)??frame;
  check(frame!=null,"audio analyzer responds within 30 ms at 48 kHz");
 }
}
