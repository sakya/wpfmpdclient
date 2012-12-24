﻿using System;
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
    public event PlayHandler BackClicked;
    public event PlayHandler ForwardClicked;
    public event PlayerToggleHandler RandomClicked;
    public event PlayerToggleHandler RepeatClicked;

    MpdStatus m_Status = null;
    string m_Artist = string.Empty;
    string m_Album = string.Empty;

    public PlayerControl()
    {
      InitializeComponent();
    }

    public Mpc Mpc
    {
      get;
      set;
    }

    public void Update(MpdStatus status)
    {
      m_Status = status;
      btnShuffle.IsChecked = status.Random;
      btnRepeat.IsChecked = status.Repeat;

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
        sliTime.Value = status.TimeElapsed;
        lblTimeBefore.Content = Utilities.FormatSeconds(status.TimeElapsed);
        lblTimeAfter.Content = Utilities.FormatSeconds(status.TimeTotal - status.TimeElapsed);
      } else {
        sliTime.Value = 0;
        lblTimeBefore.Content = Utilities.FormatSeconds(0);
        lblTimeAfter.Content = Utilities.FormatSeconds(0);
      }

      MpdFile file = Mpc.CurrentSong();
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
          ThreadPool.QueueUserWorkItem(new WaitCallback(GetAlbumArt));
        }
      }else{
        lblTitle.Text = Mpc.NoTitle;
        lblAlbum.Text = Mpc.NoAlbum;
        lblArtist.Text = Mpc.NoArtist;
        imgArt.Source = null;
      }
    }

    private void GetAlbumArt(object state)
    {
      string url = Utilities.GetAlbumArt(m_Artist, m_Album);
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

    private void btnForward_Click(object sender, RoutedEventArgs e)
    {
      if (ForwardClicked != null)
        ForwardClicked(this);
    }

    private void sliTime_DragCompleted(object sender, System.Windows.Controls.Primitives.DragCompletedEventArgs e)
    {
      if (m_Status != null && Mpc.Connected)
        Mpc.Seek(m_Status.Song, (int)sliTime.Value);
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
  }
}
