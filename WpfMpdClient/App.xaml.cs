//    WpfMpdClient
//    Copyright (C) 2012, 2013 Paolo Iommarini
//    sakya_tg@yahoo.it
//
//    This program is free software; you can redistribute it and/or modify
//    it under the terms of the GNU General Public License as published by
//    the Free Software Foundation; either version 2 of the License, or
//    (at your option) any later version.
//
//    This program is distributed in the hope that it will be useful,
//    but WITHOUT ANY WARRANTY; without even the implied warranty of
//    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
//    GNU General Public License for more details.
//
//    You should have received a copy of the GNU General Public License
//    along with this program; if not, write to the Free Software
//    Foundation, Inc., 51 Franklin St, Fifth Floor, Boston, MA  02110-1301  USA
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
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

    static async void ExceptionHandler(object sender, UnhandledExceptionEventArgs args)
    {
      Exception e = (Exception)args.ExceptionObject;
      StringBuilder sb = new StringBuilder();
      sb.AppendLine(string.Format("{0}", DateTime.Now));
      sb.AppendLine(string.Format("OS : {0} {1} {2}",
                                 System.Environment.OSVersion.Platform,
                                 System.Environment.OSVersion.ServicePack,
                                 System.Environment.OSVersion.Version));
      sb.AppendLine(string.Format("MPD: {0}", WpfMpdClient.MainWindow.MpdVersion()));

      sb.AppendLine(e.Message);
      sb.AppendLine(e.StackTrace);

      string logName = string.Format("{0}\\wpfmpdclient\\log.txt", Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData));
      try {
        using (StreamWriter sw = File.AppendText(logName)) 
        {
          sw.WriteLine(sb.ToString());
        }
      }catch (Exception) {}

      await Application.Current.Dispatcher.BeginInvoke(new Action(() =>
      {
        BugReportWindow dlg = new BugReportWindow();
        if (dlg.ShowDialog() == true) {
          BugReporter bugReporter = new BugReporter(new Uri("http://www.sakya.it/updater/bugreport.php"));
          bugReporter.BugReport("WpfMpdClient", Assembly.GetExecutingAssembly().GetName().Version.ToString(), "Windows",
                                dlg.Email, sb.ToString());
        }
      }));

      Application.Current.Shutdown();
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
