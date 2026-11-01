using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace Ember;
public partial class MainWindow
{
 private async Task VerifySingleClicks()
 {
  timer.Stop();var checks=new List<string>();
  void Check(bool ok,string text){if(!ok)throw new InvalidOperationException(text);checks.Add("PASS "+text);}
  Button Tile(string id)=>EffectGrid.Children.OfType<Button>().Single(b=>(string)b.Tag==id);
  void Click(string id){var tile=Tile(id);Check(tile.IsEnabled,"effect tile accepts one click: "+id);tile.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));}
  async Task Drained(){var watch=Stopwatch.StartNew();while(choosingEffect||busy){if(watch.Elapsed.TotalSeconds>12)throw new TimeoutException("Effect click did not complete.");await Task.Delay(10);}}
  try{
   await ble.Connect("1");settings.Profile=ble.Profile;BuildModes();BuildEffects();UpdateControls();
   // The controller starts in microphone mode while local saved state says otherwise.
   await ble.Write(Protocol.Color(255,90,31));Click("JUMP3");await Drained();
   var state=await ble.SelfTest();Check(state.GetProperty("mode").GetInt32()==135&&!state.GetProperty("mic").GetBoolean(),"one click exits pre-existing controller microphone mode and applies JUMP3");
   Check(state.GetProperty("dropped").GetInt32()==0,"first-click control commands survive firmware settling");
   Click("JUMP7");Click("RED");Click("GREEN");await Drained();state=await ble.SelfTest();
   Check(state.GetProperty("mode").GetInt32()==140&&selectedHardware=="GREEN","latest clicked effect applies while earlier effect is busy");
   busy=true;UpdateControls();Click("FADE7");await Task.Delay(40);busy=false;UpdateControls();await Drained();state=await ble.SelfTest();
   Check(state.GetProperty("mode").GetInt32()==138,"single click waits for another operation instead of being dropped");
   await ble.WriteFrame(Protocol.Color(1,2,3));var speed=Stopwatch.StartNew();
   for(int i=0;i<8;i++)await ble.WriteFrame(Protocol.Color(i+2,3,4));
   Check(speed.Elapsed.TotalMilliseconds<1000,"successive music frames keep fast pacing");
   Click("FADE3");await Drained();state=await ble.SelfTest();
   Check(state.GetProperty("mode").GetInt32()==137&&state.GetProperty("dropped").GetInt32()==0,"switch from music frames to autonomous effect applies without dropped controls");
   await ble.Disconnect();
  }catch(Exception ex){checks.Add("FAIL "+ex);Environment.ExitCode=1;}
  File.WriteAllLines(Path.Combine(AppContext.BaseDirectory,"single-click-validation.txt"),checks);allowClose=true;Close();
 }
}
