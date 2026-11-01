using System;
using System.IO;
using System.Windows;
namespace Ember;
public partial class App : Application
{
 protected override void OnStartup(StartupEventArgs e)
 {
  DispatcherUnhandledException += (_, a) => { if(Array.IndexOf(Environment.GetCommandLineArgs(),"--smoke-close")>=0){File.WriteAllText(System.IO.Path.Combine(AppContext.BaseDirectory,"close-error.txt"),a.Exception.ToString());Environment.ExitCode=1;Shutdown(1);}else{MessageBox.Show(a.Exception.Message,"Ember — помилка");a.Handled=true;} };
  base.OnStartup(e);
 }
}
