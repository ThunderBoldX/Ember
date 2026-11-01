using System;
using System.Diagnostics;
using System.IO;
using System.Windows.Forms;
class Launcher {
 [STAThread] static void Main(string[] args) {
  string root=AppDomain.CurrentDomain.BaseDirectory;
  string host=Path.Combine(root,"runtime","dotnet.exe"), app=Path.Combine(root,"app","Ember.dll");
  try {
   if(!File.Exists(host)||!File.Exists(app))throw new FileNotFoundException("Не знайдено файли Ember. Запусти Setup для відновлення програми.");
   var info=new ProcessStartInfo(host,"\""+app+"\""+(Array.IndexOf(args,"--preview")>=0?" --preview":Array.IndexOf(args,"--smoke-close")>=0?" --smoke-close":""));
   info.WorkingDirectory=root;info.UseShellExecute=false;info.CreateNoWindow=true;
   Process.Start(info);
  } catch(Exception ex) {
   try { File.WriteAllText(Path.Combine(root,"Ember-launch.log"),ex.ToString()); } catch { }
   if(Array.IndexOf(args,"--preview")<0)MessageBox.Show(ex.Message,"Ember",MessageBoxButtons.OK,MessageBoxIcon.Error);
  }
 }
}
