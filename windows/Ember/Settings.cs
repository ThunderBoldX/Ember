using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
namespace Ember;
public sealed class Settings
{
 public List<SavedLamp> Devices{get;set;}=new();
 public void Remember(){Devices??=new();Devices.RemoveAll(d=>d.Address==Address&&d.DeviceId==DeviceId);Devices.Insert(0,new(){Address=Address,DeviceId=DeviceId,AddressType=AddressType,Name=Name,Profile=Profile});if(Devices.Count>30)Devices.RemoveRange(30,Devices.Count-30);}
 public string DeviceId{get;set;}="";public int AddressType{get;set;}=2;
 public bool LastWasStripMic{get;set;}=false;public int StripMicMode{get;set;}=0;public int StripMicSensitivity{get;set;}=35;
 public int Profile{get;set;}=2; public int Brightness{get;set;}=70;public int Speed{get;set;}=50;public double Sensitivity{get;set;}=55;public string Color{get;set;}="#FF5A1F";public bool Fallback{get;set;}=true;public bool Tray{get;set;}=true;public string Address{get;set;}="";public string Name{get;set;}="";public int LastMode{get;set;}=138;public bool LastWasColor{get;set;}=false;public Dictionary<string,int[]> Mappings{get;set;}=new(){{"0",new[]{135,136,137,138}},{"1",new[]{77,78,79,80}},{"2",new[]{135,136,137,138}}};
 public static string Folder=>Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Ember");
 public static Settings Load(){try{var s=JsonSerializer.Deserialize<Settings>(File.ReadAllText(Path.Combine(Folder,"settings.json")))??new();s.Devices??=new();if(s.Devices.Count==0&&(s.Address.Length>0||s.DeviceId.Length>0))s.Remember();return s;}catch{return new();}}
 public void Save(){Directory.CreateDirectory(Folder);var path=Path.Combine(Folder,"settings.json");File.WriteAllText(path+".tmp",JsonSerializer.Serialize(this,new JsonSerializerOptions{WriteIndented=true}));File.Move(path+".tmp",path,true);}
}

public sealed class SavedLamp {public string Address{get;set;}="";public string DeviceId{get;set;}="";public int AddressType{get;set;}=2;public string Name{get;set;}="RGB стрічка";public int Profile{get;set;}=2;}
