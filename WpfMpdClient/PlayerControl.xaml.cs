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
  public partial class PlayerControl : UserControl
  {
    MpdStatus m_Status = null;
    System.Timers.Timer m_Timer = null;
    string m_Artist = string.Empty;
    string m_Album = string.Empty;
    string m_Title = string.Empty;
    bool m_TimeDragging = false;
    bool m_VolumeDragging = false;
    bool m_IgnoreVolumeChange = false;

    public PlayerControl()
    {
      InitializeComponent();

      m_Timer = new System.Timers.Timer();
      m_Timer.Interval = 1000;
      m_Timer.Elapsed += TimerHandler;
    }

    public Mpc Mpc
    {
      get;
      set;
    }

    public double CoverArtWidth
    {
      get { return imgArt.Width; }
      set 
      {
        imgArt.Width = value;
        imgArtDefault.Width = value;
      }
    }

    public double ButtonsWidth
    {
      get { return imgPrevious.Width; }
      set 
      {
        imgPrevious.Width = value;
        imgPlay.Width = value;
        imgPause.Width = value;
        imgStop.Width = value;
        imgNext.Width = value;
        imgShuffle.Width = value;
        imgRepeat.Width = value;
      }
    }

    public bool ShowVolume
    {
      get { return dockVolume.Visibility == System.Windows.Visibility.Visible; }
      set 
      {
        dockVolume.Visibility = value ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed; 
      }
    }

    public bool ShowTimeSlider
    {
      get { return gridTimeSlider.Visibility == System.Windows.Visibility.Visible; }
      set 
      {
        gridTimeSlider.Visibility = value ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed; 
      }
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

    public void Update(MpdStatus status, MpdFile currentSong)
    {
      m_Status = status;
      btnShuffle.IsChecked = status.Random;
      btnRepeat.IsChecked = status.Repeat;

      btnStop.IsEnabled = status.State != MpdState.Stop;
      switch (status.State) {
        case MpdState.Play:
          btnPlay.Visibility = Visibility.Collapsed;
          btnPause.Visibility = Visibility.Visible;
          m_Timer.Start();
          break;
        case MpdState.Pause:
        case MpdState.Stop:
          btnPlay.Visibility = Visibility.Visible;
          btnPause.Visibility = Visibility.Collapsed;
          m_Timer.Stop();
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
        m_IgnoreVolumeChange = true;
        sliVolume.Value = status.Volume;
        m_IgnoreVolumeChange = false;
      }

      if (currentSong != null) {
        lblTitle.Text = currentSong.Title;        
        if (!string.IsNullOrEmpty(currentSong.Date))
          lblAlbum.Text = string.Format("{0} ({1})", currentSong.Album, currentSong.Date);
        else
          lblAlbum.Text = currentSong.Album;
        lblArtist.Text = currentSong.Artist;

        if (currentSong.Album != m_Album || currentSong.Artist != m_Artist){
          m_Album = currentSong.Album;
          m_Artist = currentSong.Artist;
          imgArt.Source = null;
          ThreadPool.QueueUserWorkItem(new WaitCallback(GetAlbumArt));
        }
        m_Title = currentSong.Title;
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
      if (Mpc.Connected)
        Mpc.Previous();
    }

    private void btnPlay_Click(object sender, RoutedEventArgs e)
    {
      if (Mpc.Connected)
        Mpc.Play();
    }

    private void btnPause_Click(object sender, RoutedEventArgs e)
    {
      if (Mpc.Connected)
        Mpc.Pause(true);
    }

    private void btnStop_Click(object sender, RoutedEventArgs e)
    {
      if (Mpc.Connected)
        Mpc.Stop();
    }

    private void btnForward_Click(object sender, RoutedEventArgs e)
    {
      if (Mpc.Connected)
        Mpc.Next();
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
      if (Mpc.Connected)
        Mpc.Random(btnShuffle.IsChecked == true);
    }

    private void btnRepeat_Click(object sender, RoutedEventArgs e)
    {
      if (Mpc.Connected)
        Mpc.Repeat(btnRepeat.IsChecked == true);
    }

    private void sliVolume_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
      if (!m_IgnoreVolumeChange) {
        try {
          Mpc.SetVol((int)sliVolume.Value);
        }catch (Exception) {}
        lblVolume.Content = string.Format("{0}", (int)sliVolume.Value);
      }
    }

    private void sliVolume_DragStarted(object sender, System.Windows.Controls.Primitives.DragStartedEventArgs e)
    {
      m_VolumeDragging = true;
    }

    private void sliVolume_DragCompleted(object sender, System.Windows.Controls.Primitives.DragCompletedEventArgs e)
    {
      m_VolumeDragging = false;
    }

    private void TimerHandler(object sender, System.Timers.ElapsedEventArgs e)
    {
      if (ShowTimeSlider){
        Dispatcher.BeginInvoke(new Action(() =>
        {
          sliTime.Value += 1;
          lblTimeBefore.Content = Utilities.FormatSeconds((int)sliTime.Value);
          lblTimeAfter.Content = Utilities.FormatSeconds((int)sliTime.Maximum - (int)sliTime.Value);
        }));
      }
    }
  }
}
