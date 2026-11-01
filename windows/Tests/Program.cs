using Ember;
using System.Numerics;
using System.Media;
using System.Runtime.InteropServices;
void Assert(bool ok,string name){if(!ok)throw new Exception("FAIL "+name);Console.WriteLine("PASS "+name);}
Assert(Convert.ToHexString(Protocol.Color(255,90,31))=="7E070503FF5A1F10EF","RGB packet");
Assert(Convert.ToHexString(Protocol.Mode(138,0))=="7E07038A03FFFF00EF","BLEDOB effect packet");
Assert(Convert.ToHexString(Protocol.Brightness(50,0))=="7E04013201FF0201EF","BLEDOB brightness packet");
Assert(Convert.ToHexString(Protocol.Speed(50,1))=="7E040232FFFFFF00EF","MELK speed packet");
Assert(Convert.ToHexString(Protocol.Mode(138,2))=="7E05038A03FFFF00EF","BLEDDM effect packet");
Assert(Convert.ToHexString(Protocol.Power(true,2))=="7E0404F00001FF00EF","BLEDDM power on packet");
Assert(Convert.ToHexString(Protocol.Power(false,2))=="7E0404000000FF00EF","BLEDDM power off packet");
bool invalid=false;try{Protocol.Color(256,0,0);}catch(ArgumentOutOfRangeException){invalid=true;}Assert(invalid,"invalid color rejected");
var bins=new Complex[2048];for(int i=0;i<bins.Length;i++)bins[i]=Math.Sin(2*Math.PI*64*i/bins.Length);AudioAnalyzer.Transform(bins);Assert(Array.IndexOf(bins.Take(1024).Select(x=>x.Magnitude).ToArray(),bins.Take(1024).Max(x=>x.Magnitude))==64,"FFT exact-bin sine");
var analyzer=new AudioAnalyzer();AudioFrame? frame=null;for(int offset=0;offset<12000;offset+=480){var samples=Enumerable.Range(offset,480).Select(i=>(float)(.5*Math.Sin(2*Math.PI*1000*i/48000))).ToArray();frame=analyzer.Push(samples,48000)??frame;}
Assert(frame!=null&&Math.Abs(frame.Rms-.35355)<.01,"RMS calibrated sine");Assert(frame!.Mid>frame.Bass*5&&frame.Mid>frame.Treble*5,"frequency-band attribution");Assert(frame.Spectrum.All(x=>double.IsFinite(x)&&x>=0&&x<=1),"spectrum normalized");
var silence=new AudioAnalyzer();for(int i=0;i<12;i++)frame=silence.Push(new float[480],48000)??frame;Assert(frame!.Rms==0&&frame.Peak==0&&!frame.Beat&&frame.Bpm==0,"silence has no invented beat or BPM");
using(var scanJson=System.Text.Json.JsonDocument.Parse("""
{"devices":[{"address":"18838586676582","name":"Controller 42","rssi":-61,"deviceId":"","addressType":1,"source":"advertisement","likely":false},{"address":"2","name":"","rssi":-70,"deviceId":"","addressType":0,"source":"advertisement"},{"address":"","name":"Cached device","rssi":null,"deviceId":"windows-device-id","addressType":null,"source":"windows"}]}
""")){
 var found=BluetoothService.ParseScan(scanJson.RootElement);
 Assert(found.Count==3&&found[0].Name=="Controller 42","scan retains arbitrary device names");
 Assert(found[1].Name=="BLE без назви","scan retains unnamed devices");
 Assert(found[2].Rssi==null&&found[2].DeviceId=="windows-device-id","Windows discovery without invented RSSI");
 Assert(found[0].AddressType==1&&found[0].Mac=="11:22:33:44:55:66","random address type and MAC preserved");
}

using(var fixture=new BluetoothService(Path.Combine(AppContext.BaseDirectory,"WriteErrorFixture.ps1"))){
 int lost=0;fixture.Lost+=_=>lost++;await fixture.Connect("1");
 try{await fixture.Write(Protocol.Color(255,90,31));Assert(false,"fixture must fail its first write");}catch(BluetoothBridgeException){}
 Assert(fixture.Connected&&lost==0&&fixture.Sent==0,"write API error preserves live Bluetooth connection");
 try{await fixture.Connect("2");Assert(false,"second connection must fail");}catch(BluetoothBridgeException){}
 Assert(!fixture.Connected&&lost==0,"failed reconnect clears stale connected state");
 await fixture.Connect("1");
 Assert(fixture.Connected&&fixture.LastConnectionNote.Contains("2"),"successful recovery reports connection attempts");
 try{await fixture.Write(Protocol.Color(255,90,31));Assert(false,"fixture must fail its second write");}catch(BluetoothBridgeException){}
 Assert(!fixture.Connected&&lost==1,"actual link loss raises Lost once");
}
using(var native=new BluetoothService()){
 var result=await native.SelfTest();
 Assert(result.GetProperty("buffer").GetString()=="7E070503FF5A1F10EF","native WinRT IBuffer round trip through C# bridge");
 Assert(result.GetProperty("withResponse").GetString()=="WriteWithResponse","native acknowledged GATT write option");
 Assert(result.GetProperty("withoutResponse").GetString()=="WriteWithoutResponse","native unacknowledged GATT write option");
}
UpdateTests.Run(Assert);StudioTests.Run(Assert);GateTests.Run(Assert);
 if(args.Contains("--audio")) {
 using var audio=new WasapiAudio();int count=0;double maxRms=0;string? error=null;audio.Frame+=f=>{Interlocked.Increment(ref count);maxRms=Math.Max(maxRms,f.Rms);};audio.Failed+=s=>error=s;await audio.StartReady().WaitAsync(TimeSpan.FromSeconds(10));Assert(audio.SampleRate>0,"native audio startup completes before playback");await Task.Delay(300);
 // Quiet 1 kHz diagnostic tone, played for 0.6 seconds. Captured through the default Windows output.
 var wav=new MemoryStream();using(var writer=new BinaryWriter(wav,System.Text.Encoding.ASCII,true)){int samples=28800;writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));writer.Write(36+samples*2);writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));writer.Write(16);writer.Write((short)1);writer.Write((short)1);writer.Write(48000);writer.Write(96000);writer.Write((short)2);writer.Write((short)16);writer.Write(System.Text.Encoding.ASCII.GetBytes("data"));writer.Write(samples*2);for(int i=0;i<samples;i++)writer.Write((short)(1600*Math.Sin(2*Math.PI*1000*i/48000)));}wav.Position=0;
 PlaySound(wav.ToArray(),IntPtr.Zero,4);await Task.Delay(400);await audio.Stop();Console.WriteLine($"WASAPI frames={count} rms={maxRms:0.0000} rate={audio.SampleRate} error={error}");Assert(error==null&&count>0&&maxRms>0,"native WASAPI real loopback");
}
if(args.Contains("--ble")){using var ble=new BluetoothService();var devices=await ble.Scan();Assert(devices.Count>0,"Bluetooth bridge scan end to end");foreach(var device in devices)Console.WriteLine($"BLE {device.Name} {device.Rssi} dBm");}
[DllImport("winmm.dll",SetLastError=true)]static extern bool PlaySound(byte[] data,IntPtr module,uint flags);
