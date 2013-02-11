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
using Libmpc;

namespace WpfMpdClient
{
  public partial class MiniPlayerWindow : Window
  {
    Settings m_Settings = null;

    public MiniPlayerWindow(Mpc mpc, Settings settings)
    {
      InitializeComponent();

      m_Settings = settings;
      playerControl.Mpc = mpc;
    }

    public void Update(MpdStatus status, MpdFile currentSong)
    {
      playerControl.Update(status, currentSong);
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e) 
    { 
        base.OnMouseLeftButtonDown(e); 
        if (e.ButtonState == MouseButtonState.Pressed) 
            DragMove(); 
    }

    private void Window_LocationChanged(object sender, EventArgs e)
    {
      m_Settings.MiniWindowLeft = Left;
      m_Settings.MiniWindowTop = Top;
    } 
  }
}
