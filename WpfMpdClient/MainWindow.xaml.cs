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
using System.Net;
using System.Timers;
using System.IO;

namespace WpfMpdClient
{
  /// <summary>
  /// Logica di interazione per MainWindow.xaml
  /// </summary>

  public partial class MainWindow : Window
  {
    static MainWindow This = null;

    Settings m_Settings = null;
    Mpc m_Mpc = null;
    Timer m_StartTimer = null;
    Timer m_Timer = null;
    List<MpdFile> m_Tracks = null;
    About m_About = new About();

    public MainWindow()
    {
      InitializeComponent();
      This = this;

      stcAbout.DataContext = m_About;
      try {
        txtLicense.Text = File.ReadAllText("LICENSE.TXT");
      } catch (Exception){ 
        txtLicense.Text = "LICENSE not found!!!";
      }

      m_Settings = Settings.Deserialize(Settings.GetSettingsFileName());
      if (m_Settings != null) {
        txtServerAddress.Text = m_Settings.ServerAddress;
        txtServerPort.Text = m_Settings.ServerPort.ToString();
        txtPassword.Password = m_Settings.Password;
        chkAutoreconnect.IsChecked = m_Settings.AutoReconnect;
      } else
        m_Settings = new Settings();

      m_Mpc = new Mpc();
      m_Mpc.OnConnected += MpcConnected;
      m_Mpc.OnDisconnected += MpcDisconnected;

      m_Timer = new Timer();

      playerControl.Mpc = m_Mpc;
      playerControl.PlayClicked += PlayClickedHandler;
      playerControl.PauseClicked += PauseClickedHandler;
      playerControl.BackClicked += BackClickedHandler;
      playerControl.ForwardClicked += ForwardClickedHandler;
      playerControl.RandomClicked += RandomClickedHandler;
      playerControl.RepeatClicked += RepeatClickedHandler;

      if (!string.IsNullOrEmpty(m_Settings.ServerAddress)){
        m_StartTimer = new Timer();
        m_StartTimer.Interval = 500;
        m_StartTimer.Elapsed += StartTimerHandler;
        m_StartTimer.Start();
      }
    }

    private void MpcConnected(Mpc connection)
    {
      if (!string.IsNullOrEmpty(m_Settings.Password)) {
        bool res = m_Mpc.Password(m_Settings.Password);
      }
      MpdStatistics stats = m_Mpc.Stats();
      PopulateArtists();
      PopulatePlaylist();

      m_Timer.Interval = 500;
      m_Timer.Elapsed += TimerHandler;
      m_Timer.Start();
    }

    private void MpcDisconnected(Mpc connection)
    {

    }

    private void Connect()
    {
      if (!string.IsNullOrEmpty(m_Settings.ServerAddress)){
        IPAddress[] addresses = System.Net.Dns.GetHostAddresses(m_Settings.ServerAddress);
        if (addresses.Length > 0){
          IPAddress ip = addresses[0];
          IPEndPoint ie = new IPEndPoint(ip, m_Settings.ServerPort);        
          m_Mpc.Connection = new MpcConnection(ie);
        }
      }
    } // Connect

    private void PopulateArtists()
    {
      if (!m_Mpc.Connected)
        return;

      List<string> artists = m_Mpc.List(ScopeSpecifier.Artist);
      artists.Sort();
      for (int i = 0; i < artists.Count; i++) {
        if (string.IsNullOrEmpty(artists[i]))
          artists[i] = "<No Artist>";
      }
      lstArtist.ItemsSource = artists;
      if (artists.Count > 0)
        lstArtist.SelectedIndex = 0;
    }

    private void lstArtist_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
      if (!m_Mpc.Connected)
        return;

      string artist = lstArtist.SelectedItem.ToString();
      List<string> albums = m_Mpc.List(ScopeSpecifier.Album, ScopeSpecifier.Artist, artist);
      albums.Sort();
      for (int i = 0; i < albums.Count; i++) {
        if (string.IsNullOrEmpty(albums[i]))
          albums[i] = "<No Album>";
      }
      lstAlbums.ItemsSource = albums;
      if (albums.Count > 0)
        lstAlbums.SelectedIndex = 0;
    }

    private void lstTracks_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {

    }

    private void lstAlbums_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
      if (!m_Mpc.Connected)
        return;

      if (lstAlbums.SelectedItem != null) {
        string album = lstAlbums.SelectedItem.ToString();
        m_Tracks = m_Mpc.Find(ScopeSpecifier.Album, album);
        lstTracks.ItemsSource = m_Tracks;
      } else {
        m_Tracks = null;
        lstTracks.ItemsSource = null;
      }
    }

    private void btnApplySettings_Click(object sender, RoutedEventArgs e)
    {
      m_Settings.ServerAddress = txtServerAddress.Text;
      m_Settings.ServerPort = int.Parse(txtServerPort.Text);
      m_Settings.Password = txtPassword.Password;
      m_Settings.AutoReconnect = chkAutoreconnect.IsChecked == true;
      m_Settings.AutoReconnectDelay = 10;

      m_Settings.Serialize(Settings.GetSettingsFileName());

      if (m_Mpc.Connected)
        m_Mpc.Connection.Disconnect();
      Connect();
    }

    private void StartTimerHandler(object sender, ElapsedEventArgs e)
    {
      m_StartTimer.Stop();
      m_StartTimer = null;
      Dispatcher.BeginInvoke(new Action(() =>
      {
        Connect();
      }));
    } // StartTimerHandler

    private void TimerHandler(object sender, ElapsedEventArgs e)
    {
      if (!m_Mpc.Connected)
        return;

      MpdStatus status = m_Mpc.Status();
      Dispatcher.BeginInvoke(new Action(() =>
      {
        btnUpdate.IsEnabled = status.UpdatingDb <= 0;
        playerControl.Update(status);
      }));      
    } // TimerHandler

    private void PopulatePlaylist()
    {
      if (!m_Mpc.Connected)
        return;

      List<MpdFile> tracks = m_Mpc.PlaylistInfo();
      lstPlaylist.ItemsSource = tracks;
    }

    private void PlayClickedHandler(object sender)
    {
      if (m_Mpc.Connected)
        m_Mpc.Play();
    }

    private void PauseClickedHandler(object sender)
    {
      if (m_Mpc.Connected)
        m_Mpc.Pause(true);
    }

    private void BackClickedHandler(object sender)
    {
      if (m_Mpc.Connected)
        m_Mpc.Previous();
    }

    private void ForwardClickedHandler(object sender)
    {
      if (m_Mpc.Connected)
        m_Mpc.Next();
    }

    private void RandomClickedHandler(object sender, bool random)
    {
      if (m_Mpc.Connected)
        m_Mpc.Random(random);
    }

    private void RepeatClickedHandler(object sender, bool repeat)
    {
      if (m_Mpc.Connected)
        m_Mpc.Repeat(repeat);
    }

    private void ContextMenu_Click(object sender, RoutedEventArgs args)
    {
      if (!m_Mpc.Connected)
        return;

      MenuItem item = sender as MenuItem;
      if (item.Name == "mnuAddReplace" || item.Name == "mnuAddReplacePlay"){
        m_Mpc.Clear();
        if (lstPlaylist.Items.Count > 0)
          lstPlaylist.ScrollIntoView(lstPlaylist.Items[0]);
      }

      if (m_Tracks != null){
        foreach (MpdFile f in m_Tracks){
          m_Mpc.Add(f.File);
        }
      }        
      if (item.Name == "mnuAddReplacePlay")
        m_Mpc.Play();
    }

    private void TracksContextMenu_Click(object sender, RoutedEventArgs args)
    {
      if (!m_Mpc.Connected)
        return;

      MenuItem mnuItem = sender as MenuItem;
      if (mnuItem.Name == "mnuAddReplace" || mnuItem.Name == "mnuAddReplacePlay"){
        m_Mpc.Clear();
        if (lstPlaylist.Items.Count > 0)
          lstPlaylist.ScrollIntoView(lstPlaylist.Items[0]);
      }

      foreach (MpdFile file  in lstTracks.SelectedItems)
        m_Mpc.Add(file.File);

      if (mnuItem.Name == "mnuAddReplacePlay")
        m_Mpc.Play();
    }


    private void btnUpdate_Click(object sender, RoutedEventArgs e)
    {
      if (m_Mpc.Connected)
        m_Mpc.Update();
    }

    private void tabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
      if (!m_Mpc.Connected)
        return;

      if (tabControl.SelectedIndex == 1){
        PopulatePlaylist();
      }else if (tabControl.SelectedIndex == 2){
        Dispatcher.BeginInvoke(new Action(() =>
        {
          StringBuilder sb = new StringBuilder();
          sb.AppendLine(m_Mpc.Stats().ToString());
          sb.AppendLine(m_Mpc.Status().ToString());
          txtServerStatus.Text = sb.ToString();
        }));      
      }
    }

    private void lstPlaylist_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
      if (!m_Mpc.Connected)
        return;

      ListViewItem item = sender as ListViewItem;
      if (item != null) {
        MpdFile file = item.DataContext as MpdFile;
        if (file != null) {
          m_Mpc.Play(file.Pos);
        }
      }
    }

    private void hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
    {
      m_About.hyperlink_RequestNavigate(sender, e);
    }

    public static void PlayPause()
    {
      if (This.m_Mpc.Connected){
        switch (This.m_Mpc.Status().State){
          case MpdState.Play:
            This.PauseClickedHandler(null);
            break;
          case MpdState.Pause:
          case MpdState.Stop:
            This.PlayClickedHandler(null);
            break;
        }
      }
    }

    public static void NextTrack()
    {
      This.ForwardClickedHandler(null);
    }

    public static void PreviousTrack()
    {
      This.BackClickedHandler(null);
    }

    private void btnClear_Click(object sender, RoutedEventArgs e)
    {
      if (m_Mpc.Connected){
        m_Mpc.Clear();
        PopulatePlaylist();
      }
    }

    private void btnSave_Click(object sender, RoutedEventArgs e)
    {
      if (m_Mpc.Connected){
        m_Mpc.Save(txtPlaylist.Text);
        txtPlaylist.Clear();
      }
    }

    private void txtPlaylist_TextChanged(object sender, TextChangedEventArgs e)
    {
      btnSave.IsEnabled = !string.IsNullOrEmpty(txtPlaylist.Text);
    }
  }
}
