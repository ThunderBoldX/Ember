using System;
using System.IO;
using System.Linq;
using System.Windows;
using Microsoft.Win32;
namespace Ember;
public partial class MainWindow
{
 private void ImportEffectClick(object sender,RoutedEventArgs e)
 {
  if(busy||closing)return;var dialog=new OpenFileDialog{Title="Імпорт ефектів Ember",Filter="Ефекти Ember (*.ember.json;*.json)|*.ember.json;*.json",Multiselect=false};if(dialog.ShowDialog(this)!=true)return;
  try{var incoming=EffectPack.Read(dialog.FileName);var next=EffectPack.Merge(customEffects,incoming,out int added);if(added>0){EffectStore.Save(EffectsPath,next);customEffects=next;}ImportedTab.IsChecked=true;RefreshSavedEffects(added>0?next.Last().Id:null);Log(added>0?$"Імпортовано ефектів: {added}. Вони доступні у вкладці «Імпортовані».":"Ці ефекти вже є у твоїй колекції. Дублікати не додано.");}catch(Exception ex){Log("Імпорт не виконано: "+ex.Message);}
 }
 private void ExportEffectClick(object sender,RoutedEventArgs e){if(busy||closing)return;if(SavedEffects.SelectedItem is not CustomEffect effect){Log("Вибери власний або імпортований ефект для експорту.");return;}ExportPack(new[]{effect},"Ember-Effect.ember.json");}
 private void ExportCategoryClick(object sender,RoutedEventArgs e){if(busy||closing)return;if(effectTab<3){Log("Для експорту відкрий «Мої ефекти» або «Імпортовані».");return;}var effects=customEffects.Where(x=>x.IsImported==(effectTab==4)).ToArray();if(effects.Length==0){Log("У цій категорії поки немає ефектів.");return;}ExportPack(effects,"Ember-Pack.ember.json");}
 private void ExportPack(CustomEffect[] effects,string filename){try{_=EffectPack.Serialize(effects);var dialog=new SaveFileDialog{Title="Експорт ефектів Ember",Filter="Ефекти Ember (*.ember.json)|*.ember.json",FileName=filename,DefaultExt=".ember.json",AddExtension=true,OverwritePrompt=true};if(dialog.ShowDialog(this)!=true)return;EffectPack.Write(dialog.FileName,effects);Log($"Експортовано: {effects.Length} · {Path.GetFileName(dialog.FileName)}");}catch(Exception ex){Log("Експорт не виконано: "+ex.Message);}}
}
