using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Windows;

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
        KListener.KeyDown += new RawKeyEventHandler(KListener_KeyDown);
    }

    void KListener_KeyDown(object sender, RawKeyEventArgs args)
    {      
      switch(args.Key){
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
