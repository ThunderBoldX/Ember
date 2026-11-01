using System;
using System.Collections.Generic;

// Shared by the .NET app tests and the native WinRT bridge (C# 5 compatible).
public sealed class EmberConnectionTarget {
 public string DeviceId="";
 public int AddressType=2;
 public string Label="";
}
public static class EmberConnectionPolicy {
 public static EmberConnectionTarget[] Plan(ulong address,string id,int type) {
  var targets=new List<EmberConnectionTarget>();
  if(address!=0){
   int preferred=type==0||type==1?type:2;
   targets.Add(new EmberConnectionTarget{AddressType=preferred,Label="MAC"});
   if(preferred!=2)targets.Add(new EmberConnectionTarget{AddressType=preferred,Label="MAC повторно"});
   if(!string.IsNullOrEmpty(id))targets.Add(new EmberConnectionTarget{DeviceId=id,Label="Windows ID"});
   if(preferred!=2)targets.Add(new EmberConnectionTarget{AddressType=2,Label="MAC авто"});
   else{targets.Add(new EmberConnectionTarget{AddressType=0,Label="MAC public"});targets.Add(new EmberConnectionTarget{AddressType=1,Label="MAC random"});}
  }else if(!string.IsNullOrEmpty(id)){
   targets.Add(new EmberConnectionTarget{DeviceId=id,Label="Windows ID"});
   targets.Add(new EmberConnectionTarget{DeviceId=id,Label="Windows ID повторно"});
  }
  return targets.ToArray();
 }
 public static bool Retry(string status){return status=="Unreachable"||status=="ProtocolError"||status=="Disconnected"||status=="NotFound";}
 public static bool Confirmed(string gattStatus,string linkStatus,bool writable){return gattStatus=="Success"&&linkStatus=="Connected"&&writable;}
}
