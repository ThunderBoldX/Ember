using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
namespace Ember;

public readonly record struct Rgb(byte R,byte G,byte B)
{
 public string Hex=>$"#{R:X2}{G:X2}{B:X2}";
 public static Rgb Parse(string text){if(text.Length!=7||text[0]!='#'||!uint.TryParse(text.AsSpan(1),NumberStyles.HexNumber,CultureInfo.InvariantCulture,out uint v))throw new ArgumentException("Колір має бути у форматі #RRGGBB.");return new((byte)(v>>16),(byte)(v>>8),(byte)v);}
 public static Rgb Hsv(double hue,double saturation=1,double value=1){hue=(hue%1+1)%1*6;saturation=Math.Clamp(saturation,0,1);value=Math.Clamp(value,0,1);double chroma=value*saturation,x=chroma*(1-Math.Abs(hue%2-1)),m=value-chroma;(double r,double g,double b)=((int)hue) switch{0=>(chroma,x,0.0),1=>(x,chroma,0.0),2=>(0.0,chroma,x),3=>(0.0,x,chroma),4=>(x,0.0,chroma),_=>(chroma,0.0,x)};return new(Byte(r+m),Byte(g+m),Byte(b+m));}
 public (double Hue,double Saturation,double Value) ToHsv(){double r=R/255.0,g=G/255.0,b=B/255.0,max=Math.Max(r,Math.Max(g,b)),min=Math.Min(r,Math.Min(g,b)),delta=max-min;double h=delta==0?0:max==r?((g-b)/delta)%6:max==g?(b-r)/delta+2:(r-g)/delta+4;return ((h/6+1)%1,max==0?0:delta/max,max);}
 private static byte Byte(double v)=>(byte)Math.Clamp((int)Math.Round(v*255),0,255);
 public Rgb Scale(double amount)=>new(Byte(R/255.0*amount),Byte(G/255.0*amount),Byte(B/255.0*amount));
 public static Rgb Lerp(Rgb from,Rgb to,double progress){progress=Math.Clamp(progress,0,1);return new((byte)Math.Round(from.R+(to.R-from.R)*progress),(byte)Math.Round(from.G+(to.G-from.G)*progress),(byte)Math.Round(from.B+(to.B-from.B)*progress));}
}
public enum StepTrigger { Timer=0,MusicBeat=1,MicrophoneSound=2,MusicBass=3,MusicPeak=4,MusicSilence=5,MusicLevel=6,Manual=7,MusicBeatOrTimer=8 }
public enum StepTransition { Cut,Fade }
public sealed class EffectStep
{
 public string Color{get;set;}="#FF5A1F";
 public int DurationMs{get;set;}=1000;
 public StepTrigger Trigger{get;set;}=StepTrigger.Timer;
 public StepTransition Transition{get;set;}=StepTransition.Cut;
 public double ThresholdDb{get;set;}=-24;
 public int BeatCount{get;set;}=1;
 public int HoldMs{get;set;}=0;
 public int TimeoutMs{get;set;}=0;
 public bool NeedsAudio=>Trigger is not (StepTrigger.Timer or StepTrigger.Manual or StepTrigger.MicrophoneSound);
 public EffectStep Clone()=>new(){Color=Color,DurationMs=DurationMs,Trigger=Trigger,Transition=Transition,ThresholdDb=ThresholdDb,BeatCount=BeatCount,HoldMs=HoldMs,TimeoutMs=TimeoutMs};
}
public sealed class CustomEffect
{
 public string Id{get;set;}=Guid.NewGuid().ToString("N");
 public string Name{get;set;}="Мій ефект";
 public override string ToString()=>Name;
 public bool IsImported{get;set;}
 public bool Repeat{get;set;}=true;
 public List<EffectStep> Steps{get;set;}=new();
 public CustomEffect Clone()=>new(){Id=Id,Name=Name,IsImported=IsImported,Repeat=Repeat,Steps=Steps.Select(s=>s.Clone()).ToList()};
 public bool IsPlayable=>Steps.All(s=>s.Trigger!=StepTrigger.MicrophoneSound&&Enum.IsDefined(s.Trigger));
 public void ValidatePlayable(){Validate();if(!IsPlayable)throw new ArgumentException("Старий ефект використовує мікрофон ПК. У редакторі заміни цю умову на таймер або біт музики.");}
 public void Validate(){if(string.IsNullOrWhiteSpace(Name)||Name.Length>60)throw new ArgumentException("Назва: від 1 до 60 символів.");if(Steps.Count is <1 or >128)throw new ArgumentException("Ефект має містити від 1 до 128 кроків.");foreach(var s in Steps){_=Rgb.Parse(s.Color);if(s.DurationMs is <125 or >60000)throw new ArgumentException("Тривалість кроку: від 0,125 до 60 секунд.");if(!double.IsFinite(s.ThresholdDb)||s.ThresholdDb is <-60 or >0||s.BeatCount is <1 or >16||s.HoldMs is <0 or >30000||s.TimeoutMs is <0 or >120000||s.TimeoutMs is >0 and <125)throw new ArgumentException("Параметри умови: поріг −60…0 dBFS; 1–16 бітів; утримання 0–30000 мс; тайм-аут 0 або 125–120000 мс.");if(s.Trigger==StepTrigger.MusicBeatOrTimer&&s.TimeoutMs==0)throw new ArgumentException("Для умови «Біт або таймер» задай тайм-аут.");if(!Enum.IsDefined(s.Trigger)||!Enum.IsDefined(s.Transition))throw new ArgumentException("Невідома умова переходу.");}}
}
public static class EffectStore
{
 public static List<CustomEffect> Load(string path){if(!File.Exists(path))return new();var effects=JsonSerializer.Deserialize<List<CustomEffect>>(File.ReadAllText(path))??new();foreach(var effect in effects)effect.Validate();if(effects.Select(e=>e.Id).Distinct().Count()!=effects.Count)throw new InvalidDataException("Повторюються ідентифікатори ефектів.");return effects;}
 public static void Save(string path,IEnumerable<CustomEffect> effects){var list=effects.ToList();foreach(var effect in list)effect.Validate();Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);File.WriteAllText(path+".tmp",JsonSerializer.Serialize(list,new JsonSerializerOptions{WriteIndented=true}));File.Move(path+".tmp",path,true);}
}
// Monotonic time is supplied by the caller. Each event can advance only one step.
public sealed class EffectPlayer
{
 public CustomEffect? Effect{get;private set;}
 public int Index{get;private set;}
 public bool Running{get;private set;}
 public bool Paused{get;private set;}
 private double started,pausedAt,heldSince=double.NaN,lastSample=double.NaN;
 private int beats;private bool manual;private Rgb from;
 public void Start(CustomEffect effect,double now){effect.ValidatePlayable();Effect=effect.Clone();Index=0;started=now;from=Rgb.Parse(Effect.Steps[0].Color);Running=true;Paused=false;ResetGate();}
 private void ResetGate(){heldSince=lastSample=double.NaN;beats=0;manual=false;}
 public void Stop(){Running=false;Paused=false;ResetGate();}
 public void Pause(double now){if(Running&&!Paused){pausedAt=now;Paused=true;}}
 public void Resume(double now){if(Paused){double delta=now-pausedAt;started+=delta;if(!double.IsNaN(heldSince))heldSince+=delta;lastSample=now;Paused=false;}}
 public void RequestNext(){if(Running&&!Paused&&Effect!.Steps[Index].Trigger==StepTrigger.Manual)manual=true;}
 public Rgb Sample(double now,bool musicBeat=false,AudioFrame? audio=null)
 {
  if(Effect==null)throw new InvalidOperationException("Ефект не запущено.");if(Paused)now=pausedAt;
  int limit=512;while(Running&&!Paused&&--limit>0){var s=Effect.Steps[Index];double duration=s.DurationMs/1000.0;if(now-started<duration)break;
   bool passed=s.Trigger==StepTrigger.Timer;
   if(s.Trigger is StepTrigger.MusicBeat or StepTrigger.MusicBeatOrTimer){if(musicBeat)beats++;passed=beats>=s.BeatCount;}
   else if(s.Trigger==StepTrigger.Manual)passed=manual;
   else if(s.Trigger is StepTrigger.MusicBass or StepTrigger.MusicPeak or StepTrigger.MusicSilence or StepTrigger.MusicLevel){bool signal=audio!=null&&(s.Trigger switch{StepTrigger.MusicBass=>Db(audio.Bass)>=s.ThresholdDb,StepTrigger.MusicPeak=>Db(audio.Peak)>=s.ThresholdDb,StepTrigger.MusicSilence=>Db(audio.Rms)<=s.ThresholdDb,_=>Db(audio.Rms)>=s.ThresholdDb});if(!signal){heldSince=double.NaN;}else{if(double.IsNaN(heldSince)||!double.IsNaN(lastSample)&&now-lastSample>.4)heldSince=now;passed=(now-heldSince)*1000+1e-6>=s.HoldMs;}lastSample=now;}
   if(s.Trigger!=StepTrigger.Timer&&s.TimeoutMs>0&&now-started>=duration+s.TimeoutMs/1000.0)passed=true;
   if(!passed)break;from=Rgb.Parse(s.Color);if(Index==Effect.Steps.Count-1&&!Effect.Repeat){Running=false;break;}Index=(Index+1)%Effect.Steps.Count;started=s.Trigger==StepTrigger.Timer?started+duration:now;ResetGate();if(s.Trigger!=StepTrigger.Timer)break;
  }
  var current=Effect.Steps[Index];var to=Rgb.Parse(current.Color);return current.Transition==StepTransition.Fade?Rgb.Lerp(from,to,(now-started)/(current.DurationMs/1000.0)):to;
 }
 private static double Db(double value)=>value<=0?-120:20*Math.Log10(value);
 public string State(double now){if(Effect==null)return "";if(Paused)return "пауза";if(!Running)return "завершено";var s=Effect.Steps[Index];if(now-started<s.DurationMs/1000.0)return "відтворюється";return s.Trigger switch{StepTrigger.MusicBeat or StepTrigger.MusicBeatOrTimer=>$"чекає біт · {beats}/{s.BeatCount}",StepTrigger.Manual=>"чекає сигнал «Далі»",StepTrigger.MusicBass=>"чекає бас",StepTrigger.MusicPeak=>"чекає пік",StepTrigger.MusicSilence=>"чекає тишу",StepTrigger.MusicLevel=>"чекає рівень музики",_=>"відтворюється"};}
}

public sealed class LiveEffectRecorder
{
 private List<EffectStep>? steps;private double last;
 public bool Active{get;private set;}
 public void Start(List<EffectStep> destination,Rgb color,double now,StepTrigger trigger,StepTransition transition){if(destination.Count>=128)throw new ArgumentException("Максимум 128 кроків.");steps=destination;steps.Add(new(){Color=color.Hex,Trigger=trigger,Transition=transition});last=now;Active=true;}
 public void Record(Rgb color,double now,StepTrigger trigger,StepTransition transition){if(!Active||steps==null)return;if(steps.Count>=128)throw new ArgumentException("Максимум 128 кроків.");steps[^1].DurationMs=Duration(now-last);steps.Add(new(){Color=color.Hex,Trigger=trigger,Transition=transition});last=now;}
 public void Stop(double now){if(!Active||steps==null)return;steps[^1].DurationMs=Duration(now-last);Active=false;}
 private static int Duration(double seconds)=>(int)Math.Clamp(Math.Round(seconds*1000),125,60000);
}
public record EffectPreset(string Id,string Name,string Hint);
public sealed class ReactiveEffects
{
 public static readonly EffectPreset[] Music={new("pulse","Пульс","Спалах на кожен біт"),new("bass","Бас","Яскравість від басу"),new("jump3","JUMP3 / біт","Три кольори на біт"),new("jump7","JUMP7 / біт","Сім кольорів на біт"),new("fade","FADE / біт","Плавне згасання"),new("spectrum","Спектр","Колір від частоти"),new("wave","Хвиля","Райдужна хвиля"),new("strobe","Строб","Короткий спалах"),new("fire","Жар","Теплі спалахи басу"),new("aurora","Сяйво","М'які переливи"),new("breath","Дихання","Плавна пульсація"),new("echo","Ехо","Подвійний відгук"),new("spark","Іскри","Короткі піки музики"),new("ocean","Океан","Холодні хвилі басу"),new("duo","Дует","Два кольори на біт"),new("neon","Неон","Яскраві акценти ритму")};
 private double lastHit=-100;private int hits;private Rgb previous=new(255,90,31),target=new(255,90,31);
 public void Reset(){lastHit=-100;hits=0;previous=target=new(255,90,31);}
 public Rgb Render(string id,Rgb basis,double now,AudioFrame? audio,bool hit)
 {
  if(hit){lastHit=now;hits++;previous=target;target=Rgb.Hsv((hits%7)/7.0);}double age=now-lastHit,pulse=Math.Exp(-age*5),strength=Math.Clamp((audio?.Rms??0)*5,0,1),bass=Math.Clamp((audio?.Bass??0)*7,0,1);Rgb color=basis;double light=0;
  switch(id){case "spark":color=Rgb.Lerp(basis,new(255,255,255),Math.Clamp((audio?.Peak??0)*2,0,1));light=Math.Clamp((audio?.Peak??0)*2+pulse*.4,0,1);break;case "ocean":color=Rgb.Lerp(new(0,24,128),new(0,220,255),bass);light=Math.Clamp(strength*.4+bass,0,1);break;case "duo":color=hits%2==0?basis:Rgb.Hsv(basis.ToHsv().Hue+.5);light=Math.Clamp(strength*.3+pulse,0,1);break;case "neon":color=Rgb.Hsv((hits%5)/5.0,.9);light=Math.Clamp(bass*.5+pulse,0,1);break;case "pulse":light=.01+pulse*.99;break;case "bass":light=bass;break;case "jump3":color=new[]{new Rgb(255,0,0),new Rgb(0,255,0),new Rgb(0,0,255)}[hits%3];light=audio==null?0:.8;break;case "jump7":color=target;light=audio==null?0:.8;break;case "fade":color=Rgb.Lerp(previous,target,age/.55);light=.01+Math.Exp(-age*2.5)*.99;break;case "spectrum":color=audio==null?basis:Rgb.Hsv(Array.IndexOf(audio.Spectrum,audio.Spectrum.Max())/32.0);light=strength;break;case "wave":color=Rgb.Hsv(now*.045);light=strength;break;case "strobe":light=age<.13?1:0;break;case "fire":color=Rgb.Lerp(new(194,30,0),new(255,150,20),Math.Clamp(bass+pulse*.5,0,1));light=Math.Clamp(strength*.5+pulse,0,1);break;case "aurora":color=Rgb.Hsv(now*.025,.65);light=strength*(.55+.45*Math.Sin(now*1.7));break;case "breath":light=strength*(.25+.75*(.5+.5*Math.Sin(now*2)));break;case "echo":light=Math.Max(pulse,age>.25?Math.Exp(-(age-.25)*9)*.55:0);break;}
  return color.Scale(Math.Clamp(light,0,1));
 }
}
