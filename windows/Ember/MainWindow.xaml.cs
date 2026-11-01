using System;
using System.ComponentModel;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Forms=System.Windows.Forms;
namespace Ember;
public partial class MainWindow : Window
{
 private readonly BluetoothService ble=new(Environment.GetCommandLineArgs().Contains("--verify-effects")?Environment.GetEnvironmentVariable("EMBER_TEST_BRIDGE")??throw new InvalidOperationException("Missing test bridge."):null);private readonly WasapiAudio audio=new();private Settings settings=new();private readonly DispatcherTimer timer=new(){Interval=TimeSpan.FromMilliseconds(20)};private Forms.NotifyIcon? tray;
 private AudioFrame? frame;private int beatPending;private bool ready,busy,txBusy,audioOn,closing,allowClose,sequenceActive,stripMicActive;private int effectTab;private string? selectedMusic,selectedHardware;private int ticks;private CancellationTokenSource? brightnessDebounce,speedDebounce;
 private readonly Stopwatch clock=Stopwatch.StartNew();private readonly ReactiveEffects reactive=new();private readonly EffectPlayer player=new();private List<CustomEffect> customEffects=new();private byte[]? lastStreamPacket;
 private string EffectsPath=>Path.Combine(Settings.Folder,"effects.json");
 private (string Name,int Tab)? pendingEffect;private bool choosingEffect;
 private bool Preview=>Environment.GetCommandLineArgs().Contains("--preview")||Environment.GetCommandLineArgs().Contains("--smoke-close")||Environment.GetCommandLineArgs().Contains("--verify-effects");
 public MainWindow()
 {
  WindowFit.Attach(this,!Preview);InitializeComponent();settings=Preview?new():Settings.Load();Profiles.SelectedIndex=Math.Clamp(settings.Profile,0,3);BrightnessSlider.Value=Math.Clamp(settings.Brightness,0,100);SpeedSlider.Value=Math.Clamp(settings.Speed,0,100);SensitivitySlider.Value=Math.Clamp(settings.Sensitivity,0,100);StripMicSlider.Value=Math.Clamp(settings.StripMicSensitivity,0,100);StripPalette.Hex=settings.Color;FallbackCheck.IsChecked=settings.Fallback;TrayCheck.IsChecked=settings.Tray;
  try{var bitmap=new BitmapImage(new Uri("pack://application:,,,/Assets/ember-256.png"));BrandIcon.Source=bitmap;Icon=bitmap;}catch{}
  RefreshDevices();
  if(!Preview){try{customEffects=EffectStore.Load(EffectsPath);}catch(Exception ex){Log("Не вдалося завантажити ефекти: "+ex.Message);}}
  ready=true;BuildModes();RefreshSavedEffects();BuildEffects();UpdateControls();ble.Lost+=msg=>Dispatcher.BeginInvoke(()=>Lost(msg));
  audio.Frame+=f=>{Volatile.Write(ref frame,f);if(f.Beat){Interlocked.Exchange(ref beatPending,1);Dispatcher.BeginInvoke(DispatcherPriority.Send,new Action(()=>{if(!closing&&(selectedMusic!=null||sequenceActive))Tick(null,EventArgs.Empty);}));}};
  audio.Failed+=msg=>Dispatcher.BeginInvoke(()=>AudioFailed(msg));
  Closing+=OnClosing;Closed+=(_,_)=>{timer.Stop();tray?.Dispose();ble.Dispose();audio.Dispose();};timer.Tick+=Tick;timer.Start();Loaded+=OnLoaded;
 }
 private async void OnLoaded(object sender,RoutedEventArgs e)
 {
  if(Environment.GetCommandLineArgs().Contains("--smoke-close")){_ = Dispatcher.BeginInvoke(new Action(Close));return;}
  if(Environment.GetCommandLineArgs().Contains("--verify-effects")){await VerifySingleClicks();return;}
  if(Preview){await PreviewInterface();return;}

  try {var stream=Application.GetResourceStream(new Uri("pack://application:,,,/Assets/ember.ico"))?.Stream;if(stream!=null){using(stream){tray=new Forms.NotifyIcon {Icon=new System.Drawing.Icon(stream),Text="Ember — RGB & Music",Visible=true};}tray.DoubleClick+=(_,_)=>Dispatcher.Invoke(ShowFromTray);var menu=new Forms.ContextMenuStrip();menu.Items.Add("Відкрити Ember",null,(_,_)=>Dispatcher.Invoke(ShowFromTray));menu.Items.Add("Вийти",null,(_,_)=>Dispatcher.Invoke(Close));tray.ContextMenuStrip=menu;}}catch(Exception ex){Log("Трей: "+ex.Message);}
 }
 private string ProfileName=>DeviceProfiles.Name(settings.Profile);
 private List<LampDevice> nearby=new();
 private void RefreshDevices(){Devices.Items.Clear();foreach(var saved in settings.Devices){var live=nearby.FirstOrDefault(d=>d.Address==saved.Address&&d.Address.Length>0||d.DeviceId==saved.DeviceId&&d.DeviceId.Length>0);Devices.Items.Add(live==null?new LampDevice(saved.Address,"★ "+saved.Name,null,saved.DeviceId,saved.AddressType,"saved"):live with{Name="★ "+live.Name});}foreach(var d in nearby)if(!settings.Devices.Any(s=>s.Address==d.Address&&d.Address.Length>0||s.DeviceId==d.DeviceId&&d.DeviceId.Length>0))Devices.Items.Add(d);if(Devices.Items.Count>0)Devices.SelectedIndex=0;}
 private void ForgetDeviceClick(object sender,RoutedEventArgs e){if(ble.Connected||Devices.SelectedItem is not LampDevice d)return;settings.Devices.RemoveAll(s=>s.Address==d.Address&&d.Address.Length>0||s.DeviceId==d.DeviceId&&d.DeviceId.Length>0);if(settings.Address==d.Address){settings.Address=settings.DeviceId=settings.Name="";}RefreshDevices();Save();}
 private void Save(){if(!ready||Preview)return;try{settings.Brightness=(int)BrightnessSlider.Value;settings.Speed=(int)SpeedSlider.Value;settings.Sensitivity=SensitivitySlider.Value;settings.Fallback=FallbackCheck.IsChecked==true;settings.Tray=TrayCheck.IsChecked==true;settings.Save();}catch(Exception ex){Log("Не вдалося зберегти налаштування: "+ex.Message);}}
 private void Log(string message){Status.Text=message;LogBox.AppendText($"{DateTime.Now:HH:mm:ss}  {message}\n");if(LogBox.Text.Length>10000)LogBox.Text=LogBox.Text[^8000..];LogBox.ScrollToEnd();}
 private async Task Run(Func<Task> action){if(busy||closing)return;busy=true;UpdateControls();try{await action();Save();}catch(Exception ex){Log(ex.Message);}finally{busy=false;UpdateControls();}}
 private void UpdateControls()
 {
  ScanButton.IsEnabled=!busy&&!ble.Connected;ConnectButton.IsEnabled=!busy&&!ble.Connected;DisconnectButton.IsEnabled=ApplyRelease.IsEnabled=PowerButton.IsEnabled=PowerOffButton.IsEnabled=ble.Connected&&!busy;
  Profiles.IsEnabled=false;ForgetDevice.IsEnabled=!ble.Connected&&!busy;MicTab.IsEnabled=!ble.Connected||ble.HasMicrophone;Devices.IsEnabled=!ble.Connected&&!busy;BrightnessSlider.IsEnabled=SpeedSlider.IsEnabled=ble.Connected&&!busy;ManualAddress.IsEnabled=!ble.Connected&&!busy;
  AudioButton.IsEnabled=!busy;ApplyColorButton.IsEnabled=ble.Connected&&!busy;foreach(Button b in EffectGrid.Children)b.IsEnabled=ble.Connected&&!closing;StripMicApply.IsEnabled=StripMicSlider.IsEnabled=ble.Connected&&!busy;ExportCategory.IsEnabled=effectTab>=3&&SavedEffects.Items.Count>0&&!busy;ExportEffect.IsEnabled=effectTab>=3&&SavedEffects.SelectedItem is CustomEffect&&!busy;
  bool selected=SavedEffects.SelectedItem is CustomEffect;PlayCustom.IsEnabled=selected&&ble.Connected&&!busy;PauseCustom.IsEnabled=player.Running&&ble.Connected&&!busy;StopCustom.IsEnabled=sequenceActive&&ble.Connected&&!busy;EditCustom.IsEnabled=DeleteCustom.IsEnabled=selected&&!busy;NextCustom.IsEnabled=player.Running&&!player.Paused&&player.Effect!.Steps[player.Index].Trigger==StepTrigger.Manual&&ble.Connected&&!busy;PauseCustom.Content=player.Paused?"Продовжити":"Пауза";
  if(!ble.Connected){DeviceTitle.Text="Очікує підключення";EffectState.Text="НЕ ПІДКЛЮЧЕНО";}else DeviceTitle.Text=settings.Name;
  DiagnosticText.Text=$"Профіль: {(ProfileName)} · GATT: {ble.Uuid}\nВідповідність JUMP/FADE попередня: перевір фактичний ефект на стрічці.";
 }
 private async void ScanClick(object sender,RoutedEventArgs e)=>await Run(async()=>{Log("Пошук Bluetooth-пристроїв. 12 секунд");var list=await ble.Scan();Devices.Items.Clear();nearby=list;RefreshDevices();DeviceSubtitle.Text=ble.LastScanNote;Log(list.Count==0?"Windows не показав жодного BLE-пристрою. Перевір живлення стрічки, звільни Bluetooth на телефоні та повтори пошук. "+ble.LastScanNote:$"Знайдено Bluetooth-пристроїв: {list.Count}. Вибери стрічку за назвою або MAC. "+ble.LastScanNote);});
 private void DeviceSelectionChanged(object sender,SelectionChangedEventArgs e){if(!ready)return;if(Devices.SelectedItem is LampDevice d){DeviceSubtitle.Text=$"{d.Name} · {d.Mac} · {d.Signal}"+(d.Source=="windows"?" · зі списку Windows, присутність у радіусі не підтверджена":"");if(d.Name.Contains("BLEDDM",StringComparison.OrdinalIgnoreCase))Profiles.SelectedIndex=2;else if(d.Name.Contains("MELK",StringComparison.OrdinalIgnoreCase))Profiles.SelectedIndex=1;else if(d.Name.Contains("BLEDOB",StringComparison.OrdinalIgnoreCase))Profiles.SelectedIndex=0;}}
 private async void ConnectClick(object sender,RoutedEventArgs e)=>await Run(async()=>{
  var selected=Devices.SelectedItem as LampDevice;string address=selected?.Address??"";string deviceId=selected?.DeviceId??"";int addressType=selected?.AddressType??2;var raw=ManualAddress.Text.Trim();
  if(raw.Length>0){raw=raw.Replace(":","").Replace("-","");if(raw.Length!=12||!ulong.TryParse(raw,NumberStyles.HexNumber,CultureInfo.InvariantCulture,out var mac)||mac>0xffffffffffff)throw new ArgumentException("MAC повинна містити 12 шістнадцяткових цифр.");address=mac.ToString(CultureInfo.InvariantCulture);deviceId="";addressType=2;}
  if(address.Length==0&&deviceId.Length==0)throw new InvalidOperationException("Спочатку знайди та вибери свою стрічку.");Log("Підключення до пристрою. Windows може попросити дозвіл Bluetooth.");await ble.Connect(address,deviceId,addressType,selected?.Name??"");settings.Profile=ble.Profile;if(ble.Profile==3)settings.LastWasStripMic=false;settings.LastMode=Math.Clamp(settings.LastMode,ble.Profile==1?0:135,ble.Profile==1?212:ble.Profile==3?149:156);Profiles.SelectedIndex=ble.Profile;settings.Address=ble.ConnectedAddress;settings.DeviceId=ble.ConnectedDeviceId;settings.AddressType=ble.ConnectedAddressType;settings.Name=string.IsNullOrWhiteSpace(ble.ConnectedName)?selected?.Name??"RGB стрічка":ble.ConnectedName;settings.Remember();RefreshDevices();BuildModes();BuildEffects();DeviceTitle.Text=settings.Name;DeviceSubtitle.Text="Bluetooth підключено · "+ProfileName;Log("Підключено. Вибери колір або ефект — він працюватиме на стрічці. "+ble.LastConnectionNote);
 });
 private void ProfileChanged(object sender,SelectionChangedEventArgs e){if(!ready)return;settings.Profile=Profiles.SelectedIndex;selectedHardware=null;BuildModes();BuildEffects();Save();}
 private void BuildModes(){var key=settings.Profile.ToString();if(!settings.Mappings.TryGetValue(key,out var map)||map.Length!=4){map=settings.Profile==1?new[]{77,78,79,80}:new[]{135,136,137,138};settings.Mappings[key]=map;}Jump3Id.Text=map[0].ToString();Jump7Id.Text=map[1].ToString();Fade3Id.Text=map[2].ToString();Fade7Id.Text=map[3].ToString();AllModes.Items.Clear();for(int i=settings.Profile==1?0:135;i<=(settings.Profile==1?212:156);i++)AllModes.Items.Add(new LampMode($"Режим {i:D3}",i,""));AllModes.DisplayMemberPath="Name";AllModes.SelectedIndex=0;}
 private void BuildEffects()
 {
  EffectGrid.Children.Clear();EffectGrid.Visibility=effectTab>=3?Visibility.Collapsed:Visibility.Visible;CustomPanel.Visibility=effectTab>=3?Visibility.Visible:Visibility.Collapsed;
  var presets=effectTab==1?ReactiveEffects.Music:effectTab==2?ModeCatalog.StripMicrophone:ModeCatalog.ForProfile(settings.Profile);
  foreach(var preset in presets){var stack=new StackPanel();stack.Children.Add(new System.Windows.Shapes.Path{Data=Geometry.Parse("M 0,16 C 8,-5 12,34 20,10 C 25,-2 31,7 34,14"),Stroke=(Brush)FindResource("Ember"),StrokeThickness=1.5,Width=35,Height=25,HorizontalAlignment=HorizontalAlignment.Left,Margin=new Thickness(0,0,0,12)});stack.Children.Add(new TextBlock{Text=preset.Name,FontWeight=FontWeights.SemiBold,FontSize=12});stack.Children.Add(new TextBlock{Text=preset.Hint,FontSize=10,TextWrapping=TextWrapping.Wrap,Foreground=(Brush)FindResource("Muted"),Margin=new Thickness(0,5,0,0)});var button=new Button{Content=stack,Padding=new Thickness(12,14,10,12),Margin=new Thickness(0,0,8,8),Tag=preset.Id,MinHeight=109};int tab=effectTab;button.Click+=async(_,_)=>await QueueEffect(preset.Id,tab);EffectGrid.Children.Add(button);}
  StripMicPanel.Visibility=effectTab==2?Visibility.Visible:Visibility.Collapsed;RefreshSavedEffects();MarkSelected();UpdateControls();
 }
 private void MarkSelected(){foreach(Button b in EffectGrid.Children){bool active=(string)b.Tag==(effectTab==0?selectedHardware:effectTab==2?(stripMicActive?"mic"+settings.StripMicMode:null):selectedMusic);b.BorderBrush=new SolidColorBrush(active?Color.FromRgb(255,90,31):Color.FromArgb(24,255,255,255));b.Background=new SolidColorBrush(active?Color.FromRgb(42,26,20):Color.FromRgb(22,22,23));}}

 private void ChangeTab(int tab){if(!ready)return;effectTab=tab;EffectHelp.Text=tab switch{0=>"16 апаратних режимів. Назви та реакція залежать від контролера; ID доступні в діагностиці.",1=>"16 реакцій на музику з ПК. Потрібні запущена програма та Bluetooth.",2=>"16 пресетів звукового сенсора. Вибери характер реакції та чутливість.",3=>"Власні послідовності. Для відтворення потрібні ПК і Bluetooth.",_=>"Імпортовані пакети Ember v1/v2. Їх можна редагувати та експортувати."};BuildEffects();}

 private void HardwareTabClick(object sender,RoutedEventArgs e)=>ChangeTab(0);
 private void MusicTabClick(object sender,RoutedEventArgs e)=>ChangeTab(1);
 private void MicTabClick(object sender,RoutedEventArgs e)=>ChangeTab(2);
 private void CustomTabClick(object sender,RoutedEventArgs e)=>ChangeTab(3);

 private void ImportedTabClick(object sender,RoutedEventArgs e)=>ChangeTab(4);
 private int HardwareId(string name){var map=settings.Mappings[settings.Profile.ToString()];return name switch{"JUMP3"=>map[0],"JUMP7"=>map[1],"FADE3"=>map[2],"FADE7"=>map[3],_=>ModeCatalog.ModeId(name,settings.Profile)};}

 private async Task QueueEffect(string name,int tab)
 {
  pendingEffect=(name,tab);if(choosingEffect)return;choosingEffect=true;
  try{while(pendingEffect.HasValue&&!closing&&ble.Connected){if(busy){await Task.Delay(20);continue;}var next=pendingEffect.Value;pendingEffect=null;await Run(()=>ChooseEffect(next.Name,next.Tab));}}
  finally{pendingEffect=null;choosingEffect=false;}
 }
 private async Task ChooseEffect(string name,int tab)
 {
  EffectState.Text="ЗАСТОСУВАННЯ…";Log("Застосовуємо ефект…");
  await WaitMusicWrite();StopStreaming();
  if(tab==2){if(!ble.HasMicrophone)throw new NotSupportedException("Звуковий сенсор цієї стрічки не підтримується.");int mode=int.Parse(name[3..]);settings.StripMicSensitivity=ModeCatalog.MicSensitivity[mode];StripMicSlider.Value=settings.StripMicSensitivity;await EnableStripMic(mode);settings.StripMicMode=mode;settings.LastWasStripMic=true;stripMicActive=true;selectedHardware=null;EffectState.Text="МІКРОФОН СТРІЧКИ / "+ModeCatalog.StripMicrophone[mode].Name.ToUpperInvariant();Log("Команди мікрофона надіслано стрічці. Перевір реакцію на звук біля її контролера.");}
  else if(tab==1){await LeaveStripMic();await StartAudio();await ble.Write(Protocol.Brightness((int)BrightnessSlider.Value,settings.Profile));selectedMusic=name;reactive.Reset();EffectState.Text="МУЗИКА / "+ReactiveEffects.Music.First(p=>p.Id==name).Name.ToUpperInvariant();Log("Музичний ефект запущено.");}
  else if(name=="STATIC")await SetColor();
  else{await LeaveStripMic();var id=HardwareId(name);await ble.Write(Protocol.Mode(id,settings.Profile));await ble.Write(Protocol.Brightness((int)BrightnessSlider.Value,settings.Profile));await ble.Write(Protocol.Speed((int)SpeedSlider.Value,settings.Profile));settings.LastMode=id;settings.LastWasColor=false;settings.LastWasStripMic=false;selectedHardware=name;EffectState.Text=$"{name} / ID {id}";Log($"Режим {name} (ID {id}) передано контролеру.");}
  MarkSelected();
 }

 private void StopStreaming(){sequenceActive=false;selectedMusic=null;player.Stop();lastStreamPacket=null;UpdateControls();}
 private void ApplyLocalColor(){try{StripPalette.Hex=settings.Color;}catch{StripPalette.Hex="#FF5A1F";}}

 private async Task SetColor(){await LeaveStripMic();var text=StripPalette.Hex;if(text.Length!=7||text[0]!='#')throw new ArgumentException("Колір: #RRGGBB, наприклад #FF5A1F.");var color=(Color)ColorConverter.ConvertFromString(text);await ble.Write(Protocol.Color(color.R,color.G,color.B));await ble.Write(Protocol.Brightness((int)BrightnessSlider.Value,settings.Profile));settings.Color=text.ToUpperInvariant();settings.LastWasColor=true;settings.LastWasStripMic=false;selectedHardware="STATIC";StopStreaming();ApplyLocalColor();EffectState.Text="STATIC / "+settings.Color;MarkSelected();Log("Колір передано: "+settings.Color);}
 private async void ColorClick(object sender,RoutedEventArgs e)=>await Run(async()=>{await WaitMusicWrite();await SetColor();});
 private async void PowerClick(object sender,RoutedEventArgs e)=>await Run(async()=>{StopStreaming();await WaitMusicWrite();var on=(sender as Button)?.Tag?.ToString()=="on";await ble.Write(Protocol.Power(on,settings.Profile));MarkSelected();Log(on?"Передано команду ON":"Передано команду OFF");});
 private async Task Restore(){if(settings.LastWasStripMic){await EnableStripMic(Math.Clamp(settings.StripMicMode,0,15));stripMicActive=true;}else{await LeaveStripMic();if(settings.LastWasColor){var c=Rgb.Parse(settings.Color);await ble.Write(Protocol.Color(c.R,c.G,c.B));}else await ble.Write(Protocol.Mode(Math.Clamp(settings.LastMode,settings.Profile==1?0:135,settings.Profile==1?212:156),settings.Profile));await ble.Write(Protocol.Brightness((int)BrightnessSlider.Value,settings.Profile));if(!settings.LastWasColor)await ble.Write(Protocol.Speed((int)SpeedSlider.Value,settings.Profile));}}
 private async Task EnableStripMic(int mode){await ble.Write(Protocol.Brightness((int)BrightnessSlider.Value,settings.Profile));await ble.Write(Protocol.StripMicPower(true));await Task.Delay(100);await ble.Write(Protocol.StripMicMode(ModeCatalog.MicModes[mode]));await Task.Delay(100);await ble.Write(Protocol.StripMicSensitivity(Math.Clamp(settings.StripMicSensitivity,0,100)));}
 private async Task LeaveStripMic(){await ble.Write(Protocol.StripMicPower(false));stripMicActive=false;}
 private async void StripMicApplyClick(object sender,RoutedEventArgs e)=>await Run(async()=>{if(!ble.Connected)throw new InvalidOperationException("Підключи стрічку.");settings.StripMicSensitivity=(int)StripMicSlider.Value;await ble.Write(Protocol.StripMicSensitivity(settings.StripMicSensitivity));Log("Чутливість мікрофона стрічки надіслано: "+settings.StripMicSensitivity+"%.");});

 private async Task WaitMusicWrite(){while(txBusy)await Task.Delay(20);}
 private async Task Release(bool apply){await WaitMusicWrite();bool restore=apply||((selectedMusic!=null||sequenceActive)&&FallbackCheck.IsChecked==true);StopStreaming();await WaitMusicWrite();if(restore){await Restore();Log("Автономний режим передано. Збереження після Bluetooth перевір на стрічці.");}StopStreaming();await StopAllAudio();await ble.Disconnect();EffectState.Text="ВІД'ЄДНАНО";MarkSelected();Log("Bluetooth від'єднано. Команда OFF не надсилалася.");}
 private async void DisconnectClick(object sender,RoutedEventArgs e)=>await Run(()=>Release(false));
 private async void ApplyReleaseClick(object sender,RoutedEventArgs e)=>await Run(()=>Release(true));
 private void Lost(string message){StopStreaming();UpdateControls();MarkSelected();Log(message+" Під час втрати зв'язку передати резервний ефект неможливо.");}
 private async void BrightnessChanged(object sender,RoutedPropertyChangedEventArgs<double> e){if(BrightnessText==null)return;BrightnessText.Text=$"{e.NewValue:0}%";if(BrightnessRing!=null)BrightnessRing.Value=e.NewValue/100;if(!ready)return;settings.Brightness=(int)e.NewValue;brightnessDebounce?.Cancel();brightnessDebounce=new();var token=brightnessDebounce.Token;try{await Task.Delay(220,token);Save();if(ble.Connected&&!busy)await Run(()=>ble.Write(Protocol.Brightness((int)BrightnessSlider.Value,settings.Profile)));}catch(OperationCanceledException){}}
 private async void SpeedChanged(object sender,RoutedPropertyChangedEventArgs<double> e){if(SpeedText==null)return;SpeedText.Text=$"{e.NewValue:0}%";if(!ready)return;settings.Speed=(int)e.NewValue;speedDebounce?.Cancel();speedDebounce=new();var token=speedDebounce.Token;try{await Task.Delay(220,token);Save();if(ble.Connected&&!busy)await Run(()=>ble.Write(Protocol.Speed((int)SpeedSlider.Value,settings.Profile)));}catch(OperationCanceledException){}}
 private void SensitivityChanged(object sender,RoutedPropertyChangedEventArgs<double> e){if(!ready)return;audio.Analyzer.Sensitivity=2.4-e.NewValue*.018;settings.Sensitivity=e.NewValue;Save();}
 private void SettingsChanged(object sender,RoutedEventArgs e)=>Save();
 private async Task StartAudio(){if(audioOn)return;Volatile.Write(ref frame,null);Interlocked.Exchange(ref beatPending,0);audioOn=true;UpdateAudioChoice();try{await audio.StartReady().WaitAsync(TimeSpan.FromSeconds(10));audio.Analyzer.Sensitivity=2.4-SensitivitySlider.Value*.018;Log("Аналіз музики з ПК увімкнено.");}catch{audioOn=false;await audio.Stop();UpdateAudioChoice();throw;}}

 private async Task StopAllAudio(){audioOn=false;await audio.Stop();UpdateAudioChoice();}

 private void UpdateAudioChoice(){AudioButton.Content=audioOn?"Зупинити аналіз":"Увімкнути аналіз";AudioSourceHelp.Text="Системний звук Windows. Мікрофон ПК не використовується.";EventLabel.Text="БІТИ";}


 private async void AudioClick(object sender,RoutedEventArgs e)=>await Run(async()=>{if(audioOn){bool affected=selectedMusic!=null||(player.Running&&player.Effect!.Steps.Any(s=>s.NeedsAudio));if(affected){StopStreaming();await WaitMusicWrite();if(ble.Connected)await Restore();}await StopAllAudio();Log("Аналіз зупинено.");}else await StartAudio();MarkSelected();});

 private async void AudioFailed(string message){audioOn=false;await audio.Stop();bool affected=selectedMusic!=null||(player.Running&&player.Effect!.Steps.Any(s=>s.Trigger==StepTrigger.MusicBeat));if(affected){StopStreaming();await WaitMusicWrite();if(ble.Connected)await Run(Restore);}UpdateAudioChoice();MarkSelected();Log("Аудіо: "+message);}

 private void SaveMappingClick(object sender,RoutedEventArgs e){try{var texts=new[]{Jump3Id.Text,Jump7Id.Text,Fade3Id.Text,Fade7Id.Text};var values=texts.Select(int.Parse).ToArray();int min=settings.Profile==1?0:135,max=settings.Profile==1?212:156;if(values.Any(x=>x<min||x>max))throw new ArgumentException($"ID мають бути {min}–{max} для цього профілю.");settings.Mappings[settings.Profile.ToString()]=values;Save();Log("Відповідність ID збережено на ПК.");}catch(Exception ex){Log(ex.Message);}}
 private async void RawModeClick(object sender,RoutedEventArgs e)=>await Run(async()=>{if(AllModes.SelectedItem is LampMode m){await WaitMusicWrite();StopStreaming();await LeaveStripMic();await ble.Write(Protocol.Mode(m.Id,settings.Profile));settings.LastMode=m.Id;settings.LastWasStripMic=false;settings.LastWasColor=false;selectedHardware=null;EffectState.Text=$"РЕЖИМ ID {m.Id}";MarkSelected();Log($"Режим ID {m.Id} передано. Після перевірки признач його кнопці JUMP/FADE.");}});
 private async void Tick(object? sender,EventArgs e)
 {
  double now=clock.Elapsed.TotalSeconds;var f=Volatile.Read(ref frame);bool fresh=audioOn&&f!=null&&(DateTime.UtcNow-f.Time).TotalSeconds<.4;bool musicBeat=Interlocked.Exchange(ref beatPending,0)==1&&fresh;if(++ticks%5==0){Spectrum.Update(fresh?f!.Spectrum:new double[32]);History.Push(fresh?Math.Clamp(f!.Rms*3,0,1):0);
  RmsText.Text=fresh?Db(f!.Rms):audioOn?"−∞":"—";PeakText.Text=fresh?Db(f!.Peak):audioOn?"−∞":"—";BpmText.Text=fresh&&f!.Bpm>0?$"{f.Bpm:0}":"—";BassText.Text=fresh?$"{100*f!.Bass/Math.Max(.00001,f.Bass+f.Mid+f.Treble):0}":"—";BeatsText.Text=(f?.Beats??0).ToString();AudioState.Text=audioOn?"  /  музика з ПК":"  /  вимкнено";SampleRateText.Text=audioOn&&audio.SampleRate>0?$"{audio.SampleRate/1000.0:0.#} kHz · FFT 1024":"— kHz · FFT 1024";BeatText.Text=musicBeat?"● біт":fresh?"сигнал присутній":audioOn?"тиша":"аналіз вимкнено";
  LatencyText.Text=ble.Sent>0?$"{ble.Latency:0}":"—";CommandsText.Text=ble.Sent.ToString();}if(ticks%100==0&&ble.Connected&&!busy&&!txBusy)_=ble.Poll();
  if((selectedMusic!=null||(sequenceActive&&!player.Paused))&&ble.Connected&&!busy){try{Rgb color;if(sequenceActive){bool wasRunning=player.Running;color=player.Sample(now,musicBeat,fresh?f:null);SequenceState.Text=$"{player.Effect!.Name} · крок {player.Index+1}/{player.Effect.Steps.Count} · {player.State(now)}";UpdateControls();}else color=reactive.Render(selectedMusic!,Rgb.Parse(settings.Color),now,fresh?f:null,musicBeat);
   var packet=Protocol.Color(color.R,color.G,color.B);if(!txBusy&&(lastStreamPacket==null||!lastStreamPacket.SequenceEqual(packet))){txBusy=true;try{await ble.WriteFrame(packet);lastStreamPacket=packet;}finally{txBusy=false;}}
  }catch(Exception ex){StopStreaming();MarkSelected();Log("Відтворення зупинено: "+ex.Message);}}
 }

 private static string Db(double value)=>value<1e-6?"−∞":$"{20*Math.Log10(value):0}";

 private void DragTitle(object sender,MouseButtonEventArgs e){if(e.ClickCount==2)MaximizeClick(sender,e);else if(e.LeftButton==MouseButtonState.Pressed)DragMove();}
 private void ShowFromTray(){if(closing)return;Show();WindowState=WindowState.Normal;Activate();}
 private void MinimizeClick(object sender,RoutedEventArgs e){if(TrayCheck.IsChecked==true&&tray!=null)Hide();else WindowState=WindowState.Minimized;}
 private void MaximizeClick(object sender,RoutedEventArgs e)=>WindowState=WindowState==WindowState.Maximized?WindowState.Normal:WindowState.Maximized;
 private void CloseClick(object sender,RoutedEventArgs e)=>Close();
 private async void OnClosing(object? sender,CancelEventArgs e)
 {
  if(allowClose)return;e.Cancel=true;if(closing)return;closing=true;timer.Stop();brightnessDebounce?.Cancel();speedDebounce?.Cancel();
  try{await WaitMusicWrite().WaitAsync(TimeSpan.FromSeconds(4));if(ble.Connected)await Release(false).WaitAsync(TimeSpan.FromSeconds(6));await StopAllAudio().WaitAsync(TimeSpan.FromSeconds(2));Save();}catch(Exception ex){Log("Завершення: "+ex.Message);ble.Abort();audio.Dispose();Save();}
  allowClose=true;_ = Dispatcher.BeginInvoke(DispatcherPriority.Background,new Action(Close));
 }
}
