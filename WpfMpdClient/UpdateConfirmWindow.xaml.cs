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

namespace WpfMpdClient
{
  public partial class UpdateConfirmWindow : Window
  {
    public UpdateConfirmWindow(CsUpdater.UpdaterApp app)
    {
      InitializeComponent();

      Title = string.Format("Update to v.{0}", app.Version);
      txtText.Text = "A new update is available.\r\nDownload and install it now?";
      txtChangelog.Text = app.Changelog;
    }

    private void btnYes_Click(object sender, RoutedEventArgs e)
    {
      DialogResult = true;
    }

    private void btnNo_Click(object sender, RoutedEventArgs e)
    {
      DialogResult = false;
    }
  }
}
