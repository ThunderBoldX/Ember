using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
namespace Ember;
public record LampDevice(string Address,string Name,int? Rssi,string DeviceId="",int AddressType=2,string Source="advertisement",bool Likely=false) {
 public string Mac=>ulong.TryParse(Address,NumberStyles.None,CultureInfo.InvariantCulture,out var value)?string.Join(":",Enumerable.Range(0,6).Select(i=>value.ToString("X12").Substring(i*2,2))):"";
 public string Signal=>Rssi.HasValue?$"{Rssi} dBm":Source=="saved"?"збережений":"Windows";
 public string DisplayName=>$"{Name} · {(Mac.Length>0?Mac+" · ":"")}{Signal}";
 public override string ToString()=>DisplayName;
}
public sealed class BluetoothBridgeException : IOException {
 public bool? LinkConnected{get;}
 public BluetoothBridgeException(string? message,bool? connected):base(message){LinkConnected=connected;}
}
public sealed class BluetoothService : IDisposable
{
 private Process? process; private readonly SemaphoreSlim gate=new(1,1); private long? lastWrite;private bool lastWasFrame;
 private readonly string bridgePath; private readonly ProtocolAdapter adapter=new(); private bool disposed;
 public int Profile=>adapter.Profile; public bool HasMicrophone=>DeviceProfiles.HasMicrophone(Profile);
 public BluetoothService(string? bridgePath=null){this.bridgePath=bridgePath??Path.Combine(AppContext.BaseDirectory,"BleBridge.ps1");}
 public bool Connected {get;private set;} public int Sent {get;private set;} public double Latency {get;private set;} public string Uuid {get;private set;}="—";
 public string LastConnectionNote{get;private set;}="";
 public string LastScanNote{get;private set;}="";public string ConnectedName{get;private set;}="";public string ConnectedAddress{get;private set;}="";public string ConnectedDeviceId{get;private set;}="";public int ConnectedAddressType{get;private set;}=2;
 public event Action<string>? Lost;
 private void Start()
 {
  if(process is {HasExited:false}) return;
  var path=bridgePath;
  process=new Process {StartInfo=new ProcessStartInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows),"System32","WindowsPowerShell","v1.0","powershell.exe")) {UseShellExecute=false,CreateNoWindow=true,RedirectStandardInput=true,RedirectStandardOutput=true,RedirectStandardError=true,StandardOutputEncoding=new UTF8Encoding(false),StandardInputEncoding=new UTF8Encoding(false)}};
  foreach(var arg in new[]{"-NoLogo","-NoProfile","-NonInteractive","-STA","-ExecutionPolicy","Bypass","-File",path}) process.StartInfo.ArgumentList.Add(arg);
  process.Start(); process.BeginErrorReadLine();
 }
 private async Task<JsonElement> Request(object request)
 {
  if(disposed)throw new ObjectDisposedException(nameof(BluetoothService)); Start(); await process!.StandardInput.WriteLineAsync(JsonSerializer.Serialize(request)); await process.StandardInput.FlushAsync();
  string? line;
  try { line=await process.StandardOutput.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(100)); }
  catch { process.Kill(true); Connected=false; throw new IOException("Bluetooth не відповідає. Повтори підключення."); }
  if(line is null) throw new IOException("Bluetooth-служба завершилася.");
  using var doc=JsonDocument.Parse(line);
  if(!doc.RootElement.GetProperty("ok").GetBoolean()) throw ParseError(doc.RootElement);
  return doc.RootElement.GetProperty("data").Clone();
 }
 public async Task<List<LampDevice>> Scan()
 {
  await gate.WaitAsync(); try { var data=await Request(new{command="scan"});var list=ParseScan(data);LastScanNote=$"У радіусі: {data.GetProperty("advertisedCount").GetInt32()} · Windows: {data.GetProperty("knownCount").GetInt32()}";var error=data.GetProperty("enumerationError").GetString();if(!string.IsNullOrWhiteSpace(error))LastScanNote+=" · Windows обмежив системний список; пошук за BLE-сигналом працює.";return list; } finally {gate.Release();}
 }
 public static List<LampDevice> ParseScan(JsonElement data) {
  var list=new List<LampDevice>();var devices=data.ValueKind==JsonValueKind.Array?data:data.GetProperty("devices");
  foreach(var d in devices.EnumerateArray()){
   string S(string key)=>d.TryGetProperty(key,out var value)&&value.ValueKind==JsonValueKind.String?value.GetString()??"":"";
   int? N(string key)=>d.TryGetProperty(key,out var value)&&value.ValueKind==JsonValueKind.Number&&value.TryGetInt32(out var number)?number:null;
   var name=S("name");list.Add(new(S("address"),string.IsNullOrWhiteSpace(name)?"BLE без назви":name.Trim(),N("rssi"),S("deviceId"),N("addressType")??2,S("source"),d.TryGetProperty("likely",out var likely)&&likely.ValueKind==JsonValueKind.True));
  }
 return list;
 }
 public static BluetoothBridgeException ParseError(JsonElement reply)=>new(reply.GetProperty("error").GetString(),reply.TryGetProperty("connected",out var link)&&link.ValueKind is JsonValueKind.True or JsonValueKind.False?link.GetBoolean():null);
 public async Task<JsonElement> SelfTest(){await gate.WaitAsync();try{return await Request(new{command="selftest"});}finally{gate.Release();}}
 public async Task Connect(string address,string deviceId="",int addressType=2,string nameHint="")
 {
  await gate.WaitAsync(); try { Connected=false;LastConnectionNote="";var data=await Request(new{command="connect",address,deviceId,addressType});if(data.TryGetProperty("attempts",out var attempts))LastConnectionNote="Спроб підключення: "+attempts.GetInt32(); Uuid=data.GetProperty("uuid").GetString()!;ConnectedName=data.GetProperty("name").GetString()??"";ConnectedAddress=data.GetProperty("address").GetString()??address;ConnectedDeviceId=data.GetProperty("deviceId").GetString()??deviceId;ConnectedAddressType=data.GetProperty("addressType").GetInt32();Connected=data.GetProperty("status").GetString()=="Connected"; try{if(string.IsNullOrWhiteSpace(ConnectedName))ConnectedName=nameHint.Replace("★ ","");adapter.Profile=DeviceProfiles.Detect(ConnectedName,Uuid);}catch{await Request(new{command="disconnect"});Connected=false;throw;}if(!Connected)throw new IOException("Windows відкрив пристрій, але Bluetooth-зʼєднання ще не встановлено. Повтори підключення."); } finally {gate.Release();}
 }
 public Task Write(byte[] bytes)=>WritePacket(bytes,false);
 public Task WriteFrame(byte[] bytes)=>WritePacket(bytes,true);
 private async Task WritePacket(byte[] bytes,bool frame)
 {
  await gate.WaitAsync(); try {
   if(!Connected) throw new IOException("Спочатку підключи стрічку.");
   bytes=adapter.Convert(bytes);if(bytes.Length==0)return;
   // Firmware needs time for mode/power/settings commands; successive music frames stay fast.
   int gap=frame&&lastWasFrame?(Profile==0?30:15):(Profile==2?300:Profile==0?100:15);
   if(lastWrite.HasValue){var delay=gap-Stopwatch.GetElapsedTime(lastWrite.Value).TotalMilliseconds;if(delay>0)await Task.Delay((int)Math.Ceiling(delay));}
   var watch=Stopwatch.StartNew(); await Request(new{command="write",bytes=Array.ConvertAll(bytes,x=>(int)x)}); lastWrite=Stopwatch.GetTimestamp();lastWasFrame=frame; Latency=watch.Elapsed.TotalMilliseconds; Sent++;
  } catch(Exception ex) {Connected=ex is BluetoothBridgeException error&&error.LinkConnected==true;if(!Connected)Lost?.Invoke(ex.Message);throw;} finally {gate.Release();}
 }
 public async Task<bool> Poll()
 {
  if(!Connected||!await gate.WaitAsync(0)) return Connected;
  try {var d=await Request(new{command="status"}); Connected=d.GetProperty("connected").GetBoolean(); if(!Connected) Lost?.Invoke("Стрічка від'єдналася."); return Connected;} catch {Connected=false; Lost?.Invoke("Bluetooth з'єднання втрачено.");return false;} finally {gate.Release();}
 }
 public async Task Disconnect() {await gate.WaitAsync();try {await Request(new{command="disconnect"}); Connected=false;}finally {gate.Release();}}
 public void Abort(){Connected=false;try{if(process is {HasExited:false})process.Kill(true);}catch{}}
 public void Dispose() {if(disposed)return;disposed=true;if(process is {HasExited:false}) {try {process.StandardInput.WriteLine("{\"command\":\"exit\"}"); if(!process.WaitForExit(1500)) process.Kill(true);}catch{} } process?.Dispose();}
}
