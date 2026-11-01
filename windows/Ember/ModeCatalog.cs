using System;
namespace Ember;
public static class ModeCatalog
{
 public static readonly EffectPreset[] Autonomous={
  new("JUMP3","JUMP3","Стрибки трьох кольорів"),new("JUMP7","JUMP7","Стрибки семи кольорів"),new("FADE3","FADE3","Переходи трьох кольорів"),new("FADE7","FADE7","Переходи семи кольорів"),
  new("RED","Дихання / червоний","Плавна зміна яскравості"),new("GREEN","Дихання / зелений","Плавна зміна яскравості"),new("BLUE","Дихання / синій","Плавна зміна яскравості"),new("YELLOW","Дихання / жовтий","Плавна зміна яскравості"),new("CYAN","Дихання / бірюза","Плавна зміна яскравості"),new("PURPLE","Дихання / фіолет","Плавна зміна яскравості"),new("WHITE","Дихання / білий","Плавна зміна яскравості"),
  new("RG","Червоний ↔ зелений","Плавний перехід пари"),new("RB","Червоний ↔ синій","Плавний перехід пари"),new("GB","Зелений ↔ синій","Плавний перехід пари"),new("STROBE","STROBE7","Спалахи семи кольорів"),new("STATIC","STATIC","Обраний постійний колір")
 };
 public static readonly EffectPreset[] StripMicrophone={new("mic0","Класичний","Звичайна чутливість"),new("mic1","М’який","Звичайна чутливість"),new("mic2","Динамічний","Звичайна чутливість"),new("mic3","Диско","Звичайна чутливість"),new("mic4","Хвиля","Звичайна чутливість"),new("mic5","Пульсація","Звичайна чутливість"),new("mic6","Спектральний","Звичайна чутливість"),new("mic7","Вечірка","Звичайна чутливість"),new("mic8","Тихий ритм","Підвищена чутливість"),new("mic9","М’який акцент","Підвищена чутливість"),new("mic10","Сильний біт","Підвищена чутливість"),new("mic11","Диско плюс","Підвищена чутливість"),new("mic12","Чутлива хвиля","Підвищена чутливість"),new("mic13","Чутливий пульс","Підвищена чутливість"),new("mic14","Спектр плюс","Підвищена чутливість"),new("mic15","Вечірка плюс","Підвищена чутливість")};
 public static readonly int[] MicModes={0,1,2,3,4,5,6,7,0,1,2,3,4,5,6,7};
 public static readonly int[] MicSensitivity={35,30,45,50,40,35,45,50,65,60,75,80,70,65,75,80};
 // ELK ordering documented by CommandUtils + PatternData. MELK ordering can vary;
 // expose the exact ID in the app instead of claiming identical firmware behavior.
 public static EffectPreset[] ForProfile(int profile)=>profile==3?Array.ConvertAll(Autonomous,p=>p.Id=="JUMP3"?new EffectPreset(p.Id,"Червоний строб","Спалахи червоного"):p.Id=="FADE3"?new EffectPreset(p.Id,"Жовтий строб","Спалахи жовтого"):p):profile!=1?Autonomous:Array.ConvertAll(Autonomous,p=>p.Id is "JUMP3" or "JUMP7" or "FADE3" or "FADE7" or "STATIC"?p:new EffectPreset(p.Id,"Режим "+ModeId(p.Id,profile),"Відповідність MELK за ID"));
 public static int ModeId(string name,int profile)=>name switch{"RED"=>139,"GREEN"=>140,"BLUE"=>141,"YELLOW"=>142,"CYAN"=>143,"PURPLE"=>144,"WHITE"=>145,"RG"=>146,"RB"=>147,"GB"=>148,"STROBE"=>profile==1?181:149,_=>throw new ArgumentException("Невідомий апаратний ефект.")};
}
