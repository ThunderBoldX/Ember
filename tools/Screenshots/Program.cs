using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Ember;

static class Program
{
 const BindingFlags Private=BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.DeclaredOnly;
 static MainWindow window=null!;
 static T Field<T>(string name)=>(T)typeof(MainWindow).GetField(name,Private)!.GetValue(window)!;
 static void Set(string name,object value)=>typeof(MainWindow).GetField(name,Private)!.SetValue(window,value);
 static object? Call(string name,params object?[] args)=>typeof(MainWindow).GetMethod(name,Private)!.Invoke(window,args);
 static T Control<T>(string name)=>(T)window.FindName(name);
 static void Connected(bool value)=>typeof(BluetoothService).GetProperty("Connected")!.SetValue(Field<BluetoothService>("ble"),value);
 [STAThread] static int Main(string[] args)
 {
  if(!args.Contains("--preview"))throw new ArgumentException("Pass --preview to isolate screenshot data from user settings.");
  string output=Path.GetFullPath(args.Last());Directory.CreateDirectory(output);
  var app=new System.Windows.Application();app.Resources.MergedDictionaries.Add(new ResourceDictionary{Source=new Uri("pack://application:,,,/Ember;component/Theme.xaml")});
  window=new MainWindow();Field<DispatcherTimer>("timer").Stop();
  var original=(RoutedEventHandler)Delegate.CreateDelegate(typeof(RoutedEventHandler),window,typeof(MainWindow).GetMethod("OnLoaded",Private)!);
  window.Loaded-=original;
  window.Loaded+=async(_,_)=>{
   try{await Capture(output);}catch(Exception e){File.WriteAllText(Path.Combine(output,"capture-error.txt"),e.ToString());Environment.ExitCode=1;}
   finally{Connected(false);Set("allowClose",true);window.Close();}
  };
  app.Run(window);return Environment.ExitCode;
 }
 static async Task Capture(string output)
 {
  window.Width=1440;window.Height=1080;
  var settings=Field<Settings>("settings");settings.Name="ELK-BLEDDM · Studio Demo";settings.Brightness=70;settings.Speed=50;
  settings.Devices.Add(new SavedLamp{Name=settings.Name,Address="187723572702721",DeviceId="demo-studio",AddressType=0,Profile=2});
  Connected(true);Call("RefreshDevices");Call("UpdateControls");
  Control<TextBlock>("DeviceSubtitle").Text="Демонстраційна стрічка · AA:BB:CC:DD:EE:01 · без реального Bluetooth";
  Control<TextBlock>("EffectState").Text="ДЕМО";
  Control<TextBlock>("Status").Text="Демонстраційні дані · назви, адреси й аудіосигнал вигадані";
  var brand=(StackPanel)Control<System.Windows.Controls.Image>("BrandIcon").Parent;
  brand.Children.Add(new TextBlock{Text="ДЕМОНСТРАЦІЙНІ ДАНІ",Foreground=new SolidColorBrush(Color.FromRgb(185,150,130)),FontSize=10,Margin=new Thickness(20,0,0,0),VerticalAlignment=VerticalAlignment.Center});
  double[] bars=Enumerable.Range(0,32).Select(i=>.15+.66*Math.Exp(-Math.Pow((i-8)/7.0,2))+.14*Math.Sin(i*1.1)).Select(v=>Math.Clamp(v,.06,.94)).ToArray();
  Control<SpectrumView>("Spectrum").Update(bars);for(int i=0;i<96;i++)Control<HistoryView>("History").Push(.15+.5*Math.Pow(Math.Sin(i*.19),6)+.1*Math.Sin(i*.51));
  Control<TextBlock>("AudioState").Text=" / ДЕМО-СИГНАЛ";Control<TextBlock>("SampleRateText").Text="Демонстрація · FFT 1024";
  Control<TextBlock>("BpmText").Text="120";Control<TextBlock>("RmsText").Text="−18";Control<TextBlock>("PeakText").Text="−6";Control<TextBlock>("BassText").Text="42";Control<TextBlock>("BeatsText").Text="24";
  Control<TextBlock>("BeatText").Text="Демонстраційний аудіосигнал";
  await Task.Delay(120);Save(window,Path.Combine(output,"windows-main.png"));
  async Task View(string tab,string name){Control<RadioButton>(tab).IsChecked=true;window.UpdateLayout();var scroll=Control<ScrollViewer>("MainScroll");scroll.ScrollToVerticalOffset(Control<Border>("EffectsCard").TranslatePoint(new Point(),scroll).Y+scroll.VerticalOffset-12);await Task.Delay(100);Save(window,Path.Combine(output,name));}
  await View("HardwareTab","windows-effects.png");await View("MusicTab","windows-music.png");await View("MicTab","windows-strip-sound.png");
  var effect=new CustomEffect{Name="Evening Gradient · Demo",Steps={new(){Color="#FF5A1F",DurationMs=1200,Transition=StepTransition.Fade},new(){Color="#0066FF",DurationMs=1800,Transition=StepTransition.Fade,Trigger=StepTrigger.MusicBeat,BeatCount=4},new(){Color="#B400FF",DurationMs=1200,Transition=StepTransition.Fade}}};
  Field<List<CustomEffect>>("customEffects").Add(effect);Call("RefreshSavedEffects",new object?[]{null});await View("CustomTab","windows-sequences.png");
  var editor=new EffectEditor(effect){Owner=window,Icon=window.Icon};editor.Show();await Task.Delay(150);Save(editor,Path.Combine(output,"windows-editor.png"));editor.Close();
 }
 static void Save(Window target,string path)
 {
  target.UpdateLayout();var bitmap=new RenderTargetBitmap((int)target.ActualWidth,(int)target.ActualHeight,96,96,PixelFormats.Pbgra32);bitmap.Render(target);var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(bitmap));using var stream=File.Create(path);png.Save(stream);
 }
}
