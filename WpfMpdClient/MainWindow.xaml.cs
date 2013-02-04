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
using CsUpdater;
using System.Reflection;
using System.Collections.ObjectModel;

namespace WpfMpdClient
{
  public partial class MainWindow : Window
  {
    static MainWindow This = null;

    #region Private members
    LastfmScrobbler m_LastfmScrobbler = null;
    Updater m_Updater = null;
    UpdaterApp m_App = null;
    Settings m_Settings = null;
    Mpc m_Mpc = null;
    Mpc m_MpcIdle = null;
    MpdStatus m_LastStatus = null;
    Timer m_StartTimer = null;
    Timer m_ReconnectTimer = null;
    List<MpdFile> m_Tracks = null;
    MpdFile m_CurrentTrack = null;
    DateTime m_CurrentTrackStart = DateTime.MinValue;
    About m_About = new About();
    System.Windows.Forms.NotifyIcon m_NotifyIcon = null;
    ContextMenu m_NotifyIconMenu = null;
    WindowState m_StoredWindowState = WindowState.Normal;
    bool m_Close = false;

    ArtDownloader m_ArtDownloader = new ArtDownloader();
    ObservableCollection<ListboxEntry> m_ArtistsSource = new ObservableCollection<ListboxEntry>();
    ObservableCollection<ListboxEntry> m_AlbumsSource = new ObservableCollection<ListboxEntry>();
    ObservableCollection<ListboxEntry> m_GenresAlbumsSource = new ObservableCollection<ListboxEntry>();
    #endregion

    public MainWindow()
    {
      InitializeComponent();
      This = this;

      Title = string.Format("WpfMpdClient v.{0}", Assembly.GetExecutingAssembly().GetName().Version);
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
        chkShowStop.IsChecked = m_Settings.ShowStopButton;
        chkShowFilesystem.IsChecked = m_Settings.ShowFilesystemTab;
        chkMinimizeToTray.IsChecked = m_Settings.MinimizeToTray;
        chkCloseToTray.IsChecked = m_Settings.CloseToTray;
        chkScrobbler.IsChecked = m_Settings.Scrobbler;
      } else
        m_Settings = new Settings();
      m_LastfmScrobbler = new LastfmScrobbler(Utilities.DecryptString(m_Settings.ScrobblerSessionKey));

      if (m_Settings.WindowWidth > 0 && m_Settings.WindowHeight > 0){
        Width = m_Settings.WindowWidth;
        Height = m_Settings.WindowHeight;
      }
      if (m_Settings.WindowLeft >= 0 && m_Settings.WindowHeight >= 0){
        Left = m_Settings.WindowLeft;
        Top = m_Settings.WindowTop;
      }else
        WindowStartupLocation = System.Windows.WindowStartupLocation.CenterOwner;
      if (m_Settings.WindowMaximized)
        WindowState = System.Windows.WindowState.Maximized;

      m_Mpc = new Mpc();
      m_Mpc.OnConnected += MpcConnected;
      m_Mpc.OnDisconnected += MpcDisconnected;

      m_MpcIdle = new Mpc();
      m_MpcIdle.OnConnected += MpcIdleConnected;
      m_MpcIdle.OnSubsystemsChanged += MpcIdleSubsystemsChanged;

      cmbSearch.SelectedIndex = 0;

      tabFileSystem.Visibility = m_Settings.ShowFilesystemTab ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
      playerControl.ShowStopButton = m_Settings.ShowStopButton;
      playerControl.Mpc = m_Mpc;
      playerControl.PlayClicked += PlayClickedHandler;
      playerControl.PauseClicked += PauseClickedHandler;
      playerControl.BackClicked += BackClickedHandler;
      playerControl.ForwardClicked += ForwardClickedHandler;
      playerControl.RandomClicked += RandomClickedHandler;
      playerControl.RepeatClicked += RepeatClickedHandler;
      playerControl.StopClicked += StopClickedHandler;

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

      m_Updater = new Updater(new Uri("http://www.sakya.it/updater/updater.php"), "WpfMpdClient", "Windows");
      m_Updater.CheckCompletedDelegate += CheckCompleted;
      m_Updater.Check();

      lstArtist.ItemsSource = m_ArtistsSource;
      Type t = typeof(ListboxEntry);
      lstArtist.SearchProperty = t.GetProperty("Artist");

      lstAlbums.ItemsSource = m_AlbumsSource;
      lstAlbums.SearchProperty = t.GetProperty("Album");

      lstGenresAlbums.ItemsSource = m_GenresAlbumsSource;
      lstGenresAlbums.SearchProperty = t.GetProperty("Album");

      m_ArtDownloader.Start();
    }

    public int CurrentTrackId
    {
      get { return (int)GetValue(CurrentTrackIdProperty); }
      set { SetValue(CurrentTrackIdProperty, value); }
    }

    public static readonly DependencyProperty CurrentTrackIdProperty = DependencyProperty.Register(
        "CurrentTrackId", typeof(int), typeof(MainWindow), new PropertyMetadata(0, null));

    private void MpcIdleConnected(Mpc connection)
    {
      if (!string.IsNullOrEmpty(m_Settings.Password))
        m_MpcIdle.Password(m_Settings.Password);

      MpcIdleSubsystemsChanged(m_MpcIdle, Mpc.Subsystems.All);
      m_MpcIdle.Idle(Mpc.Subsystems.player | Mpc.Subsystems.playlist | Mpc.Subsystems.stored_playlist | Mpc.Subsystems.update |
                     Mpc.Subsystems.mixer | Mpc.Subsystems.options);
    }

    private void MpcIdleSubsystemsChanged(Mpc connection, Mpc.Subsystems subsystems)
    {
      if (!m_Mpc.Connected)
        return;

      MpdStatus status = m_Mpc.Status();

      if ((subsystems & Mpc.Subsystems.player) != 0 || (subsystems & Mpc.Subsystems.mixer) != 0 ||
          (subsystems & Mpc.Subsystems.options) != 0){
        Dispatcher.BeginInvoke(new Action(() =>
        {
          MenuItem m = m_NotifyIconMenu.Items[1] as MenuItem;
          m.Visibility = status.State != MpdState.Play ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
          m = m_NotifyIconMenu.Items[2] as MenuItem;
          m.Visibility = status.State == MpdState.Play ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;

          MpdFile file = m_Mpc.CurrentSong();
          playerControl.Update(status, file);          
          if (m_CurrentTrack == null || file == null || m_CurrentTrack.Id != file.Id) {
            TrackChanged(file);
            m_CurrentTrack = file;
            CurrentTrackId = file != null ? file.Id : 0;
            m_CurrentTrackStart = DateTime.Now;
          }
        }));
      }

      if ((subsystems & Mpc.Subsystems.playlist) != 0){
        Dispatcher.BeginInvoke(new Action(() =>
        {
          PopulatePlaylist();
        }));
      }

      if ((subsystems & Mpc.Subsystems.update) != 0){
        int lastUpdate = m_LastStatus != null ? m_LastStatus.UpdatingDb : -1;
        Dispatcher.BeginInvoke(new Action(() =>
        {
          btnUpdate.IsEnabled = status.UpdatingDb <= 0;
          // Update db finished:
          if (lastUpdate > 0 && status.UpdatingDb <= 0)
              UpdateDbFinished();
        }));
      }

      m_LastStatus = status;
    }

    private void MpcConnected(Mpc connection)
    {
      if (!string.IsNullOrEmpty(m_Settings.Password))
        m_Mpc.Password(m_Settings.Password);

      MpdStatistics stats = m_Mpc.Stats();      
      PopulateGenres();
      PopulatePlaylists();
      PopulateFileSystemTree();
      PopulatePlaylist();
      PopulateArtists();      
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
      if (!string.IsNullOrEmpty(m_Settings.ServerAddress)) {
        try {
          IPAddress[] addresses = System.Net.Dns.GetHostAddresses(m_Settings.ServerAddress);
          if (addresses.Length > 0) {
            IPAddress ip = addresses[0];
            IPEndPoint ie = new IPEndPoint(ip, m_Settings.ServerPort);

            if (m_Mpc.Connected)
              m_Mpc.Connection.Disconnect();
            m_Mpc.Connection = new MpcConnection(ie);
            if (m_MpcIdle.Connected)
              m_MpcIdle.Connection.Disconnect();
            m_MpcIdle.Connection = new MpcConnection(ie);
          }
        }
        catch (Exception ex) {
          MessageBox.Show(string.Format("Error connecting to server:\r\n{0}", ex.Message), "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
      }
    } // Connect

    private void PopulateArtists()
    {
      if (!m_Mpc.Connected)
        return;

      m_ArtistsSource.Clear();
      List<string> artists = m_Mpc.List(ScopeSpecifier.Artist);
      artists.Sort();
      for (int i = 0; i < artists.Count; i++) {
        if (string.IsNullOrEmpty(artists[i]))
          artists[i] = Mpc.NoArtist;
        ListboxEntry entry = new ListboxEntry() { Type = ListboxEntry.EntryType.Artist, 
                                                  Artist = artists[i] };
        m_ArtistsSource.Add(entry);
      }
      if (artists.Count > 0){
        lstArtist.SelectedIndex = 0;
        lstArtist.ScrollIntoView(m_ArtistsSource[0]);
      }
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
      if (genres.Count > 0){
        lstGenres.SelectedIndex = 0;
        lstGenres.ScrollIntoView(genres[0]);
      }
    }

    private void PopulatePlaylists()
    {
      if (!m_Mpc.Connected)
        return;

      List<string> playlists = m_Mpc.ListPlaylists();
      playlists.Sort();
      lstPlaylists.ItemsSource = playlists;
      if (playlists.Count > 0){
        lstPlaylists.SelectedIndex = 0;
        lstPlaylists.ScrollIntoView(playlists[0]);
      }
    }

    private void PopulateFileSystemTree()
    {
      if (!m_Mpc.Connected)
        return;

      treeFileSystem.Items.Clear();
      if (!m_Settings.ShowFilesystemTab)
        return;

      TreeViewItem root = new TreeViewItem();
      root.Header = "Root";
      root.Tag = null;
      treeFileSystem.Items.Add(root);

      PopulateFileSystemTree(root.Items, null);
      if (treeFileSystem.Items != null && treeFileSystem.Items.Count > 0) {
        TreeViewItem item = treeFileSystem.Items[0] as TreeViewItem;
        item.IsSelected = true;
        item.IsExpanded = true;
      }
    }

    private void PopulateFileSystemTree(ItemCollection items, string path)
    {
      items.Clear();
      MpdDirectoryListing list = m_Mpc.LsInfo(path);
      foreach (string dir in list.DirectoryList){
        TreeViewItem item = new TreeViewItem();
        item.Header = path != null ? dir.Remove(0, path.Length + 1) : dir;
        item.Tag = dir;
        if (HasSubdirectories(item.Tag.ToString())){
          item.Items.Add(null);
          item.Expanded += TreeItemExpanded;
        }
        items.Add(item);
      }
    }

    private bool HasSubdirectories(string path)
    {
      MpdDirectoryListing list = m_Mpc.LsInfo(path);
      return list.DirectoryList.Count > 0;      
    }

    private void TreeItemExpanded(object sender, RoutedEventArgs e)
    {
      TreeViewItem treeItem = sender as TreeViewItem;
      if (treeItem != null){
        if (treeItem.Items.Count == 1 && treeItem.Items[0] == null) {
          treeFileSystem.Cursor = Cursors.Wait;
          PopulateFileSystemTree(treeItem.Items, treeItem.Tag.ToString());
          treeFileSystem.Cursor = Cursors.Arrow;
        }
      }
    }

    private void treeFileSystem_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
      TreeViewItem treeItem = treeFileSystem.SelectedItem as TreeViewItem;
      if (treeItem != null){
        MpdDirectoryListing list = m_Mpc.LsInfo(treeItem.Tag != null ? treeItem.Tag.ToString() : null);
        m_Tracks = new List<MpdFile>();
        foreach (MpdFile file in list.FileList)
          m_Tracks.Add(file);
        lstTracks.ItemsSource = m_Tracks;
        ScrollTracksToLeft();
      }
    }

    private void lstArtist_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
      if (!m_Mpc.Connected)
        return;

      if (lstArtist.SelectedItem == null) {
        m_AlbumsSource.Clear();
        return;
      }

      m_AlbumsSource.Clear();
      string artist = SelectedArtist();
      List<string> albums = m_Mpc.List(ScopeSpecifier.Album, ScopeSpecifier.Artist, artist);
      albums.Sort();
      for (int i = 0; i < albums.Count; i++) {
        if (string.IsNullOrEmpty(albums[i]))
          albums[i] = Mpc.NoAlbum;
        ListboxEntry entry = new ListboxEntry() { Type = ListboxEntry.EntryType.Album, 
                                                  Artist = artist,
                                                  Album = albums[i] };
        m_AlbumsSource.Add(entry);
      }
      if (albums.Count > 0){
        lstAlbums.SelectedIndex = 0;
        lstAlbums.ScrollIntoView(m_AlbumsSource[0]);
      }
    }

    private void lstGenres_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
      if (!m_Mpc.Connected)
        return;

      if (lstGenres.SelectedItem == null) {
        m_GenresAlbumsSource.Clear();
        return;
      }

      m_GenresAlbumsSource.Clear();
      string genre = lstGenres.SelectedItem.ToString();
      if (genre == Mpc.NoGenre)
        genre = string.Empty;

      List<MpdFile> files = m_Mpc.Search(ScopeSpecifier.Genre, genre);
      files.Sort(delegate(MpdFile p1, MpdFile p2)
                 { 
                   return string.Compare(p2.Album, p1.Album);
                 });
      MpdFile lastFile = null;
      MpdFile last = files.Count > 0 ? files[files.Count - 1] : null;
      foreach (MpdFile file in files){
        if (lastFile != null && lastFile.Album != file.Album || file == last){
          string album = file == last ? file.Album : lastFile.Album;
          if (string.IsNullOrEmpty(album))
            album = Mpc.NoAlbum;
          ListboxEntry entry = new ListboxEntry()
          {
            Type = ListboxEntry.EntryType.Album,
            Artist = file == last ? file.Artist : lastFile.Artist,
            Album = album
          };
          m_GenresAlbumsSource.Add(entry);
        }
        lastFile = file;
      }

      if (m_GenresAlbumsSource.Count > 0) {
        lstGenresAlbums.SelectedIndex = 0;
        lstGenresAlbums.ScrollIntoView(m_GenresAlbumsSource[0]);
      }

      //List<string> albums = m_Mpc.List(ScopeSpecifier.Album, ScopeSpecifier.Genre, genre);
      //albums.Sort();
      //for (int i = 0; i < albums.Count; i++) {
      //  if (string.IsNullOrEmpty(albums[i]))
      //    albums[i] = Mpc.NoAlbum;
      //  ListboxEntry entry = new ListboxEntry()
      //  {
      //    Type = ListboxEntry.EntryType.Album,
      //    Artist = string.Empty,
      //    Album = albums[i]
      //  };
      //  m_GenresAlbumsSource.Add(entry);
      //}

      //if (albums.Count > 0) {
      //  lstGenresAlbums.SelectedIndex = 0;
      //  lstGenresAlbums.ScrollIntoView(m_GenresAlbumsSource[0]);
      //}
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
        ScrollTracksToLeft();
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
        Dictionary<ScopeSpecifier, string> search = new Dictionary<ScopeSpecifier, string>();

        ListBox listBox = null;
        if (tabBrowse.SelectedIndex == 0 && lstArtist.SelectedItem != null) {
          string artist = SelectedArtist();
          if (artist == Mpc.NoArtist)
            artist = string.Empty;
          search[ScopeSpecifier.Artist] = artist;
          listBox = lstAlbums;
        } else if (tabBrowse.SelectedIndex == 1 && lstGenres.SelectedItem != null) {
          string genre = lstGenres.SelectedItem.ToString();
          if (genre == Mpc.NoGenre)
            genre = string.Empty;
          search[ScopeSpecifier.Genre] = genre;
          listBox = lstGenresAlbums;
        }

        string album = SelectedAlbum(listBox);
        if (album == Mpc.NoAlbum)
          album = string.Empty;
        search[ScopeSpecifier.Album] = album;

        m_Tracks = m_Mpc.Find(search);
        lstTracks.ItemsSource = m_Tracks;
        ScrollTracksToLeft();
      } else {
        m_Tracks = null;
        lstTracks.ItemsSource = null;
      }
    }

    private void btnApplySettings_Click(object sender, RoutedEventArgs e)
    {
      m_Settings.ServerAddress = txtServerAddress.Text;
      int port = 0;
      if (int.TryParse(txtServerPort.Text, out port))
        m_Settings.ServerPort = port;
      else
        m_Settings.ServerPort = 6600;
      m_Settings.Password = txtPassword.Password;
      m_Settings.AutoReconnect = chkAutoreconnect.IsChecked == true;
      m_Settings.AutoReconnectDelay = 10;
      m_Settings.ShowStopButton = chkShowStop.IsChecked == true;
      m_Settings.ShowFilesystemTab = chkShowFilesystem.IsChecked == true;
      m_Settings.MinimizeToTray = chkMinimizeToTray.IsChecked == true;
      m_Settings.CloseToTray = chkCloseToTray.IsChecked == true;
      m_Settings.Scrobbler = chkScrobbler.IsChecked == true;

      m_Settings.Serialize(Settings.GetSettingsFileName());

      playerControl.ShowStopButton = m_Settings.ShowStopButton;
      tabFileSystem.Visibility = m_Settings.ShowFilesystemTab ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;

      if (m_Mpc.Connected)
        m_Mpc.Connection.Disconnect();
      if (m_MpcIdle.Connected)
        m_MpcIdle.Connection.Disconnect();
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

    private void StopClickedHandler(object sender)
    {
      if (m_Mpc.Connected)
        m_Mpc.Stop();
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
        if (Utilities.Confirm("Delete", string.Format("Delete playlist \"{0}\"?", playlist))){
          m_Mpc.Rm(playlist);
          PopulatePlaylists();
        }
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
      else if (tabBrowse.SelectedIndex == 3)
        treeFileSystem_SelectedItemChanged(null, null);
      else if (tabBrowse.SelectedIndex == 4)
        btnSearch_Click(null, null);
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

    public static void Stop()
    {
      if (This.m_Mpc.Connected) {
        switch (This.m_Mpc.Status().State) {
          case MpdState.Play:
          case MpdState.Pause:
            This.m_Mpc.Stop();
            break;
        }
      }
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
      }
    }

    private void btnSave_Click(object sender, RoutedEventArgs e)
    {
      if (m_Mpc.Connected){
        m_Mpc.Save(txtPlaylist.Text);
        txtPlaylist.Clear();
        PopulatePlaylists();
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
          m_StoredWindowState = WindowState;
          m_NotifyIcon.BalloonTipText = "WpfMpdClient has been minimized. Click the tray icon to show.";
          m_NotifyIcon.BalloonTipTitle = "WpfMpdClient";

          m_NotifyIcon.Visible = true;
          m_NotifyIcon.ShowBalloonTip(2000);
        }
        e.Cancel = true;
      }

      if (m_Close){
        if (IsVisible)
          m_Settings.WindowMaximized = WindowState == System.Windows.WindowState.Maximized;
        else
          m_Settings.WindowMaximized = m_StoredWindowState == System.Windows.WindowState.Maximized;
        m_Settings.WindowLeft = Left;
        m_Settings.WindowTop = Top;
        m_Settings.WindowWidth = ActualWidth;
        m_Settings.WindowHeight = ActualHeight;
        m_Settings.Serialize(Settings.GetSettingsFileName());

        m_LastfmScrobbler.SaveCache();
      }
    } // CloseHandler

    private void Window_StateChanged(object sender, EventArgs e)
    {
      if (WindowState == System.Windows.WindowState.Minimized && m_Settings.MinimizeToTray) {
        Hide();
        if (m_NotifyIcon != null) {
          m_NotifyIcon.BalloonTipText = "WpfMpdClient has been minimized. Click the tray icon to show.";
          m_NotifyIcon.BalloonTipTitle = "WpfMpdClient";
          m_NotifyIcon.Visible = true;
          m_NotifyIcon.ShowBalloonTip(2000);
        }
      } else {
        m_StoredWindowState = WindowState;
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

    private void UpdateDbFinished()
    {
      PopulateArtists();
      PopulateGenres();
      PopulatePlaylists();
      PopulatePlaylist();
    } // UpdateDbFinished

    private void TrackChanged(MpdFile track)
    {
      if (m_Settings.Scrobbler){
        if (m_CurrentTrack != null && m_CurrentTrack.Time >= 30){
          double played = (DateTime.Now - m_CurrentTrackStart).TotalSeconds;
          if (played >= 240 || played >= m_CurrentTrack.Time / 2) 
            m_LastfmScrobbler.Scrobble(m_CurrentTrack.Artist, m_CurrentTrack.Title, m_CurrentTrack.Album, m_CurrentTrackStart);
        }

        if (track != null){
          m_LastfmScrobbler.UpdateNowPlaying(track.Artist, track.Title, track.Album);
        }
      }

      System.Threading.ThreadPool.QueueUserWorkItem(new System.Threading.WaitCallback(GetLyrics), track);

      if (m_NotifyIcon != null && track != null) {
        string trackText = string.Format("\"{0}\"\r\n{1}", track.Title, track.Artist);
        if (trackText.Length > 64)
          m_NotifyIcon.Text = string.Format("{0}...", trackText.Substring(0, 59));
        else
          m_NotifyIcon.Text = trackText;

        if (m_NotifyIcon.Visible){
          m_NotifyIcon.BalloonTipText = string.Format("\"{0}\"\r\n{1}\r\n{2}", track.Title, track.Album, track.Artist);
          m_NotifyIcon.BalloonTipTitle = "WpfMpdClient";
          m_NotifyIcon.ShowBalloonTip(2000);
        }
      }
    } // TrackChanged

    public void Quit()
    {
      DiskImageCache.DeleteCacheFiles();
      m_NotifyIcon.Visible = false;
      m_Close = true;
      Application.Current.Shutdown();
    }

    private void mnuQuit_Click(object sender, RoutedEventArgs e)
    {
      Quit();
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
      MpdFile track = state as MpdFile;
      if (track == null)
        return;

      Dispatcher.BeginInvoke(new Action(() =>
      {
        txtLyrics.Text = track != null ? "Downloading lyrics" : string.Empty;
      }));

      if (m_CurrentTrack != null) {
        string lyrics = Utilities.GetLyrics(track.Artist, track.Title);
        if (string.IsNullOrEmpty(lyrics))
          lyrics = "No lyrics found";

        Dispatcher.BeginInvoke(new Action(() =>
        {
          txtLyrics.Text = lyrics;
          scrLyrics.ScrollToTop();
        }));
      }
    }

    private void btnSearch_Click(object sender, RoutedEventArgs e)
    {
      if (!m_Mpc.Connected)
        return;

      if (!string.IsNullOrEmpty(txtSearch.Text)){
        ScopeSpecifier searchBy = ScopeSpecifier.Title;
        switch (cmbSearch.SelectedIndex){
          case 0:
            searchBy = ScopeSpecifier.Artist;
            break;
          case 1:
            searchBy = ScopeSpecifier.Album;
            break;
          case 2:
            searchBy = ScopeSpecifier.Title;
            break;
        }
        m_Tracks = m_Mpc.Search(searchBy, txtSearch.Text);
        lstTracks.ItemsSource = m_Tracks;
        ScrollTracksToLeft();
      }else{
        m_Tracks = null;
        lstTracks.ItemsSource = null;
      }
    }

    private void btnSearchClear_Click(object sender, RoutedEventArgs e)
    {
      txtSearch.Text = string.Empty;
      cmbSearch.SelectedIndex = 0;
      m_Tracks = null;
      lstTracks.ItemsSource = null;
    }

    private void CheckCompleted(UpdaterApp app)
    {
      m_App = app;
      Dispatcher.BeginInvoke(new Action(() =>
      {
        btnCheckUpdates.IsEnabled = true;
        if (m_App != null && m_App.Version > Assembly.GetExecutingAssembly().GetName().Version) {
          UpdateConfirmWindow cdlg = new UpdateConfirmWindow(m_App);
          cdlg.Owner = this;
          if (cdlg.ShowDialog() == true){
            UpdateWindow dlg = new UpdateWindow(m_Updater, m_App);
            dlg.Owner = this;
            dlg.ShowDialog();
          }
        }
      }));
    }

    private void btnCheckUpdates_Click(object sender, RoutedEventArgs e)
    {
      btnCheckUpdates.IsEnabled = false;
      m_Updater.Check();
    }

    private void btnScrobblerAuthorize_Click(object sender, RoutedEventArgs e)
    {
      string token = m_LastfmScrobbler.GetToken();
      string url = m_LastfmScrobbler.GetAuthorizationUrl(token);

      BrowserWindow dlg = new BrowserWindow();
      dlg.Owner = this;
      dlg.NavigateTo(url);
      dlg.ShowDialog();

      m_Settings.ScrobblerSessionKey = Utilities.EncryptString(m_LastfmScrobbler.GetSession());
      btnApplySettings_Click(null, null);
    }

    private void txtServerPort_PreviewTextInput(object sender, TextCompositionEventArgs e)
    {
      string chars = "0123456789";
      char keyChar = e.Text.ToCharArray().First();
      if (chars.IndexOf(keyChar) == -1 && keyChar != 8)
        e.Handled = true;
    }

    private string SelectedArtist()
    {
      ListboxEntry entry = lstArtist.SelectedItem as ListboxEntry;
      if (entry != null) {
        if (entry.Artist == Mpc.NoArtist)
          return string.Empty;
        return entry.Artist;
      }
      return string.Empty;
    } // SelectedArtist

    private string SelectedAlbum(ListBox listbox)
    {
      if (listbox == null)
        return string.Empty;

      ListboxEntry entry = listbox.SelectedItem as ListboxEntry;
      if (entry != null) {
        if (entry.Artist == Mpc.NoAlbum)
          return string.Empty;
        return entry.Album;
      }
      return string.Empty;
    } // SelectedAlbum

    private void LisboxItem_Loaded(object sender, RoutedEventArgs e)
    {
      StackPanel stackPanel = sender as StackPanel;
      if (stackPanel != null){
        ListboxEntry entry = stackPanel.DataContext as ListboxEntry;
        if (entry != null) {
          m_ArtDownloader.Add(entry, 0);
        }
      }
    }

    private void ScrollTracksToLeft()
    {
      ScrollViewer listViewScrollViewer = Utilities.GetVisualChild<ScrollViewer>(lstTracks);
      listViewScrollViewer.ScrollToLeftEnd();
    }
  }
}
