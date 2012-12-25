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
using System.ComponentModel;

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
    Timer m_ReconnectTimer = null;
    List<MpdFile> m_Tracks = null;
    MpdFile m_CurrentTrack = null;
    About m_About = new About();
    System.Windows.Forms.NotifyIcon m_NotifyIcon = null;
    ContextMenu m_NotifyIconMenu = null;
    WindowState m_StoredWindowState = WindowState.Normal;
    bool m_Close = false;

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
        chkMinimizeToTray.IsChecked = m_Settings.MinimizeToTray;
        chkCloseToTray.IsChecked = m_Settings.CloseToTray;
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

      m_NotifyIcon = new System.Windows.Forms.NotifyIcon();
      m_NotifyIcon.Icon = new System.Drawing.Icon("mpd_icon.ico", new System.Drawing.Size(32,32));
      m_NotifyIcon.MouseDown += new System.Windows.Forms.MouseEventHandler(NotifyIcon_MouseDown);
      m_NotifyIconMenu = (ContextMenu)this.FindResource("TrayIconContextMenu");
      Closing += CloseHandler;

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
      PopulateGenres();
      PopulatePlaylists();
      PopulatePlaylist();

      m_Timer.Interval = 500;
      m_Timer.Elapsed += TimerHandler;
      m_Timer.Start();
    }

    private void MpcDisconnected(Mpc connection)
    {
      if (m_Settings.AutoReconnect && m_ReconnectTimer == null){
        m_ReconnectTimer = new Timer();
        m_ReconnectTimer.Interval = m_Settings.AutoReconnectDelay * 1000;
        m_ReconnectTimer.Elapsed += ReconnectTimerHandler;
        m_ReconnectTimer.Start();
      }
    }

    private void Connect()
    {
      if (!string.IsNullOrEmpty(m_Settings.ServerAddress)){
        IPAddress[] addresses = System.Net.Dns.GetHostAddresses(m_Settings.ServerAddress);
        if (addresses.Length > 0){
          IPAddress ip = addresses[0];
          IPEndPoint ie = new IPEndPoint(ip, m_Settings.ServerPort);       
          try{
            m_Mpc.Connection = new MpcConnection(ie);
          }catch (Exception ex){
            MessageBox.Show(string.Format("Error connecting to server:\r\n{0}", ex.Message), "Error", MessageBoxButton.OK, MessageBoxImage.Error);
          }
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
          artists[i] = Mpc.NoArtist;
      }
      lstArtist.ItemsSource = artists;
      if (artists.Count > 0)
        lstArtist.SelectedIndex = 0;
    }

    private void PopulateGenres()
    {
      if (!m_Mpc.Connected)
        return;

      List<string> genres = m_Mpc.List(ScopeSpecifier.Genre);
      genres.Sort();
      for (int i = 0; i < genres.Count; i++) {
        if (string.IsNullOrEmpty(genres[i]))
          genres[i] = Mpc.NoGenre;
      }
      lstGenres.ItemsSource = genres;
      if (genres.Count > 0)
        lstGenres.SelectedIndex = 0;
    }

    private void PopulatePlaylists()
    {
      if (!m_Mpc.Connected)
        return;

      List<string> playlists = m_Mpc.ListPlaylists();
      playlists.Sort();
      lstPlaylists.ItemsSource = playlists;
      if (playlists.Count > 0)
        lstPlaylists.SelectedIndex = 0;
    }

    private void lstArtist_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
      if (!m_Mpc.Connected)
        return;

      string artist = lstArtist.SelectedItem.ToString();
      if (artist == Mpc.NoArtist)
        artist = string.Empty;

      List<string> albums = m_Mpc.List(ScopeSpecifier.Album, ScopeSpecifier.Artist, artist);
      albums.Sort();
      for (int i = 0; i < albums.Count; i++) {
        if (string.IsNullOrEmpty(albums[i]))
          albums[i] = Mpc.NoAlbum;
      }
      lstAlbums.ItemsSource = albums;
      if (albums.Count > 0)
        lstAlbums.SelectedIndex = 0;
    }

    private void lstGenres_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
      if (!m_Mpc.Connected)
        return;

      string genre = lstGenres.SelectedItem.ToString();
      if (genre == Mpc.NoGenre)
        genre = string.Empty;

      List<string> albums = m_Mpc.List(ScopeSpecifier.Album, ScopeSpecifier.Genre, genre);
      albums.Sort();
      for (int i = 0; i < albums.Count; i++) {
        if (string.IsNullOrEmpty(albums[i]))
          albums[i] = Mpc.NoAlbum;
      }
      lstGenresAlbums.ItemsSource = albums;
      if (albums.Count > 0)
        lstGenresAlbums.SelectedIndex = 0;
    }

    private void lstPlaylists_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
      if (!m_Mpc.Connected)
        return;

      ListBox list = sender as ListBox;
      if (list.SelectedItem != null) {
        string playlist = list.SelectedItem.ToString();

        m_Tracks = m_Mpc.ListPlaylistInfo(playlist);
        lstTracks.ItemsSource = m_Tracks;
      } else {
        m_Tracks = null;
        lstTracks.ItemsSource = null;
      }
    } 

    private void lstTracks_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {

    }

    private void lstAlbums_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
      if (!m_Mpc.Connected)
        return;

      ListBox list = sender as ListBox;
      if (list.SelectedItem != null) {
        string album = list.SelectedItem.ToString();
        if (album == Mpc.NoAlbum)
          album = string.Empty;

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
      m_Settings.MinimizeToTray = chkMinimizeToTray.IsChecked == true;
      m_Settings.CloseToTray = chkCloseToTray.IsChecked == true;

      m_Settings.Serialize(Settings.GetSettingsFileName());

      if (m_Mpc.Connected)
        m_Mpc.Connection.Disconnect();
      Connect();
    }

    private void ReconnectTimerHandler(object sender, ElapsedEventArgs e)
    {
      m_ReconnectTimer.Stop();
      m_ReconnectTimer = null;
      Dispatcher.BeginInvoke(new Action(() =>
      {
        Connect();
      }));
    } // ReconnectTimerHandler

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
        MenuItem m = m_NotifyIconMenu.Items[1] as MenuItem;
        m.Visibility = status.State != MpdState.Play ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
        m = m_NotifyIconMenu.Items[2] as MenuItem;
        m.Visibility = status.State == MpdState.Play ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;

        MpdFile file = m_Mpc.CurrentSong();
        if (m_CurrentTrack == null || m_CurrentTrack.Id != file.Id){
          TrackChanged(file);
        }
        m_CurrentTrack = file;
        SelectCurrentTrack();
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
      SelectCurrentTrack();
    }

    private void SelectCurrentTrack()
    {
      List<MpdFile> playList = lstPlaylist.ItemsSource as List<MpdFile>;
      if (playList != null){
        if (m_CurrentTrack != null){
          foreach (MpdFile f in playList){
            if (f.Id == m_CurrentTrack.Id){
              lstPlaylist.SelectedItem = f;
              break;
            }
          }
        }
      }
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
      if (item.Name == "mnuDeletePlaylist") {
        string playlist = lstPlaylists.SelectedItem.ToString();
        m_Mpc.Rm(playlist);
        PopulatePlaylists();
        return;
      }

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

      if (e.AddedItems.Count > 0){
        TabItem tab = e.AddedItems[0] as TabItem;
        if (tab == null)
          return;
      }

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

    private void tabBrowse_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
      if (!m_Mpc.Connected)
        return;

      if (e.AddedItems.Count > 0) {
        TabItem tab = e.AddedItems[0] as TabItem;
        if (tab == null)
          return;
      }

      if (tabBrowse.SelectedIndex == 0)
        lstAlbums_SelectionChanged(lstAlbums, null);
      else if (tabBrowse.SelectedIndex == 1)
        lstAlbums_SelectionChanged(lstGenresAlbums, null);
      else if (tabBrowse.SelectedIndex == 2)
        lstPlaylists_SelectionChanged(lstPlaylists, null);
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

    private void CloseHandler(object sender, CancelEventArgs e)
    {
      if (!m_Close && m_Settings.CloseToTray){
        Hide();
        if (m_NotifyIcon != null && !m_Close){
          m_NotifyIcon.BalloonTipText = "WpfMpdClient has been minimized. Click the tray icon to show.";
          m_NotifyIcon.BalloonTipTitle = "WpfMpdClient";

          m_NotifyIcon.Visible = true;
          m_NotifyIcon.ShowBalloonTip(2000);
        }
        e.Cancel = true;
      }
    } // CloseHandler

    private void Window_StateChanged(object sender, EventArgs e)
    {
      if (WindowState == System.Windows.WindowState.Minimized && m_Settings.MinimizeToTray){
        Hide();
        if (m_NotifyIcon != null){
          m_NotifyIcon.BalloonTipText = "WpfMpdClient has been minimized. Click the tray icon to show.";
          m_NotifyIcon.BalloonTipTitle = "WpfMpdClient";
          m_NotifyIcon.Visible = true;
          m_NotifyIcon.ShowBalloonTip(2000);
        }
      }
    } // Window_StateChanged

    private void NotifyIcon_MouseDown(object sender, System.Windows.Forms.MouseEventArgs e)
    {
      if (e.Button == System.Windows.Forms.MouseButtons.Right) {
        m_NotifyIconMenu.IsOpen = !m_NotifyIconMenu.IsOpen;
      }else if (e.Button == System.Windows.Forms.MouseButtons.Left){
        m_NotifyIconMenu.IsOpen = false;
        Show();
        WindowState = m_StoredWindowState;
        Focus();
        m_NotifyIcon.Visible = false;
      }
    } // NotifyIcon_MouseDown

    private void TrackChanged(MpdFile track)
    {
      txtLyrics.Text = "Downloading lyrics";
      System.Threading.ThreadPool.QueueUserWorkItem(new System.Threading.WaitCallback(GetLyrics));

      if (m_NotifyIcon != null && m_NotifyIcon.Visible && track != null) {
        m_NotifyIcon.BalloonTipText = string.Format("\"{0}\"\r\n{1}\r\n{2}", track.Title, track.Album, track.Artist);
        m_NotifyIcon.BalloonTipTitle = "WpfMpdClient";
        m_NotifyIcon.ShowBalloonTip(2000);
      }
    } // TrackChanged

    private void mnuQuit_Click(object sender, RoutedEventArgs e)
    {
      m_NotifyIcon.Visible = false;
      m_Close = true;
      Application.Current.Shutdown();
    }

    private void mnuPrevious_Click(object sender, RoutedEventArgs e)
    {
      PreviousTrack();
    }

    private void mnuNext_Click(object sender, RoutedEventArgs e)
    {
      NextTrack();
    }

    private void mnuPlay_Click(object sender, RoutedEventArgs e)
    {
      PlayClickedHandler(null);
    }

    private void mnuPause_Click(object sender, RoutedEventArgs e)
    {
      PauseClickedHandler(null);
    }

    private void GetLyrics(object state)
    {
      if (m_CurrentTrack != null) {
        string lyrics = Utilities.GetLyrics(m_CurrentTrack.Artist, m_CurrentTrack.Title);
        if (string.IsNullOrEmpty(lyrics))
          lyrics = "No lyrics found";

        Dispatcher.BeginInvoke(new Action(() =>
        {
          txtLyrics.Text = lyrics;
          scrLyrics.ScrollToTop();
        }));
      }
    }

  }
}
