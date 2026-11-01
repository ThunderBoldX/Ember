using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
namespace Ember;
public partial class MainWindow
{
 private void RefreshSavedEffects(string? id=null){SavedEffects.ItemsSource=null;var visible=customEffects.Where(e=>e.IsImported==(effectTab==4)).ToList();SavedEffects.ItemsSource=visible;SavedEffects.SelectedItem=visible.FirstOrDefault(e=>e.Id==id)??visible.FirstOrDefault();RenderSequence();UpdateControls();}
 private void SavedEffectChanged(object sender,SelectionChangedEventArgs e){if(ready){RenderSequence();UpdateControls();}}
 private void RenderSequence(){SequenceColors.Children.Clear();if(SavedEffects.SelectedItem is not CustomEffect effect){SequenceState.Text="Створи ефект із власних кольорів і таймінгів.";return;}SequenceState.Text=$"{effect.Name} · {effect.Steps.Count} кроків · "+(effect.Repeat?"по колу":"один раз");for(int i=0;i<effect.Steps.Count;i++){var s=effect.Steps[i];var color=Rgb.Parse(s.Color);var chip=new Border{Width=36,Height=32,CornerRadius=new(8),Background=new SolidColorBrush(Color.FromRgb(color.R,color.G,color.B)),BorderBrush=new SolidColorBrush(Color.FromArgb(45,255,255,255)),BorderThickness=new(1),Margin=new(0,0,7,7),ToolTip=$"Крок {i+1} · {s.Color} · {s.DurationMs/1000.0:0.###} с · "+(s.Trigger switch{StepTrigger.MusicBeat=>"біт музики",StepTrigger.MicrophoneSound=>"стара умова — зміни її в редакторі",_=>"таймер"})};SequenceColors.Children.Add(chip);}}
 private void NewEffectClick(object sender,RoutedEventArgs e){CustomTab.IsChecked=true;OpenEditor(null);}
 private void EditCustomClick(object sender,RoutedEventArgs e){if(SavedEffects.SelectedItem is CustomEffect effect)OpenEditor(effect);}
 private void OpenEditor(CustomEffect? effect){var editor=new EffectEditor(effect??new CustomEffect{IsImported=effectTab==4,Steps={new EffectStep()}}){Owner=this,Icon=Icon};if(editor.ShowDialog()!=true||editor.Result==null)return;try{var next=customEffects.Select(e=>e.Clone()).ToList();var at=next.FindIndex(e=>e.Id==editor.Result.Id);if(at>=0)next[at]=editor.Result;else next.Add(editor.Result);EffectStore.Save(EffectsPath,next);customEffects=next;RefreshSavedEffects(editor.Result.Id);Log("Ефект збережено: "+editor.Result.Name);}catch(Exception ex){Log("Не вдалося зберегти ефект: "+ex.Message);}}
 private async void PlayCustomClick(object sender,RoutedEventArgs e)=>await Run(async()=>{if(SavedEffects.SelectedItem is not CustomEffect effect)return;effect.ValidatePlayable();await WaitMusicWrite();StopStreaming();await LeaveStripMic();if(effect.Steps.Any(s=>s.NeedsAudio))await StartAudio();await ble.Write(Protocol.Brightness((int)BrightnessSlider.Value,settings.Profile));player.Start(effect,clock.Elapsed.TotalSeconds);sequenceActive=true;EffectState.Text="МІЙ ЕФЕКТ / "+effect.Name.ToUpperInvariant();MarkSelected();Log("Відтворення: "+effect.Name);});
 private void NextCustomClick(object sender,RoutedEventArgs e){player.RequestNext();}
 private void PauseCustomClick(object sender,RoutedEventArgs e){if(!player.Running)return;double now=clock.Elapsed.TotalSeconds;if(player.Paused)player.Resume(now);else player.Pause(now);SequenceState.Text=$"{player.Effect!.Name} · {player.State(now)}";UpdateControls();}
 private async void StopCustomClick(object sender,RoutedEventArgs e)=>await Run(async()=>{StopStreaming();await WaitMusicWrite();if(ble.Connected)await Restore();RenderSequence();EffectState.Text="ЗУПИНЕНО";Log("Власний ефект зупинено. Відновлено автономний стан.");});
 private async void DeleteCustomClick(object sender,RoutedEventArgs e)=>await Run(async()=>{if(SavedEffects.SelectedItem is not CustomEffect effect)return;var next=customEffects.Where(e=>e.Id!=effect.Id).ToList();EffectStore.Save(EffectsPath,next);customEffects=next;if(sequenceActive&&player.Effect?.Id==effect.Id){StopStreaming();await WaitMusicWrite();if(ble.Connected)await Restore();}RefreshSavedEffects();Log("Ефект видалено: "+effect.Name);});
 private static void RenderWindow(Window window,string path){window.UpdateLayout();var target=new RenderTargetBitmap((int)window.ActualWidth,(int)window.ActualHeight,96,96,PixelFormats.Pbgra32);target.Render(window);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(target));using var file=File.Create(path);encoder.Save(file);}
 private async Task PreviewInterface()
 {
  timer.Stop();await Task.Delay(300);string folder=AppContext.BaseDirectory;var errors=new List<string>();
  try{
   var handle=new System.Windows.Interop.WindowInteropHelper(this).Handle;var area=System.Windows.Forms.Screen.FromHandle(handle).WorkingArea;var scale=PresentationSource.FromVisual(this)!.CompositionTarget!.TransformToDevice;var actual=scale.Transform(new Vector(ActualWidth,ActualHeight));if(actual.X>area.Width+1||actual.Y>area.Height+1)errors.Add("Initial window exceeds monitor work area.");
   RenderWindow(this,Path.Combine(folder,"preview.png"));
   MainScroll.ScrollToVerticalOffset(EffectsCard.TranslatePoint(new Point(),MainScroll).Y+MainScroll.VerticalOffset-12);await Task.Delay(80);RenderWindow(this,Path.Combine(folder,"preview-autonomous.png"));if(EffectGrid.Children.Count!=16)errors.Add("Expected sixteen autonomous effects.");
   MusicTab.IsChecked=true;UpdateLayout();MainScroll.ScrollToVerticalOffset(EffectsCard.TranslatePoint(new Point(),MainScroll).Y+MainScroll.VerticalOffset-12);await Task.Delay(100);RenderWindow(this,Path.Combine(folder,"preview-music.png"));CheckTabs(MusicTab,errors);
   MicTab.IsChecked=true;UpdateLayout();RenderWindow(this,Path.Combine(folder,"preview-microphone.png"));CheckTabs(MicTab,errors);
   customEffects.Add(new CustomEffect{Name="Перевірка палітри",Steps={new(){Color="#FF5A1F",DurationMs=1000},new(){Color="#0066FF",DurationMs=1500,Transition=StepTransition.Fade,Trigger=StepTrigger.MusicBeat},new(){Color="#B400FF",DurationMs=500,Trigger=StepTrigger.Timer}}});RefreshSavedEffects();CustomTab.IsChecked=true;UpdateLayout();RenderWindow(this,Path.Combine(folder,"preview-custom.png"));CheckTabs(CustomTab,errors);
   ImportedTab.IsChecked=true;customEffects.Add(new CustomEffect{Name="Імпортований імпульс",IsImported=true,Steps={new EffectStep()}});RefreshSavedEffects();UpdateLayout();RenderWindow(this,Path.Combine(folder,"preview-imported.png"));CheckTabs(ImportedTab,errors);
   if(!EditCustom.IsEnabled||PlayCustom.IsEnabled)errors.Add("Editing must work offline; playing must require a connection.");
   var editor=new EffectEditor(customEffects[0]){Owner=this,Icon=Icon};editor.Show();await Task.Delay(150);RenderWindow(editor,Path.Combine(folder,"preview-editor.png"));editor.Close();
   Width=1060;Height=760;MusicTab.IsChecked=true;UpdateLayout();MainScroll.ScrollToVerticalOffset(EffectsCard.TranslatePoint(new Point(),MainScroll).Y+MainScroll.VerticalOffset-12);await Task.Delay(80);RenderWindow(this,Path.Combine(folder,"preview-compact.png"));CheckTabs(MusicTab,errors);
  }catch(Exception ex){errors.Add(ex.ToString());}
  File.WriteAllText(Path.Combine(folder,"ui-validation.txt"),errors.Count==0?"PASS: initial window fits monitor work area; sixteen autonomous modes; tab underline alignment and selected visibility; offline editing and connected playback; all views rendered at normal and minimum width.":string.Join("\n",errors));allowClose=true;Close();
 }
 private void CheckTabs(RadioButton selected,List<string> errors){foreach(var tab in new[]{HardwareTab,MusicTab,MicTab,CustomTab,ImportedTab}){tab.ApplyTemplate();var underline=tab.Template.FindName("Underline",tab) as Border;var presenter=FindPresenter(tab);if(underline==null||presenter==null){errors.Add("Missing tab template parts.");continue;}if((underline.Visibility==Visibility.Visible)!=(tab==selected))errors.Add("Incorrect selected tab underline.");if(tab==selected){Point bottom=presenter.TranslatePoint(new Point(0,presenter.ActualHeight),tab),line=underline.TranslatePoint(new Point(),tab);if(line.Y<bottom.Y+8)errors.Add("Tab underline overlaps text.");}}}
 private static ContentPresenter? FindPresenter(DependencyObject root){for(int i=0;i<VisualTreeHelper.GetChildrenCount(root);i++){var child=VisualTreeHelper.GetChild(root,i);if(child is ContentPresenter presenter)return presenter;var nested=FindPresenter(child);if(nested!=null)return nested;}return null;}
}
