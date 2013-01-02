using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using CsUpdater;
using System.Diagnostics;

namespace WpfMpdClient
{
  /// <summary>
  /// Interaction logic for UpdateWindow.xaml
  /// </summary>
  public partial class UpdateWindow : Window
  {
    Updater m_Updater = null;
    UpdaterApp m_App = null;

    public UpdateWindow(Updater updater, UpdaterApp app)
    {
      InitializeComponent();

      m_Updater = updater;
      m_App = app;
      m_Updater.DownloadingDelegate += Download;
      m_Updater.DownloadCompletedDelegate += DownloadCompleted;

      txtText.Text = string.Format("Changelog:\r\n{0}", m_App.Changelog);
      m_Updater.Download(m_App.Url, string.Format("{0}\\temp\\{1}", Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), m_App.FileName));
    }

    private void Download(string filename, double percentage)
    {
      pgbProgress.Maximum = 100;
      pgbProgress.Value = percentage;
    }

    private void DownloadCompleted(string filename)
    {
      ProcessStartInfo psInfo = new ProcessStartInfo(filename);
      psInfo.UseShellExecute = true;
      Process process = Process.Start(psInfo);
      
      MainWindow main = Owner as MainWindow;
      main.Quit();
    }
  }
}
