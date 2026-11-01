using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.Advertisement;
using Windows.Devices.Enumeration;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Storage.Streams;

public sealed class EmberSeen {
 public string Address="", Name="", DeviceId="";
 public int? Rssi;
 public int AddressType=2;
 public bool ServiceHint, Advertising, Connected, Paired;
}
public sealed class EmberScanner {
 public readonly ConcurrentDictionary<string,EmberSeen> Found=new ConcurrentDictionary<string,EmberSeen>();
 public readonly ConcurrentDictionary<string,EmberSeen> Known=new ConcurrentDictionary<string,EmberSeen>();
 readonly Dictionary<string,DeviceInformation> info=new Dictionary<string,DeviceInformation>();
 readonly object sync=new object();
 readonly BluetoothLEAdvertisementWatcher watcher=new BluetoothLEAdvertisementWatcher();
 readonly List<DeviceWatcher> enumerators=new List<DeviceWatcher>();
 public string Error="", EnumerationError="";
 public EmberScanner() {
  watcher.ScanningMode=BluetoothLEScanningMode.Active;
  watcher.Received+=(s,e)=>{
   if(e.BluetoothAddress==0)return;
   string key=e.BluetoothAddress.ToString(CultureInfo.InvariantCulture);
   EmberSeen prior;Found.TryGetValue(key,out prior);
   string name=e.Advertisement.LocalName;
   if(string.IsNullOrWhiteSpace(name)&&prior!=null)name=prior.Name;
   bool hint=e.Advertisement.ServiceUuids.Any(x=>x==new Guid("0000fff0-0000-1000-8000-00805f9b34fb")||x==new Guid("0000ffe0-0000-1000-8000-00805f9b34fb")||x==new Guid("0000ffe5-0000-1000-8000-00805f9b34fb")||x==new Guid("0000ffd5-0000-1000-8000-00805f9b34fb"));
   Found[key]=new EmberSeen{Address=key,Name=name??"",Rssi=e.RawSignalStrengthInDBm,AddressType=(int)e.BluetoothAddressType,Advertising=true,ServiceHint=hint||(prior!=null&&prior.ServiceHint)};
  };
  watcher.Stopped+=(s,e)=>{if(e.Error!=BluetoothError.Success)Error=e.Error.ToString();};
  foreach(bool paired in new[]{false,true}) {
   var dw=DeviceInformation.CreateWatcher(BluetoothLEDevice.GetDeviceSelectorFromPairingState(paired),new[]{"System.Devices.Aep.DeviceAddress","System.Devices.Aep.IsConnected"},DeviceInformationKind.AssociationEndpoint);
   dw.Added+=(s,e)=>{lock(sync){info[e.Id]=e;Known[e.Id]=ConvertInfo(e);}};
   dw.Updated+=(s,e)=>{lock(sync){DeviceInformation item;if(info.TryGetValue(e.Id,out item)){item.Update(e);Known[e.Id]=ConvertInfo(item);}}};
   dw.Removed+=(s,e)=>{lock(sync){info.Remove(e.Id);EmberSeen removed;Known.TryRemove(e.Id,out removed);}};
   enumerators.Add(dw);
  }
 }
 static EmberSeen ConvertInfo(DeviceInformation d) {
  object value;string address="";
  if(d.Properties.TryGetValue("System.Devices.Aep.DeviceAddress",out value)&&value!=null){
   string raw=value.ToString().Replace(":","").Replace("-","");ulong number;
   if(ulong.TryParse(raw,NumberStyles.HexNumber,CultureInfo.InvariantCulture,out number)&&number!=0)address=number.ToString(CultureInfo.InvariantCulture);
  }
  bool connected=d.Properties.TryGetValue("System.Devices.Aep.IsConnected",out value)&&value is bool&&(bool)value;
  return new EmberSeen{Address=address,Name=d.Name??"",DeviceId=d.Id,Connected=connected,Paired=d.Pairing.IsPaired};
 }
 public void Start() {
  watcher.Start();
  foreach(var dw in enumerators)try{dw.Start();}catch(Exception ex){EnumerationError=ex.Message;}
 }
 public void Stop() {
  if(watcher.Status==BluetoothLEAdvertisementWatcherStatus.Started)watcher.Stop();
  foreach(var dw in enumerators)if(dw.Status==DeviceWatcherStatus.Started||dw.Status==DeviceWatcherStatus.EnumerationCompleted)dw.Stop();
 }
 public string Status(){return watcher.Status.ToString();}
 public EmberSeen[] Snapshot() {
  var merged=new Dictionary<string,EmberSeen>();
  foreach(var item in Known.Values)merged[string.IsNullOrEmpty(item.Address)?item.DeviceId:item.Address]=item;
  foreach(var ad in Found.Values){
   EmberSeen known;merged.TryGetValue(ad.Address,out known);
   merged[ad.Address]=new EmberSeen{Address=ad.Address,Name=string.IsNullOrWhiteSpace(ad.Name)&&known!=null?known.Name:ad.Name,Rssi=ad.Rssi,AddressType=ad.AddressType,DeviceId=known==null?"":known.DeviceId,Advertising=true,ServiceHint=ad.ServiceHint,Connected=known!=null&&known.Connected,Paired=known!=null&&known.Paired};
  }
  return merged.Values.ToArray();
 }
}
public static class EmberWinRt {
 public static GattWriteOption WriteOption(GattCharacteristicProperties properties) {
  if((properties&GattCharacteristicProperties.WriteWithoutResponse)!=0)return GattWriteOption.WriteWithoutResponse;
  if((properties&GattCharacteristicProperties.Write)!=0)return GattWriteOption.WriteWithResponse;
  throw new InvalidOperationException("The GATT characteristic does not support writing.");
 }
 public static string Write(object nativeCharacteristic,byte[] bytes) {
  if(bytes==null||bytes.Length==0)throw new ArgumentException("Empty Bluetooth packet.");
  var characteristic=(GattCharacteristic)nativeCharacteristic;
  using(var writer=new DataWriter()) {
   writer.WriteBytes(bytes);
   IBuffer buffer=writer.DetachBuffer();
   var task=Operation<GattCommunicationStatus>(characteristic.WriteValueAsync(buffer,WriteOption(characteristic.CharacteristicProperties)));
   if(!task.Wait(20000))throw new TimeoutException("GATT write timed out.");
   return task.Result.ToString();
  }
 }
 public static byte[] BufferRoundTrip(byte[] bytes) {
  using(var writer=new DataWriter()) {
   writer.WriteBytes(bytes);
   using(var reader=DataReader.FromBuffer(writer.DetachBuffer())) {
    var result=new byte[reader.UnconsumedBufferLength];reader.ReadBytes(result);return result;
   }
  }
 }
 static Task<T> Operation<T>(object operation) {
  var method=typeof(System.WindowsRuntimeSystemExtensions).GetMethods().First(m=>m.Name=="AsTask"&&m.IsGenericMethod&&m.GetGenericArguments().Length==1&&m.GetParameters().Length==1&&m.GetParameters()[0].ParameterType.Name=="IAsyncOperation"+(char)96+"1");
  return (Task<T>)method.MakeGenericMethod(typeof(T)).Invoke(null,new[]{operation});
 }
 public static GattSession Maintain(object nativeDevice,int timeoutMs) {
  var device=(BluetoothLEDevice)nativeDevice;
  var task=Operation<GattSession>(GattSession.FromDeviceIdAsync(device.BluetoothDeviceId));
  if(!task.Wait(timeoutMs)){
   task.ContinueWith(t=>{if(t.Status==TaskStatus.RanToCompletion&&t.Result!=null)t.Result.Dispose();});
   throw new TimeoutException("GATT session creation timed out.");
  }
  var session=task.Result;if(session==null)throw new InvalidOperationException("Windows did not create a GATT session.");
  try{if(session.CanMaintainConnection)session.MaintainConnection=true;return session;}catch{session.Dispose();throw;}
 }
 public static GattDeviceServicesResult Discover(object nativeDevice,int timeoutMs) {
  var device=(BluetoothLEDevice)nativeDevice;
  var task=Operation<GattDeviceServicesResult>(device.GetGattServicesAsync(BluetoothCacheMode.Uncached));
  if(!task.Wait(timeoutMs)){
   task.ContinueWith(t=>{if(t.Status==TaskStatus.RanToCompletion&&t.Result!=null)foreach(var service in t.Result.Services)service.Dispose();});
   throw new TimeoutException("GATT discovery timed out.");
  }
  return task.Result;
 }
 public static string RadioState() {
  var task=Operation<BluetoothAdapter>(BluetoothAdapter.GetDefaultAsync());
  if(!task.Wait(10000))return "Unknown";
  var adapter=task.Result;if(adapter==null)return "Missing";
  var radioTask=Operation<Windows.Devices.Radios.Radio>(adapter.GetRadioAsync());
  if(!radioTask.Wait(10000)||radioTask.Result==null)return "Unknown";
  return radioTask.Result.State.ToString();
 }
 // Consent-capable WinRT creation runs on an STA UI thread with a message loop.
 public static BluetoothLEDevice Open(string id,ulong address,int addressType) {return OpenBounded(id,address,addressType,20000);}
 public static BluetoothLEDevice OpenBounded(string id,ulong address,int addressType,int timeoutMs) {
  var completion=new TaskCompletionSource<BluetoothLEDevice>();
  var thread=new Thread(()=>{
   using(var form=new Form{ShowInTaskbar=false,Opacity=0,StartPosition=FormStartPosition.Manual,Location=new System.Drawing.Point(-32000,-32000),Size=new System.Drawing.Size(1,1)}){
    form.Shown+=async(s,e)=>{
     try{
      var operation=!string.IsNullOrEmpty(id)?BluetoothLEDevice.FromIdAsync(id):
       addressType==2?BluetoothLEDevice.FromBluetoothAddressAsync(address):
       BluetoothLEDevice.FromBluetoothAddressAsync(address,(BluetoothAddressType)addressType);
      var task=Operation<BluetoothLEDevice>(operation);
      if(await Task.WhenAny(task,Task.Delay(timeoutMs))!=task){
       task.ContinueWith(t=>{if(t.Status==TaskStatus.RanToCompletion&&t.Result!=null)t.Result.Dispose();});
       throw new TimeoutException("Windows Bluetooth device creation timed out.");
      }
      completion.TrySetResult(await task);
     }catch(Exception ex){completion.TrySetException(ex);}
     finally{form.Close();}
    };
    Application.Run(form);
   }
  });
  thread.IsBackground=true;thread.SetApartmentState(ApartmentState.STA);thread.Start();
  if(!completion.Task.Wait(timeoutMs+1500))throw new TimeoutException("Windows Bluetooth UI thread timed out.");
  return completion.Task.Result;
 }
}
