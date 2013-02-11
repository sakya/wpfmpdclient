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
  public partial class UpdateWindow : Window
  {
    Updater m_Updater = null;
    UpdaterApp m_App = null;

    public UpdateWindow(Updater updater, UpdaterApp app)
    {
      InitializeComponent();

      Title = string.Format("Update to v.{0}", app.Version);
      m_Updater = updater;
      m_App = app;
      m_Updater.DownloadingDelegate += Download;
      m_Updater.DownloadCompletedDelegate += DownloadCompleted;
      m_Updater.DownloadFailedDelegate += DownloadFailed;

      m_Updater.Download(m_App.Url, string.Format("{0}\\{1}", System.IO.Path.GetTempPath(), m_App.FileName));
    }

    private void Download(string filename, double percentage)
    {
      pgbProgress.Maximum = 100;
      pgbProgress.Value = percentage;
    }

    private void DownloadCompleted(string filename)
    {
      if (!System.IO.File.Exists(filename)){
        MessageBox.Show("Failed to download and install the update.\nPlease download and install the update manually.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
        Process.Start(new ProcessStartInfo("http://www.sakya.it/wordpress/?page_id=250"));
      }else{
        ProcessStartInfo psInfo = new ProcessStartInfo(filename);
        psInfo.UseShellExecute = true;
        Process process = Process.Start(psInfo);
      
        MainWindow main = Owner as MainWindow;
        main.Quit();
      }
    }

    private void DownloadFailed(string filename, Exception exception)
    {
        MessageBox.Show(string.Format("Failed to download the update.\n\n{0}", exception.Message),
                        "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
    }
  }
}
