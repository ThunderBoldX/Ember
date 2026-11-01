using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
namespace Ember;
public static class EffectPack
{
 public const int MaxBytes=1048576,MaxEffects=64;
 private static InvalidDataException Error(string message)=>new("Файл ефектів: "+message);
 private static void Fields(JsonElement element,params string[] names){if(element.ValueKind!=JsonValueKind.Object)throw Error("очікується об’єкт.");var seen=new HashSet<string>();foreach(var field in element.EnumerateObject())if(!names.Contains(field.Name)||!seen.Add(field.Name))throw Error("невідоме або повторне поле: "+field.Name);if(names.Any(n=>!seen.Contains(n)))throw Error("відсутнє обов’язкове поле.");}
 private static string Text(JsonElement obj,string name)=>obj.GetProperty(name).ValueKind==JsonValueKind.String?obj.GetProperty(name).GetString()!:throw Error(name+" має бути текстом.");
 private static int Number(JsonElement obj,string name)=>obj.GetProperty(name).ValueKind==JsonValueKind.Number&&obj.GetProperty(name).TryGetInt32(out int n)?n:throw Error(name+" має бути цілим числом.");
 private static double Real(JsonElement obj,string name)=>obj.GetProperty(name).ValueKind==JsonValueKind.Number&&obj.GetProperty(name).TryGetDouble(out double n)&&double.IsFinite(n)?n:throw Error(name+" має бути скінченним числом.");
 private static bool Boolean(JsonElement obj,string name)=>obj.GetProperty(name).ValueKind switch{JsonValueKind.True=>true,JsonValueKind.False=>false,_=>throw Error(name+" має бути true або false.")};
 public static List<CustomEffect> Read(string path){using var file=File.OpenRead(path);if(file.Length>MaxBytes)throw Error("максимальний розмір — 1 MiB.");using var bytes=new MemoryStream();file.CopyTo(bytes);byte[] data=bytes.ToArray();int offset=data.Length>=3&&data[0]==239&&data[1]==187&&data[2]==191?3:0;return Parse(new UTF8Encoding(false,true).GetString(data,offset,data.Length-offset));}
 public static StepTrigger Trigger(string name)=>name switch{"timer"=>StepTrigger.Timer,"musicBeat"=>StepTrigger.MusicBeat,"musicBass"=>StepTrigger.MusicBass,"musicPeak"=>StepTrigger.MusicPeak,"musicSilence"=>StepTrigger.MusicSilence,"musicLevel"=>StepTrigger.MusicLevel,"manual"=>StepTrigger.Manual,"musicBeatOrTimer"=>StepTrigger.MusicBeatOrTimer,_=>throw Error("невідома умова: "+name)};
 public static string TriggerName(StepTrigger trigger)=>trigger switch{StepTrigger.Timer=>"timer",StepTrigger.MusicBeat=>"musicBeat",StepTrigger.MusicBass=>"musicBass",StepTrigger.MusicPeak=>"musicPeak",StepTrigger.MusicSilence=>"musicSilence",StepTrigger.MusicLevel=>"musicLevel",StepTrigger.Manual=>"manual",StepTrigger.MusicBeatOrTimer=>"musicBeatOrTimer",_=>throw Error("старий ПК-мікрофон більше не підтримується.")};
 public static List<CustomEffect> Parse(string json)
 {
  if(Encoding.UTF8.GetByteCount(json)>MaxBytes)throw Error("максимальний розмір — 1 MiB.");using var doc=JsonDocument.Parse(json,new JsonDocumentOptions{MaxDepth=16});var root=doc.RootElement;Fields(root,"format","version","effects");int version=Number(root,"version");if(Text(root,"format")!="ember-effects"||version is not (1 or 2))throw Error("потрібен формат ember-effects, версія 1 або 2.");var array=root.GetProperty("effects");if(array.ValueKind!=JsonValueKind.Array||array.GetArrayLength() is <1 or >MaxEffects)throw Error("пакет має містити 1–64 ефекти.");var result=new List<CustomEffect>();
  foreach(var item in array.EnumerateArray()){
   Fields(item,"name","repeat","steps");var effect=new CustomEffect{Name=Text(item,"name"),Repeat=Boolean(item,"repeat"),IsImported=true};var steps=item.GetProperty("steps");if(steps.ValueKind!=JsonValueKind.Array||steps.GetArrayLength() is <1 or >128)throw Error("ефект має містити 1–128 кроків.");
   foreach(var step in steps.EnumerateArray()){
    Fields(step,version==1?new[]{"color","durationMs","transition","trigger"}:new[]{"color","durationMs","transition","trigger","condition"});var trigger=Trigger(Text(step,"trigger"));if(version==1&&trigger is not (StepTrigger.Timer or StepTrigger.MusicBeat))throw Error("нова умова потребує версії 2.");var value=new EffectStep{Color=Rgb.Parse(Text(step,"color")).Hex,DurationMs=Number(step,"durationMs"),Transition=Text(step,"transition") switch{"cut"=>StepTransition.Cut,"fade"=>StepTransition.Fade,_=>throw Error("transition: cut або fade.")},Trigger=trigger};
    if(version==2){var condition=step.GetProperty("condition");Fields(condition,"thresholdDb","beatCount","holdMs","timeoutMs");value.ThresholdDb=Real(condition,"thresholdDb");value.BeatCount=Number(condition,"beatCount");value.HoldMs=Number(condition,"holdMs");value.TimeoutMs=Number(condition,"timeoutMs");}effect.Steps.Add(value);
   }effect.ValidatePlayable();result.Add(effect);
  }return result;
 }
 private static object Definition(CustomEffect effect){effect.ValidatePlayable();return new{name=effect.Name,repeat=effect.Repeat,steps=effect.Steps.Select(s=>new{color=Rgb.Parse(s.Color).Hex,durationMs=s.DurationMs,transition=s.Transition==StepTransition.Fade?"fade":"cut",trigger=TriggerName(s.Trigger),condition=new{thresholdDb=s.ThresholdDb,beatCount=s.BeatCount,holdMs=s.HoldMs,timeoutMs=s.TimeoutMs}}).ToArray()};}
 public static string Serialize(IEnumerable<CustomEffect> effects){var list=effects.ToList();if(list.Count is <1 or >MaxEffects)throw Error("для експорту вибери 1–64 ефекти.");string json=JsonSerializer.Serialize(new{format="ember-effects",version=2,effects=list.Select(Definition).ToArray()},new JsonSerializerOptions{WriteIndented=true,Encoder=System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping});if(Encoding.UTF8.GetByteCount(json)>MaxBytes)throw Error("пакет перевищує 1 MiB.");return json;}
 public static void Write(string path,IEnumerable<CustomEffect> effects){string json=Serialize(effects);string temporary=path+".tmp-"+Guid.NewGuid().ToString("N");try{File.WriteAllText(temporary,json,new UTF8Encoding(false));File.Move(temporary,path,true);}finally{if(File.Exists(temporary))File.Delete(temporary);}}
 public static string Fingerprint(CustomEffect effect)=>Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(Definition(effect)))));
 public static List<CustomEffect> Merge(IEnumerable<CustomEffect> existing,IEnumerable<CustomEffect> incoming,out int added){var next=existing.Select(e=>e.Clone()).ToList();var hashes=new HashSet<string>(next.Where(e=>e.IsPlayable).Select(Fingerprint));added=0;foreach(var effect in incoming){effect.ValidatePlayable();if(!hashes.Add(Fingerprint(effect)))continue;var copy=effect.Clone();copy.Id=Guid.NewGuid().ToString("N");copy.IsImported=true;next.Add(copy);added++;}return next;}
}
