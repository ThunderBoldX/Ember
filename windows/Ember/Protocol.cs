using System;
using System.Collections.Generic;
namespace Ember;
public static class Protocol
{
 public static byte[] Color(int r,int g,int b) => [0x7e,7,5,3,B(r,255),B(g,255),B(b,255),0x10,0xef];
 public static byte[] Brightness(int value) => [0x7e,7,1,B(value,100),255,255,255,0,0xef];
 public static byte[] Speed(int value) => [0x7e,4,2,B(value,100),255,255,255,0,0xef];
 public static byte[] Mode(int value) => [0x7e,7,3,B(value,212),255,255,255,0,0xef];
 public static byte[] Brightness(int value,int profile) => profile switch{0=>[0x7e,4,1,B(value,100),1,255,2,1,0xef],2=>[0x7e,4,1,B(value,100),255,255,255,0,0xef],_=>Brightness(value)};
 public static byte[] Speed(int value,int profile) => profile==0?[0x7e,7,2,B(value,100),255,255,255,0,0xef]:Speed(value);
 public static byte[] Mode(int value,int profile) => profile switch{0=>[0x7e,7,3,B(value,156),3,255,255,0,0xef],2=>[0x7e,5,3,B(value,156),3,255,255,0,0xef],_=>Mode(value)};
 public static byte[] Power(bool on) => on?[0x7e,7,4,255,0,1,2,1,0xef]:[0x7e,7,4,0,0,0,2,1,0xef];
 public static byte[] Power(bool on,int profile) => profile==2?(on?[0x7e,4,4,240,0,1,255,0,0xef]:[0x7e,4,4,0,0,0,255,0,0xef]):Power(on);
 // Native controller microphone commands: these do not capture PC audio.
 public static byte[] StripMicPower(bool on)=>[0x7e,4,7,(byte)(on?1:0),255,255,255,0,0xef];
 public static byte[] StripMicMode(int mode)=>[0x7e,5,3,(byte)(128+B(mode,7)),4,255,255,0,0xef];
 public static byte[] StripMicSensitivity(int value)=>[0x7e,4,6,B(value,100),255,255,255,0,0xef];
 private static byte B(int n,int max) { if(n<0||n>max) throw new ArgumentOutOfRangeException(nameof(n)); return (byte)n; }
}
public record LampMode(string Name,int Id,string Description);
