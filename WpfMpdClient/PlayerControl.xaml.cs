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
using System.Windows.Navigation;
using System.Windows.Shapes;
using Libmpc;
using System.Threading;

namespace WpfMpdClient
{
  /// <summary>
  /// Logica di interazione per PlayerControl.xaml
  /// </summary>
  public partial class PlayerControl : UserControl
  {
    public delegate void PlayHandler(object sender);
    public delegate void PlayerToggleHandler(object sender, bool value);
    public event PlayHandler PlayClicked;
    public event PlayHandler PauseClicked;
    public event PlayHandler StopClicked;
    public event PlayHandler BackClicked;
    public event PlayHandler ForwardClicked;
    public event PlayerToggleHandler RandomClicked;
    public event PlayerToggleHandler RepeatClicked;

    MpdStatus m_Status = null;
    string m_Artist = string.Empty;
    string m_Album = string.Empty;
    string m_Title = string.Empty;
    bool m_TimeDragging = false;
    bool m_VolumeDragging = false;

    public PlayerControl()
    {
      InitializeComponent();
    }

    public Mpc Mpc
    {
      get;
      set;
    }

    public bool ShowStopButton
    {
      get
      {
        return btnStop.Visibility == System.Windows.Visibility.Visible;
      }

      set
      {
        btnStop.Visibility = value ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
        colStop.Width = new GridLength(value ? 1 : 0, GridUnitType.Star);
      }
    }

    public void Update(MpdStatus status)
    {
      m_Status = status;
      btnShuffle.IsChecked = status.Random;
      btnRepeat.IsChecked = status.Repeat;

      btnStop.IsEnabled = status.State != MpdState.Stop;
      switch (status.State) {
        case MpdState.Play:
          btnPlay.Visibility = Visibility.Collapsed;
          btnPause.Visibility = Visibility.Visible;
          break;
        case MpdState.Pause:
        case MpdState.Stop:
          btnPlay.Visibility = Visibility.Visible;
          btnPause.Visibility = Visibility.Collapsed;
          break;
      }

      if (status.TimeTotal > 0) {
        sliTime.Maximum = status.TimeTotal;
        if (!m_TimeDragging){
          sliTime.Value = status.TimeElapsed;
          lblTimeBefore.Content = Utilities.FormatSeconds(status.TimeElapsed);
          lblTimeAfter.Content = Utilities.FormatSeconds(status.TimeTotal - status.TimeElapsed);
        }
      } else {
        sliTime.Value = 0;
        lblTimeBefore.Content = Utilities.FormatSeconds(0);
        lblTimeAfter.Content = Utilities.FormatSeconds(0);
      }

      if (!m_VolumeDragging) {
        lblVolume.Content = string.Format("{0}", status.Volume);
        sliVolume.Value = status.Volume;
      }

      MpdFile file = Mpc.Connected ? Mpc.CurrentSong() : null;
      if (file != null) {
        lblTitle.Text = file.Title;        
        if (!string.IsNullOrEmpty(file.Date))
          lblAlbum.Text = string.Format("{0} ({1})", file.Album, file.Date);
        else
          lblAlbum.Text = file.Album;
        lblArtist.Text = file.Artist;

        if (file.Album != m_Album || file.Artist != m_Artist){
          m_Album = file.Album;
          m_Artist = file.Artist;
          imgArt.Source = null;
          ThreadPool.QueueUserWorkItem(new WaitCallback(GetAlbumArt));
        }
        m_Title = file.Title;
      }else{
        lblTitle.Text = Mpc.NoTitle;
        lblAlbum.Text = Mpc.NoAlbum;
        lblArtist.Text = Mpc.NoArtist;
        imgArt.Source = null;
      }
    }

    private void GetAlbumArt(object state)
    {
      string url = LastfmScrobbler.GetAlbumArt(m_Artist, m_Album);
      Dispatcher.BeginInvoke(new Action(() =>
      {
        if (!string.IsNullOrEmpty(url))
          imgArt.Source = new BitmapImage(new Uri(url));
        else
          imgArt.Source = null;
      }));
    }

    private void btnBack_Click(object sender, RoutedEventArgs e)
    {
      if (BackClicked != null)
        BackClicked(this);
    }

    private void btnPlay_Click(object sender, RoutedEventArgs e)
    {
      if (PlayClicked != null)
        PlayClicked(this);
    }

    private void btnPause_Click(object sender, RoutedEventArgs e)
    {
      if (PauseClicked != null)
        PauseClicked(this);
    }

    private void btnStop_Click(object sender, RoutedEventArgs e)
    {
      if (StopClicked != null)
        StopClicked(this);
    }

    private void btnForward_Click(object sender, RoutedEventArgs e)
    {
      if (ForwardClicked != null)
        ForwardClicked(this);
    }

    private void sliTime_DragStarted(object sender, System.Windows.Controls.Primitives.DragStartedEventArgs e)
    {
      m_TimeDragging = true;
    }

    private void sliTime_DragCompleted(object sender, System.Windows.Controls.Primitives.DragCompletedEventArgs e)
    {
      if (m_Status != null && Mpc.Connected)
        Mpc.Seek(m_Status.Song, (int)sliTime.Value);
      m_TimeDragging = false;
    }

    private void sliTime_DragDelta(object sender, System.Windows.Controls.Primitives.DragDeltaEventArgs e)
    {
        lblTimeBefore.Content = Utilities.FormatSeconds((int)sliTime.Value);
        lblTimeAfter.Content = Utilities.FormatSeconds((int)sliTime.Maximum - (int)sliTime.Value);      
    }

    private void btnShuffle_Click(object sender, RoutedEventArgs e)
    {
      if (RandomClicked != null)
        RandomClicked(this, btnShuffle.IsChecked == true);
    }

    private void btnRepeat_Click(object sender, RoutedEventArgs e)
    {
      if (RepeatClicked != null)
        RepeatClicked(this, btnRepeat.IsChecked == true);
    }

    private void sliVolume_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
      Mpc.SetVol((int)sliVolume.Value);
      lblVolume.Content = string.Format("{0}", (int)sliVolume.Value);
    }

    private void sliVolume_DragStarted(object sender, System.Windows.Controls.Primitives.DragStartedEventArgs e)
    {
      m_VolumeDragging = true;
    }

    private void sliVolume_DragCompleted(object sender, System.Windows.Controls.Primitives.DragCompletedEventArgs e)
    {
      m_VolumeDragging = false;
    }

  }
}
