using System;
namespace Ember;

public static class DeviceProfiles
{
 public static int Detect(string name,string uuid)
 {
  name=(name??"").ToUpperInvariant();uuid=(uuid??"").ToLowerInvariant();
  if(uuid.StartsWith("0000ffd9"))return 3;
  if(!uuid.StartsWith("0000fff3")&&!uuid.StartsWith("0000ffe9")&&!uuid.StartsWith("0000ffe1"))throw new NotSupportedException("Ця стрічка поки не підтримується.");
  if(name.Contains("BLEDOB"))return 0;
  if(name.Contains("MELK"))return 1;
  if(name.Contains("BLEDOM")||name.Contains("BLEDDM"))return 2;
  throw new NotSupportedException("Не вдалося визначити протокол цієї стрічки. Потрібна її Bluetooth-назва або модель.");
 }
 public static string Name(int profile)=>profile switch{0=>"ELK-BLEDOB",1=>"MELK",2=>"ELK-BLEDOM / BLEDDM",3=>"Triones / HappyLighting",_=>"Невідома стрічка"};
 public static bool HasMicrophone(int profile)=>profile is 0 or 1 or 2;
}

// Translate only supported commands. A writable BLE characteristic alone is not a protocol.
public sealed class ProtocolAdapter
{
 public int Profile{get;set;}=2;
 private int brightness=100,speed=50,mode=-1,r=255,g=90,b=31;
 public byte[] Convert(byte[] packet)
 {
  if(Profile!=3)return packet;
  if(packet.Length!=9||packet[0]!=126||packet[8]!=239)throw new ArgumentException("Невідома команда стрічки.");
  switch(packet[2])
  {
   case 5:r=packet[4];g=packet[5];b=packet[6];mode=-1;return Color();
   case 1:brightness=packet[3];return mode<0?Color():Mode();
   case 2:speed=packet[3];return mode<0?Color():Mode();
   case 3:mode=MapMode(packet[3]);return Mode();
   case 4:return new byte[]{0xcc,(byte)(packet[3]>0?0x23:0x24),0x33};
   case 7:if(packet[3]==0)return Array.Empty<byte>();break;
  }
  throw new NotSupportedException("Ця стрічка не має підтримуваного звукового сенсора.");
 }
 private byte[] Color()=>new byte[]{0x56,(byte)(r*brightness/100),(byte)(g*brightness/100),(byte)(b*brightness/100),0,0xf0,0xaa};
 private byte[] Mode()=>new byte[]{0xbb,(byte)mode,(byte)(255-speed*254/100),0x44};
 public static int MapMode(int id)=>id switch{135=>0x31,136=>0x38,137=>0x34,138=>0x25,139=>0x26,140=>0x27,141=>0x28,142=>0x29,143=>0x2a,144=>0x2b,145=>0x2c,146=>0x2d,147=>0x2e,148=>0x2f,149=>0x30,_=>throw new NotSupportedException("Непідтримуваний режим Triones.")};
}
