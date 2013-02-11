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
