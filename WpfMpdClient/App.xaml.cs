using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Windows;
using System.IO;
using System.Text;
using CsUpdater;
using System.Reflection;

namespace WpfMpdClient
{
  /// <summary>
  /// Logica di interazione per App.xaml
  /// </summary>
  public partial class App : Application
  {
    KeyboardListener KListener = new KeyboardListener();

    private void Application_Startup(object sender, StartupEventArgs e)
    {
      AppDomain currentDomain = AppDomain.CurrentDomain;
      currentDomain.UnhandledException += new UnhandledExceptionEventHandler(ExceptionHandler);

      KListener.KeyDown += new RawKeyEventHandler(KListener_KeyDown);
    }

    static void ExceptionHandler(object sender, UnhandledExceptionEventArgs args)
    {
      Exception e = (Exception)args.ExceptionObject;
      StringBuilder sb = new StringBuilder();
      sb.AppendLine(string.Format("{0}", DateTime.Now));
      sb.AppendLine(string.Format("OS: {0} {1} {2}",
                                 System.Environment.OSVersion.Platform,
                                 System.Environment.OSVersion.ServicePack,
                                 System.Environment.OSVersion.Version));
      sb.AppendLine(e.Message);
      sb.AppendLine(e.StackTrace);

      string logName = string.Format("{0}\\wpfmpdclient\\log.txt", Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData));
      try {
        using (StreamWriter sw = File.AppendText(logName)) 
        {
          sw.WriteLine(sb.ToString());
        }
      }catch (Exception) {}

      if (Utilities.Confirm("Crash", "WpfMpdClient crashed.\r\nWould you like to send a bug report?\r\n\r\nNo private informations will be sent.")) {
        BugReporter bugReporter = new BugReporter(new Uri("http://www.sakya.it/updater/bugreport.php"));
        bugReporter.BugReport("WpfMpdClient", Assembly.GetExecutingAssembly().GetName().Version.ToString(), "Windows", sb.ToString());
      }

      throw e;
    }

    void KListener_KeyDown(object sender, RawKeyEventArgs args)
    {
      switch (args.Key) {
        case System.Windows.Input.Key.MediaStop:
          WpfMpdClient.MainWindow.Stop();
          break;
        case System.Windows.Input.Key.MediaPlayPause:
          WpfMpdClient.MainWindow.PlayPause();
          break;
        case System.Windows.Input.Key.MediaNextTrack:
          WpfMpdClient.MainWindow.NextTrack();
          break;
        case System.Windows.Input.Key.MediaPreviousTrack:
          WpfMpdClient.MainWindow.PreviousTrack();
          break;
      }
    }

    private void Application_Exit(object sender, ExitEventArgs e)
    {
      KListener.Dispose();
    }
  }
}
