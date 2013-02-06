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
