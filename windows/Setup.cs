using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Forms;

class Setup {
 const string Version="0.5.3";
 static readonly Color Orange=Color.FromArgb(255,90,31);
 [STAThread] static int Main(string[] args){
  try{
   if(args.Length==2&&(args[0]=="--extract"||args[0]=="--extract-progress")){
    string target=Path.GetFullPath(args[1]);var values=new List<int>();
    Extract(target,p=>values.Add(p));
    if(args[0]=="--extract-progress")File.WriteAllText(Path.Combine(target,"setup-progress.txt"),string.Join(",",values));
    return 0;
   }
   Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
   var window=new InstallWindow();
   if(args.Length==2&&args[0]=="--preview")window.Preview(Path.GetFullPath(args[1]));
   Application.Run(window);return 0;
  }catch(Exception e){if(args.Length>0){Console.Error.WriteLine(e);return 1;}MessageBox.Show(e.Message,"Ember — встановлення",MessageBoxButtons.OK,MessageBoxIcon.Error);return 1;}
 }
 public static void Extract(string target,Action<int> progress=null){
  Directory.CreateDirectory(target);
  using(Stream resource=Assembly.GetExecutingAssembly().GetManifestResourceStream("Ember.payload"))
  using(var archive=new ZipArchive(resource,ZipArchiveMode.Read)){
   long total=0,done=0;int last=-1;
   foreach(var entry in archive.Entries)total+=entry.Length;
   Action<int> report=p=>{if(p!=last){last=p;if(progress!=null)progress(p);}};
   report(0);var buffer=new byte[81920];
   foreach(var entry in archive.Entries){
    string path=Path.GetFullPath(Path.Combine(target,entry.FullName));
    if(!path.StartsWith(Path.GetFullPath(target)+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Некоректний шлях у пакеті.");
    if(entry.Name.Length==0){Directory.CreateDirectory(path);continue;}
    Directory.CreateDirectory(Path.GetDirectoryName(path));
    using(var input=entry.Open())using(var output=new FileStream(path,FileMode.Create,FileAccess.Write,FileShare.None)){
     int count;while((count=input.Read(buffer,0,buffer.Length))>0){output.Write(buffer,0,count);done+=count;report(total==0?95:(int)(done*95/total));}
    }
   }
   report(95);
  }
 }
 static void Shortcut(string path,string exe){
  Type type=Type.GetTypeFromProgID("WScript.Shell");dynamic shell=Activator.CreateInstance(type);dynamic link=shell.CreateShortcut(path);link.TargetPath=exe;link.WorkingDirectory=Path.GetDirectoryName(exe);link.IconLocation=exe;link.Description="Ember — кольори, ефекти й музика для RGB-стрічки";link.Save();
 }
 sealed class DarkCheckBox:CheckBox {
  public DarkCheckBox(){AutoSize=false;Size=new Size(540,28);SetStyle(ControlStyles.UserPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.AllPaintingInWmPaint,true);}
  protected override void OnPaint(PaintEventArgs e){
   e.Graphics.Clear(BackColor);int box=16,y=(Height-box)/2;
   using(var fill=new SolidBrush(Checked?(Enabled?Orange:Color.FromArgb(128,68,45)):Color.FromArgb(28,28,30)))e.Graphics.FillRectangle(fill,1,y,box,box);
   using(var border=new Pen(Enabled?Color.FromArgb(180,180,185):Color.FromArgb(125,125,130)))e.Graphics.DrawRectangle(border,1,y,box,box);
   if(Checked){e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;using(var tick=new Pen(Color.White,2))e.Graphics.DrawLines(tick,new[]{new Point(4,y+8),new Point(8,y+12),new Point(14,y+4)});}
   TextRenderer.DrawText(e.Graphics,Text,Font,new Rectangle(28,0,Width-28,Height),Enabled?Color.FromArgb(235,235,239):Color.FromArgb(175,175,182),TextFormatFlags.Left|TextFormatFlags.VerticalCenter|TextFormatFlags.SingleLine|TextFormatFlags.NoPrefix);
   if(Focused&&ShowFocusCues)ControlPaint.DrawFocusRectangle(e.Graphics,new Rectangle(25,2,Width-27,Height-4),Color.White,BackColor);
  }
 }
 sealed class DarkButton:Button {
  public DarkButton(){FlatStyle=FlatStyle.Flat;FlatAppearance.BorderSize=0;SetStyle(ControlStyles.UserPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.AllPaintingInWmPaint,true);}
  protected override void OnPaint(PaintEventArgs e){
   e.Graphics.Clear(Enabled?Orange:Color.FromArgb(142,58,28));
   TextRenderer.DrawText(e.Graphics,Text,Font,ClientRectangle,Color.White,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter|TextFormatFlags.SingleLine|TextFormatFlags.NoPrefix);
   if(Focused&&ShowFocusCues)ControlPaint.DrawFocusRectangle(e.Graphics,new Rectangle(4,4,Width-8,Height-8),Color.White,BackColor);
  }
 }
 sealed class InstallProgress:Control {
  int value;public int Value{get{return value;}set{this.value=Math.Max(0,Math.Min(100,value));Invalidate();}}
  public InstallProgress(){SetStyle(ControlStyles.UserPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.AllPaintingInWmPaint,true);AccessibleName="Прогрес встановлення";AccessibleRole=AccessibleRole.ProgressBar;}
  protected override void OnPaint(PaintEventArgs e){
   e.Graphics.Clear(Color.FromArgb(32,32,35));
   using(var fill=new SolidBrush(Orange))e.Graphics.FillRectangle(fill,0,0,(int)(Width*Value/100.0),Height);
  }
 }
 class InstallWindow:Form {
  readonly DarkButton install=new DarkButton();readonly Label status=new Label(),percent=new Label();readonly DarkCheckBox desktop=new DarkCheckBox(),launch=new DarkCheckBox();readonly InstallProgress progress=new InstallProgress();bool installing;
  public InstallWindow(){
   Text="Ember "+Version;AutoScaleMode=AutoScaleMode.Dpi;ClientSize=new Size(600,360);FormBorderStyle=FormBorderStyle.FixedDialog;MaximizeBox=false;StartPosition=FormStartPosition.CenterScreen;BackColor=Color.FromArgb(12,12,13);ForeColor=Color.White;Font=new Font("Segoe UI",10);
   using(Stream icon=Assembly.GetExecutingAssembly().GetManifestResourceStream("Ember.icon")){Icon=new Icon(icon);}
   var title=new Label{Text="EMBER",Font=new Font("Segoe UI",25,FontStyle.Bold),ForeColor=Orange,AutoSize=true,Location=new Point(28,22)};
   var caption=new Label{Text="Встановлення Ember\nBluetooth-керування твоєю RGB-стрічкою.",ForeColor=Color.FromArgb(220,220,227),AutoSize=true,Location=new Point(30,84)};
   desktop.Text="Ярлик на робочому столі";desktop.Checked=true;desktop.Location=new Point(30,145);
   launch.Text="Відкрити Ember після встановлення";launch.Checked=true;launch.Location=new Point(30,181);
   progress.Bounds=new Rectangle(30,235,540,8);
   status.Text="Готово до встановлення";status.ForeColor=Color.FromArgb(210,210,218);status.Bounds=new Rectangle(30,252,485,28);
   percent.Text="0%";percent.ForeColor=Orange;percent.TextAlign=ContentAlignment.MiddleRight;percent.Bounds=new Rectangle(515,252,55,28);
   var footer=new Label{Text="Windows x64 · версія "+Version,ForeColor=Color.FromArgb(145,145,155),AutoSize=true,Location=new Point(30,309)};
   install.Text="Встановити";install.Font=new Font("Segoe UI",10,FontStyle.Bold);install.Bounds=new Rectangle(390,295,180,42);install.Click+=Install;
   Controls.AddRange(new Control[]{title,caption,desktop,launch,progress,status,percent,footer,install});AcceptButton=install;
  }
  void Busy(bool busy){installing=busy;desktop.Enabled=launch.Enabled=install.Enabled=!busy;install.Text=busy?"Встановлюємо…":"Встановити";}
  void Progress(int value){progress.Value=value;percent.Text=value+"%";}
  protected override void OnFormClosing(FormClosingEventArgs e){if(installing)e.Cancel=true;base.OnFormClosing(e);}
  async void Install(object sender,EventArgs e){
   if(installing)return;Busy(true);Progress(0);status.Text="Розпаковуємо файли Ember…";
   string target=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Ember","versions",Version);
   try{
    var updates=new Progress<int>(value=>Progress(value));
    await Task.Run(()=>Extract(target,value=>((IProgress<int>)updates).Report(value)));
    status.Text="Створюємо ярлики…";Progress(97);
    string exe=Path.Combine(target,"Ember.exe");
    string programs=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs),"Ember");Directory.CreateDirectory(programs);Shortcut(Path.Combine(programs,"Ember.lnk"),exe);
    if(desktop.Checked)Shortcut(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),"Ember.lnk"),exe);
    Progress(100);status.Text="Ember встановлено. Все готово!";install.Text="Готово";
    await Task.Delay(600);installing=false;
    if(launch.Checked)Process.Start(new ProcessStartInfo(exe){WorkingDirectory=target,UseShellExecute=true});
    Close();
   }catch(Exception ex){Busy(false);MessageBox.Show(ex.Message,"Не вдалося встановити Ember",MessageBoxButtons.OK,MessageBoxIcon.Error);status.Text="Не вдалося завершити. Спробуй ще раз.";}
  }
  public void Preview(string folder){
   Shown+=delegate{BeginInvoke(new Action(()=>{
    Directory.CreateDirectory(folder);SavePreview(Path.Combine(folder,"setup-ready.png"));
    Busy(true);Progress(47);status.Text="Розпаковуємо файли Ember…";SavePreview(Path.Combine(folder,"setup-installing.png"));
    Progress(100);status.Text="Ember встановлено. Все готово!";install.Text="Готово";SavePreview(Path.Combine(folder,"setup-complete.png"));
    installing=false;Close();
   }));};
  }
  void SavePreview(string path){using(var bitmap=new Bitmap(Width,Height)){DrawToBitmap(bitmap,new Rectangle(Point.Empty,Size));bitmap.Save(path,System.Drawing.Imaging.ImageFormat.Png);}}
 }
}
