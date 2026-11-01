using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
namespace Ember;

// Output loopback only: the app never opens a PC microphone endpoint.
public sealed class WasapiAudio : IDisposable
{
 private CancellationTokenSource? cancel; private Task? worker; public event Action<AudioFrame>? Frame; public event Action<string>? Failed; public int SampleRate{get;private set;} public string DeviceName{get;private set;}="Типовий вихід Windows"; public AudioAnalyzer Analyzer{get;private set;}=new();
 public static (int Flow,int Flags) CaptureConfiguration()=>(0,0x20000);
 private TaskCompletionSource<bool>? startup;
 public Task StartReady(){if(worker is {IsCompleted:false})return startup?.Task??Task.CompletedTask;startup=new(TaskCreationOptions.RunContinuationsAsynchronously);Start();return startup.Task;}
 public void Start() {if(worker is {IsCompleted:false})return;Analyzer=new();SampleRate=0;cancel=new();worker=Task.Run(()=>Capture(cancel.Token));}
 public async Task Stop() {cancel?.Cancel();if(worker!=null)await worker;cancel?.Dispose();cancel=null;worker=null;}
 private void Capture(CancellationToken token)
 {
  IMMDeviceEnumerator? enumerator=null; IMMDevice? device=null; IAudioClient? client=null; IAudioCaptureClient? capture=null; IntPtr format=IntPtr.Zero;
  try {
   var configuration=CaptureConfiguration();enumerator=(IMMDeviceEnumerator)new MMDeviceEnumerator();Check(enumerator.GetDefaultAudioEndpoint(configuration.Flow,1,out device));var iid=typeof(IAudioClient).GUID;Check(device.Activate(ref iid,23,IntPtr.Zero,out var instance));client=(IAudioClient)instance;
   Check(client.GetMixFormat(out format));int tag=(ushort)Marshal.ReadInt16(format,0),channels=(ushort)Marshal.ReadInt16(format,2),rate=Marshal.ReadInt32(format,4),block=(ushort)Marshal.ReadInt16(format,12),bits=(ushort)Marshal.ReadInt16(format,14);
   bool ieee=tag==3||(tag==65534&&Marshal.ReadInt32(format,24)==3);SampleRate=rate;
   if((ieee&&bits!=32)||(!ieee&&bits is not(16 or 24 or 32)))throw new NotSupportedException("Формат звуку Windows не підтримується. Спробуй PCM 16/24/32-bit або float 32-bit.");
   Check(client.Initialize(0,configuration.Flags,200000,0,format,IntPtr.Zero));iid=typeof(IAudioCaptureClient).GUID;Check(client.GetService(ref iid,out instance));capture=(IAudioCaptureClient)instance;Check(client.Start());startup?.TrySetResult(true);
   while(!token.IsCancellationRequested) {
    Check(capture.GetNextPacketSize(out var frames));while(frames>0&&!token.IsCancellationRequested) {
     Check(capture.GetBuffer(out var data,out var count,out var flags,out _,out _));
     try {
      var mono=new float[(int)count];if((flags&2)==0) {var bytes=new byte[(int)count*block];Marshal.Copy(data,bytes,0,bytes.Length);for(int f=0;f<count;f++){double sum=0;for(int ch=0;ch<channels;ch++){int at=f*block+ch*bits/8;double value=ieee?BitConverter.ToSingle(bytes,at):bits switch{16=>BitConverter.ToInt16(bytes,at)/32768.0,24=>((bytes[at]|bytes[at+1]<<8|bytes[at+2]<<16)<<8>>8)/8388608.0,32=>BitConverter.ToInt32(bytes,at)/2147483648.0,_=>0};sum+=value;}mono[f]=(float)(sum/channels);}}
      var frame=Analyzer.Push(mono,rate);if(frame!=null) Frame?.Invoke(frame);
     } finally{Check(capture.ReleaseBuffer(count));}Check(capture.GetNextPacketSize(out frames));
    }
    token.WaitHandle.WaitOne(2);
   }
  } catch(Exception e){string message=e.Message;startup?.TrySetException(new InvalidOperationException(message,e));Failed?.Invoke(message);}finally{try{client?.Stop();}catch{} if(format!=IntPtr.Zero)Marshal.FreeCoTaskMem(format);foreach(var obj in new object?[]{capture,client,device,enumerator})if(obj!=null&&Marshal.IsComObject(obj))Marshal.ReleaseComObject(obj);}
 }
 private static void Check(int hr){if(hr<0)Marshal.ThrowExceptionForHR(hr);}
 public void Dispose(){cancel?.Cancel();}
 [ComImport,Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]private class MMDeviceEnumerator{}
 [ComImport,Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]private interface IMMDeviceEnumerator {
  [PreserveSig]int EnumAudioEndpoints(int flow,int mask,out IntPtr devices);[PreserveSig]int GetDefaultAudioEndpoint(int flow,int role,out IMMDevice device);[PreserveSig]int GetDevice([MarshalAs(UnmanagedType.LPWStr)]string id,out IMMDevice device);[PreserveSig]int RegisterEndpointNotificationCallback(IntPtr client);[PreserveSig]int UnregisterEndpointNotificationCallback(IntPtr client);
 }
 [ComImport,Guid("D666063F-1587-4E43-81F1-B948E807363F"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]private interface IMMDevice {
  [PreserveSig]int Activate(ref Guid iid,int context,IntPtr activation,[MarshalAs(UnmanagedType.IUnknown)]out object instance);[PreserveSig]int OpenPropertyStore(int access,out IntPtr store);[PreserveSig]int GetId(out IntPtr id);[PreserveSig]int GetState(out int state);
 }
 [ComImport,Guid("1CB9AD4C-DBFA-4C32-B178-C2F568A703B2"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]private interface IAudioClient {
  [PreserveSig]int Initialize(int mode,int flags,long duration,long periodicity,IntPtr format,IntPtr session);[PreserveSig]int GetBufferSize(out uint size);[PreserveSig]int GetStreamLatency(out long latency);[PreserveSig]int GetCurrentPadding(out uint frames);[PreserveSig]int IsFormatSupported(int mode,IntPtr format,out IntPtr closest);[PreserveSig]int GetMixFormat(out IntPtr format);[PreserveSig]int GetDevicePeriod(out long normal,out long minimum);[PreserveSig]int Start();[PreserveSig]int Stop();[PreserveSig]int Reset();[PreserveSig]int SetEventHandle(IntPtr handle);[PreserveSig]int GetService(ref Guid iid,[MarshalAs(UnmanagedType.IUnknown)]out object service);
 }
 [ComImport,Guid("C8ADBD64-E71E-48A0-A4DE-185C395CD317"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]private interface IAudioCaptureClient {
  [PreserveSig]int GetBuffer(out IntPtr data,out uint frames,out int flags,out ulong devicePosition,out ulong qpcPosition);[PreserveSig]int ReleaseBuffer(uint frames);[PreserveSig]int GetNextPacketSize(out uint frames);
 }
}
