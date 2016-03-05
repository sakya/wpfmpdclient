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
using WPF.JoshSmith.ServiceProviders.UI;
using System.Threading;
using System.Threading.Tasks;

namespace WpfMpdClient
{
  public partial class MainWindow : Window
  {
    public class MpdChannel : INotifyPropertyChanged
    {
      public event PropertyChangedEventHandler PropertyChanged;

      private string m_Name = string.Empty;
      private bool m_Subscribed = false;

      public string Name
      {
        get { return m_Name; }
        set
        {
          m_Name = value;
          OnPropertyChanged("Name");
        }
      }

      public bool Subscribed
      {
        get { return m_Subscribed; }
        set
        {
          m_Subscribed = value;
          OnPropertyChanged("Subscribed");
        }
      }

      protected void OnPropertyChanged(string name)
      {
        PropertyChangedEventHandler handler = PropertyChanged;
        if (handler != null) {
          handler(this, new PropertyChangedEventArgs(name));
        }
      }
    }

    static MainWindow This = null;

    #region Private members
    bool m_Connecting = false;
    LastfmScrobbler m_LastfmScrobbler = null;
    Updater m_Updater = null;
    UpdaterApp m_App = null;
    Settings m_Settings = null;
    Mpc m_Mpc = null;
    Mpc m_MpcIdle = null;
    MpdStatus m_LastStatus = null;
    System.Timers.Timer m_StartTimer = null;
    System.Timers.Timer m_ReconnectTimer = null;
    List<MpdFile> m_Tracks = null;
    MpdFile m_CurrentTrack = null;
    DateTime m_CurrentTrackStart = DateTime.MinValue;
    About m_About = new About();
    System.Windows.Forms.NotifyIcon m_NotifyIcon = null;
    ContextMenu m_NotifyIconMenu = null;
    WindowState m_StoredWindowState = WindowState.Normal;
    bool m_Close = false;
    bool m_IgnoreDisconnect = false;
    bool m_MpdConnectedOnce = false;
    ListViewDragDropManager<MpdFile> m_DragDropManager = null;
    MiniPlayerWindow m_MiniPlayer = null;
    List<string> m_Languages = new List<string>() { string.Empty, "fr", "de", "it", "jp", "pl", "pt", "ru", "es", "sv", "tr" };

    ArtDownloader m_ArtDownloader = new ArtDownloader();
    ObservableCollection<ListboxEntry> m_ArtistsSource = new ObservableCollection<ListboxEntry>();
    ObservableCollection<ListboxEntry> m_AlbumsSource = new ObservableCollection<ListboxEntry>();
    ObservableCollection<ListboxEntry> m_GenresAlbumsSource = new ObservableCollection<ListboxEntry>();
    ObservableCollection<MpdFile> m_PlaylistTracks = new ObservableCollection<MpdFile>();
    ObservableCollection<MpdMessage> m_Messages = new ObservableCollection<MpdMessage>();
    ObservableCollection<MpdChannel> m_Channels = new ObservableCollection<MpdChannel>();
    List<Expander> m_MessagesExpanders = new List<Expander>();
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
        chkShowMiniPlayer.IsChecked = m_Settings.ShowMiniPlayer;
        chkScrobbler.IsChecked = m_Settings.Scrobbler;
        cmbLastFmLang.SelectedIndex = m_Languages.IndexOf(m_Settings.InfoLanguage);
        if (cmbLastFmLang.SelectedIndex == -1)
          cmbLastFmLang.SelectedIndex = 0;
        cmbPlaylistStyle.SelectedIndex = m_Settings.StyledPlaylist ? 1 : 0;

        chkTray_Changed(null, null);

        lstTracks.SetColumnsInfo(m_Settings.TracksListView);
        lstPlaylist.SetColumnsInfo(m_Settings.PlayListView);
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
      lstPlaylist.Visibility = m_Settings.StyledPlaylist ? System.Windows.Visibility.Collapsed : System.Windows.Visibility.Visible;
      lstPlaylistStyled.Visibility = m_Settings.StyledPlaylist ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;

      lstTracks.Visibility = m_Settings.StyledPlaylist ? System.Windows.Visibility.Collapsed : System.Windows.Visibility.Visible;
      lstTracksStyled.Visibility = m_Settings.StyledPlaylist ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;


      m_NotifyIcon = new System.Windows.Forms.NotifyIcon();
      m_NotifyIcon.Icon = new System.Drawing.Icon("mpd_icon.ico", new System.Drawing.Size(32,32));
      m_NotifyIcon.MouseDown += new System.Windows.Forms.MouseEventHandler(NotifyIcon_MouseDown);
      m_NotifyIconMenu = (ContextMenu)this.FindResource("TrayIconContextMenu");
      Closing += CloseHandler;

      if (!string.IsNullOrEmpty(m_Settings.ServerAddress)){
        m_StartTimer = new System.Timers.Timer();
        m_StartTimer.Interval = 500;
        m_StartTimer.Elapsed += StartTimerHandler;
        m_StartTimer.Start();
      }

      m_Updater = new Updater(new Uri("http://www.sakya.it/updater/updater.php"), "WpfMpdClient", "Windows");
      m_Updater.AppCurrentVersion = Assembly.GetExecutingAssembly().GetName().Version.ToString();
      m_Updater.CheckCompletedDelegate += CheckCompleted;
      m_Updater.Check();

      lstArtist.ItemsSource = m_ArtistsSource;
      Type t = typeof(ListboxEntry);
      lstArtist.SearchProperty = t.GetProperty("Artist");

      lstAlbums.ItemsSource = m_AlbumsSource;
      lstAlbums.SearchProperty = t.GetProperty("Album");

      lstGenresAlbums.ItemsSource = m_GenresAlbumsSource;
      lstGenresAlbums.SearchProperty = t.GetProperty("Album");

      lstPlaylist.ItemsSource = m_PlaylistTracks;
      lstPlaylistStyled.ItemsSource = m_PlaylistTracks;
      m_DragDropManager = new ListViewDragDropManager<MpdFile>(m_Settings.StyledPlaylist ? lstPlaylistStyled : lstPlaylist);
      m_DragDropManager.ProcessDrop += dragMgr_ProcessDrop;

      lstChannels.ItemsSource = m_Channels;
      cmbChannnels.ItemsSource = m_Channels;
      lstMessages.ItemsSource = m_Messages;
      CollectionView view = (CollectionView)CollectionViewSource.GetDefaultView(lstMessages.ItemsSource);
      PropertyGroupDescription group = new PropertyGroupDescription("Channel");
      view.GroupDescriptions.Add(group);

      m_ArtDownloader.Start();      
      txtStatus.Text = "Not connected";
    }

    public int CurrentTrackId
    {
      get { return (int)GetValue(CurrentTrackIdProperty); }
      set { SetValue(CurrentTrackIdProperty, value); }
    }

    public static readonly DependencyProperty CurrentTrackIdProperty = DependencyProperty.Register(
        "CurrentTrackId", typeof(int), typeof(MainWindow), new PropertyMetadata(0, null));

    private bool CheckMpdConnection()
    {
      if (m_Mpc != null && m_Mpc.Connected)
        return true;

      // Reconnect:
      if (m_MpdConnectedOnce && !string.IsNullOrEmpty(m_Settings.ServerAddress) && !m_Connecting) {
        txtStatus.Text = "Not connected";
        if (!m_IgnoreDisconnect && m_Settings.AutoReconnect && m_ReconnectTimer == null) {
          m_ReconnectTimer = new System.Timers.Timer();
          m_ReconnectTimer.Interval = m_Settings.AutoReconnectDelay * 1000;
          m_ReconnectTimer.Elapsed += ReconnectTimerHandler;
          m_ReconnectTimer.Start();
        }
      }
      return false;
    } // CheckMpdConnection

    private void MpcIdleConnected(Mpc connection)
    {
      if (!string.IsNullOrEmpty(m_Settings.Password)){
        if (!m_MpcIdle.Password(m_Settings.Password))
          return;
      }

      MpcIdleSubsystemsChanged(m_MpcIdle, Mpc.Subsystems.All);

      Mpc.Subsystems subsystems = Mpc.Subsystems.player | Mpc.Subsystems.playlist | Mpc.Subsystems.stored_playlist | Mpc.Subsystems.update |
                                  Mpc.Subsystems.mixer | Mpc.Subsystems.options;
      if (m_Mpc.Commands().Contains("channels"))
        subsystems |= Mpc.Subsystems.message | Mpc.Subsystems.subscription;
      m_MpcIdle.Idle(subsystems);
    }

    private async void MpcIdleSubsystemsChanged(Mpc connection, Mpc.Subsystems subsystems)
    {
      if (!CheckMpdConnection())
        return;

      MpdStatus status = null;
      try{
        status = m_Mpc.Status();
      }catch{
        return;
      }
      if ((subsystems & Mpc.Subsystems.player) != 0 || (subsystems & Mpc.Subsystems.mixer) != 0 ||
          (subsystems & Mpc.Subsystems.options) != 0){
        await Dispatcher.BeginInvoke(new Action(() =>
        {
          MenuItem m = m_NotifyIconMenu.Items[1] as MenuItem;
          m.Visibility = status.State != MpdState.Play ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
          m = m_NotifyIconMenu.Items[2] as MenuItem;
          m.Visibility = status.State == MpdState.Play ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;

          MpdFile file = m_Mpc.CurrentSong();
          playerControl.Update(status, file);          
          if (m_MiniPlayer != null)
            m_MiniPlayer.Update(status, file);

          if (m_CurrentTrack == null || file == null || m_CurrentTrack.Id != file.Id) {
            TrackChanged(file);
            m_CurrentTrack = file;
            CurrentTrackId = file != null ? file.Id : 0;
            m_CurrentTrackStart = DateTime.Now;
          }
        }));
      }

      if ((subsystems & Mpc.Subsystems.playlist) != 0){        
        await PopulatePlaylist();
      }

      if ((subsystems & Mpc.Subsystems.update) != 0){
        int lastUpdate = m_LastStatus != null ? m_LastStatus.UpdatingDb : -1;
        await Dispatcher.BeginInvoke(new Action(() =>
        {
          btnUpdate.IsEnabled = status.UpdatingDb <= 0;
          // Update db finished:
          if (lastUpdate > 0 && status.UpdatingDb <= 0)
              UpdateDbFinished();
        }));
      }

      //if ((subsystems & Mpc.Subsystems.subscription) != 0)
      //  PopulateChannels();
      //if ((subsystems & Mpc.Subsystems.message) != 0)
      //  PopulateMessages();

      m_LastStatus = status;
    }

    private async void MpcConnected(Mpc connection)
    {
        if (!string.IsNullOrEmpty(m_Settings.Password)) {
          if (!m_Mpc.Password(m_Settings.Password)) {
            await Dispatcher.BeginInvoke(new Action(() =>
            {
              MessageBox.Show("Error connecting to server:\r\nWrong password", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }));
            return;
          }
        }

        List<string> commands = m_Mpc.Commands();
        await Dispatcher.BeginInvoke(new Action(() =>
        {
          if (!commands.Contains("channels"))
            tabMessages.Visibility = System.Windows.Visibility.Collapsed;

          txtStatus.Text = string.Format("Connected to {0}:{1} [MPD v.{2}]", m_Settings.ServerAddress, m_Settings.ServerPort, m_Mpc.Connection.Version);
        }));

        MpdStatistics stats = m_Mpc.Stats();
        await PopulateGenres();
        await PopulatePlaylists();
        await PopulateFileSystemTree();
        await PopulatePlaylist();
        await PopulateArtists();
      //}));
    }

    private void MpcDisconnected(Mpc connection)
    {
      if (!connection.Connected){
        txtStatus.Text = "Not connected";
        if (!m_IgnoreDisconnect  && m_Settings.AutoReconnect && m_ReconnectTimer == null){
          m_ReconnectTimer = new System.Timers.Timer();
          m_ReconnectTimer.Interval = m_Settings.AutoReconnectDelay * 1000;
          m_ReconnectTimer.Elapsed += ReconnectTimerHandler;
          m_ReconnectTimer.Start();
        }
      }
    }

    private void Connect()
    {
      if (!string.IsNullOrEmpty(m_Settings.ServerAddress) && !m_Connecting) {
        m_Connecting = true;
        txtStatus.Text = string.Format("Connecting to {0}:{1}...", m_Settings.ServerAddress, m_Settings.ServerPort);
        try {
          IPAddress[] addresses = System.Net.Dns.GetHostAddresses(m_Settings.ServerAddress);
          if (addresses.Length > 0) {
            IPAddress ip = addresses[0];
            IPEndPoint ie = new IPEndPoint(ip, m_Settings.ServerPort);

            m_IgnoreDisconnect = true;
            if (m_Mpc.Connected)
              m_Mpc.Connection.Disconnect();
            m_Mpc.Connection = new MpcConnection();
            m_Mpc.Connection.Server = ie;
            m_MpcIdle.Connection = new MpcConnection();
            m_MpcIdle.Connection.Server = ie;

            ThreadPool.QueueUserWorkItem( (o) => 
            {
              try {
                m_Mpc.Connection.Connect();
                if (m_MpcIdle.Connected)
                  m_MpcIdle.Connection.Disconnect();
                m_MpcIdle.Connection.Connect();
                m_IgnoreDisconnect = false;
                m_MpdConnectedOnce = true;
              } catch (Exception ex) {
                Dispatcher.BeginInvoke(new Action(() =>
                {
                  txtStatus.Text = string.Empty;
                  MessageBox.Show(string.Format("Error connecting to server:\r\n{0}\r\n{1}", ex.Message, ex.StackTrace), "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }));
              } finally {
                m_Connecting = false;
              }
            });

          }
        }
        catch (Exception ex) {
          m_Connecting = false;
          txtStatus.Text = string.Empty;
          MessageBox.Show(string.Format("Error connecting to server:\r\n{0}\r\n{1}", ex.Message, ex.StackTrace), "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
      }
    } // Connect

    private async Task<bool> PopulateArtists()
    {
      if (!CheckMpdConnection())
        return false;


      await Dispatcher.BeginInvoke(new Action(() =>
      {
        m_ArtistsSource.Clear();
      }));

      List<string> artists = null;
      try{
        artists = await Task.Factory.StartNew(() => m_Mpc.List(ScopeSpecifier.Artist));
      }catch (Exception ex){
        ShowException(ex);
        return false;
      }

      artists.Sort();
      for (int i = 0; i < artists.Count; i++) {
        if (string.IsNullOrEmpty(artists[i]))
          artists[i] = Mpc.NoArtist;
        ListboxEntry entry = new ListboxEntry() { Type = ListboxEntry.EntryType.Artist, 
                                                  Artist = artists[i] };

        await Dispatcher.BeginInvoke(new Action(() =>
        {
          m_ArtistsSource.Add(entry);
        }));
      }

      await Dispatcher.BeginInvoke(new Action(() =>
      {
        if (artists.Count > 0) {
          lstArtist.SelectedIndex = 0;
          lstArtist.ScrollIntoView(m_ArtistsSource[0]);
        }
      }));
      return true;
    }

    private async Task<bool> PopulateGenres()
    {
      if (!CheckMpdConnection())
        return false;

      List<string> genres = null;
      try{
        genres = await Task.Factory.StartNew(() => m_Mpc.List(ScopeSpecifier.Genre));
      }catch (Exception ex){
        ShowException(ex);
        return false;
      }

      genres.Sort();
      for (int i = 0; i < genres.Count; i++) {
        if (string.IsNullOrEmpty(genres[i]))
          genres[i] = Mpc.NoGenre;
      }

      await Dispatcher.BeginInvoke(new Action(() =>
      {

        lstGenres.ItemsSource = genres;
        if (genres.Count > 0) {
          lstGenres.SelectedIndex = 0;
          lstGenres.ScrollIntoView(genres[0]);
        }
      }));

      return true;
    }

    private async Task<bool> PopulatePlaylists()
    {
      if (!CheckMpdConnection())
        return false;

      List<string> playlists = null;
      try{
        playlists = await Task.Factory.StartNew(() => m_Mpc.ListPlaylists());
      }catch (Exception ex){
        ShowException(ex);
        return false;
      }
      playlists.Sort();

      await Dispatcher.BeginInvoke(new Action(() =>
      {
        lstPlaylists.ItemsSource = playlists;
        if (playlists.Count > 0) {
          lstPlaylists.SelectedIndex = 0;
          lstPlaylists.ScrollIntoView(playlists[0]);
        }
      }));
      return true;
    }

    private async Task<bool> PopulateFileSystemTree()
    {
      if (!CheckMpdConnection())
        return false;

      await Dispatcher.BeginInvoke(new Action(() =>
      {
        treeFileSystem.Items.Clear();
      }));

      if (!m_Settings.ShowFilesystemTab)
        return false;

      TreeViewItem root = null;
      await Dispatcher.BeginInvoke(new Action(async () =>
      {
        root = new TreeViewItem();
        root.Header = "Root";
        root.Tag = null;
        treeFileSystem.Items.Add(root);
        await PopulateFileSystemTree(root.Items, null);
        if (treeFileSystem.Items != null && treeFileSystem.Items.Count > 0) {
          TreeViewItem item = treeFileSystem.Items[0] as TreeViewItem;
          item.IsSelected = true;
          item.IsExpanded = true;
        }
      }));



      return true;
    }

    private async Task<bool> PopulateFileSystemTree(ItemCollection items, string path)
    {
      items.Clear();
      MpdDirectoryListing list = null;
      try{
        list = await Task.Factory.StartNew(() => m_Mpc.LsInfo(path));
      }catch (Exception ex){
        ShowException(ex);
        return false;
      }

      await Dispatcher.BeginInvoke(new Action( async () =>
      {
        foreach (string dir in list.DirectoryList) {
          TreeViewItem item = new TreeViewItem();
          item.Header = path != null ? dir.Remove(0, path.Length + 1) : dir;
          item.Tag = dir;
          if (await HasSubdirectories(item.Tag.ToString())) {
            item.Items.Add(null);
            item.Expanded += TreeItemExpanded;
          }
          items.Add(item);
        }
      }));
      return true;
    }

    private async Task<bool> HasSubdirectories(string path)
    {
      try {
        MpdDirectoryListing list = await Task.Factory.StartNew(() => m_Mpc.LsInfo(path));
        return list.DirectoryList.Count > 0;
      }catch (MpdResponseException) {
        return false;
      }
    }

    private async void TreeItemExpanded(object sender, RoutedEventArgs e)
    {
      TreeViewItem treeItem = sender as TreeViewItem;
      if (treeItem != null){
        if (treeItem.Items.Count == 1 && treeItem.Items[0] == null) {
          treeFileSystem.Cursor = Cursors.Wait;
          await PopulateFileSystemTree(treeItem.Items, treeItem.Tag.ToString());
          treeFileSystem.Cursor = Cursors.Arrow;
        }
      }
    }

    private async void treeFileSystem_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
      TreeViewItem treeItem = treeFileSystem.SelectedItem as TreeViewItem;
      if (treeItem != null){
        lstTracks.ItemsSource = null;
        lstTracksStyled.ItemsSource = null;
        MpdDirectoryListing list = null;
        try{
          string tag = treeItem.Tag != null ? treeItem.Tag.ToString() : null;
          list = await Task.Factory.StartNew(() => (m_Mpc.LsInfo(tag)));
        }catch (Exception ex){
          ShowException(ex);
          return;
        }
        m_Tracks = new List<MpdFile>();
        foreach (MpdFile file in list.FileList)
          m_Tracks.Add(file);
        lstTracks.ItemsSource = m_Tracks;
        lstTracksStyled.ItemsSource = m_Tracks;
        ScrollTracksToLeft();
      }
    }

    private async void lstArtist_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
      if (!CheckMpdConnection())
        return;

      if (lstArtist.SelectedItem == null) {
        m_AlbumsSource.Clear();
        return;
      }

      m_AlbumsSource.Clear();
      string artist = SelectedArtist();
      List<string> albums = null;
      try{
        albums = await Task.Factory.StartNew(() => m_Mpc.List(ScopeSpecifier.Album, ScopeSpecifier.Artist, artist));
      }catch (Exception ex){
        ShowException(ex);
        return;
      }
      albums.Sort();
      m_AlbumsSource.Clear();
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

    private async void lstGenres_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
      if (!CheckMpdConnection())
        return;

      if (lstGenres.SelectedItem == null) {
        m_GenresAlbumsSource.Clear();
        return;
      }

      m_GenresAlbumsSource.Clear();
      string genre = lstGenres.SelectedItem.ToString();
      if (genre == Mpc.NoGenre)
        genre = string.Empty;

      List<MpdFile> files = null;
      try{
        files = await Task.Factory.StartNew(() => m_Mpc.Find(ScopeSpecifier.Genre, genre));
      }catch (Exception ex){
        ShowException(ex);
        return;
      }
      files.Sort(delegate(MpdFile p1, MpdFile p2)
                 { 
                   return string.Compare(p1.Album, p2.Album);
                 });
      MpdFile lastFile = null;
      MpdFile last = files.Count > 0 ? files[files.Count - 1] : null;
      m_GenresAlbumsSource.Clear();
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
    }

    private async void lstPlaylists_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
      if (!CheckMpdConnection())
        return;

      ListBox list = sender as ListBox;
      if (list.SelectedItem != null) {
        string playlist = list.SelectedItem.ToString();
        lstTracks.ItemsSource = null;
        lstTracksStyled.ItemsSource = null;

        try {
          m_Tracks = await Task.Factory.StartNew(() => m_Mpc.ListPlaylistInfo(playlist));
        }catch (Exception ex){
          ShowException(ex);
          return;
        }
        lstTracks.ItemsSource = m_Tracks;
        lstTracksStyled.ItemsSource = m_Tracks;
        ScrollTracksToLeft();
      } else {
        m_Tracks = null;
        lstTracks.ItemsSource = null;
        lstTracksStyled.ItemsSource = null;
      }
    } 

    private void lstTracks_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {

    }

    private async void lstAlbums_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
      if (!CheckMpdConnection())
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

        lstTracks.ItemsSource = null;
        lstTracksStyled.ItemsSource = null;

        string album = SelectedAlbum(listBox);
        if (album == Mpc.NoAlbum)
          album = string.Empty;
        search[ScopeSpecifier.Album] = album;

        try{
          m_Tracks = await Task.Factory.StartNew(() => m_Mpc.Find(search));
        }catch (Exception ex){
          ShowException(ex);
          return;
        }
        lstTracks.ItemsSource = m_Tracks;
        lstTracksStyled.ItemsSource = m_Tracks;
        ScrollTracksToLeft();
      } else {
        m_Tracks = null;
        lstTracks.ItemsSource = null;
        lstTracksStyled.ItemsSource = null;
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
      m_Settings.ShowMiniPlayer = chkShowMiniPlayer.IsChecked == true;
      m_Settings.Scrobbler = chkScrobbler.IsChecked == true;
      m_Settings.InfoLanguage = cmbLastFmLang.SelectedIndex < 0 ? m_Languages[0] : m_Languages[cmbLastFmLang.SelectedIndex];
      m_Settings.StyledPlaylist = cmbPlaylistStyle.SelectedIndex == 1;

      m_Settings.Serialize(Settings.GetSettingsFileName());

      playerControl.ShowStopButton = m_Settings.ShowStopButton;
      tabFileSystem.Visibility = m_Settings.ShowFilesystemTab ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;

      lstPlaylist.Visibility = m_Settings.StyledPlaylist ? System.Windows.Visibility.Collapsed : System.Windows.Visibility.Visible;
      lstPlaylistStyled.Visibility = m_Settings.StyledPlaylist ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;

      lstTracks.Visibility = m_Settings.StyledPlaylist ? System.Windows.Visibility.Collapsed : System.Windows.Visibility.Visible;
      lstTracksStyled.Visibility = m_Settings.StyledPlaylist ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;

      m_DragDropManager.Selector = m_Settings.StyledPlaylist ? lstPlaylistStyled : lstPlaylist;
      m_DragDropManager.ProcessDrop += dragMgr_ProcessDrop;

      m_IgnoreDisconnect = true;
      if (m_Mpc.Connected)
        m_Mpc.Connection.Disconnect();
      if (m_MpcIdle.Connected)
        m_MpcIdle.Connection.Disconnect();
      Connect();
      tabBrowse.SelectedIndex = 0;
      m_IgnoreDisconnect = false;
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
      if (m_StartTimer != null)
        m_StartTimer.Stop();
      m_StartTimer = null;
      Dispatcher.BeginInvoke(new Action(() =>
      {
        Connect();
      }));
    } // StartTimerHandler

    private async Task<bool> PopulatePlaylist()
    {
      if (!CheckMpdConnection())
        return false;

      List<MpdFile> tracks = null;
      try{
        tracks = await Task.Factory.StartNew(() => m_Mpc.PlaylistInfo());
      }catch (Exception ex){
        ShowException(ex);
        return false;
      }

      await Dispatcher.BeginInvoke(new Action(() =>
      {
        m_PlaylistTracks.Clear();
        foreach (MpdFile file in tracks)
          m_PlaylistTracks.Add(file);
      }));

      return true;
    }

    private async void ContextMenu_Click(object sender, RoutedEventArgs args)
    {
      if (!CheckMpdConnection())
        return;

      MenuItem item = sender as MenuItem;
      if (item.Name == "mnuDeletePlaylist") {
        string playlist = lstPlaylists.SelectedItem.ToString();
        if (Utilities.Confirm("Delete", string.Format("Delete playlist \"{0}\"?", playlist))){
          try{
            await Task.Factory.StartNew(() => m_Mpc.Rm(playlist));
            await PopulatePlaylists();
          }catch (Exception ex){
            ShowException(ex);
            return;
          }
        }
        return;
      }

      bool scroll = false;
      if (item.Name == "mnuAddReplace" || item.Name == "mnuAddReplacePlay"){
        scroll = true;
        try{
          await Task.Factory.StartNew(() => m_Mpc.Clear());
        }catch (Exception ex){
          ShowException(ex);
          return;
        }
      }

      if (m_Tracks != null){
        foreach (MpdFile f in m_Tracks){
          try{
            await Task.Factory.StartNew(() => m_Mpc.Add(f.File));
          }catch (Exception){}
        }
        if (scroll && lstPlaylist.Items.Count > 0)
          lstPlaylist.ScrollIntoView(lstPlaylist.Items[0]);
      }        
      if (item.Name == "mnuAddReplacePlay"){
        try{
          await Task.Factory.StartNew(() => m_Mpc.Play());
        }catch (Exception ex){
          ShowException(ex);
          return;
        }
      }        
    }

    private async void TracksContextMenu_Click(object sender, RoutedEventArgs args)
    {
      if (!CheckMpdConnection())
        return;

      bool scroll = false;
      MenuItem mnuItem = sender as MenuItem;
      if (mnuItem.Name == "mnuAddReplace" || mnuItem.Name == "mnuAddReplacePlay"){
        scroll = true;
        try{
          await Task.Factory.StartNew(() => m_Mpc.Clear());
        }catch (Exception ex){
          ShowException(ex);
          return;
        }
      }

      foreach (MpdFile file  in m_Settings.StyledPlaylist ? lstTracksStyled.SelectedItems : lstTracks.SelectedItems)
        await Task.Factory.StartNew(() => m_Mpc.Add(file.File));

      if (scroll && lstPlaylist.Items.Count > 0)
        lstPlaylist.ScrollIntoView(lstPlaylist.Items[0]);

      if (mnuItem.Name == "mnuAddReplacePlay")
        await Task.Factory.StartNew(() => m_Mpc.Play());
    }


    private void btnUpdate_Click(object sender, RoutedEventArgs e)
    {
      if (m_Mpc.Connected){
        try{
          m_Mpc.Update();
        }catch (Exception ex){
          ShowException(ex);
          return;
        }
      }
    }

    private async void tabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
      if (!CheckMpdConnection())
        return;

      if (e.AddedItems.Count > 0) {
        TabItem tab = e.AddedItems[0] as TabItem;
        if (tab == null)
          return;
      } else
        return;

      if (tabControl.SelectedIndex == 1){

      }else if (tabControl.SelectedIndex == 2){
        await Dispatcher.BeginInvoke(new Action( async() =>
        {
          StringBuilder sb = new StringBuilder();
          sb.AppendLine(await Task.Factory.StartNew(() => m_Mpc.Stats().ToString()));
          sb.AppendLine(await Task.Factory.StartNew(() => m_Mpc.Status().ToString()));
          txtServerStatus.Text = sb.ToString();
        }));
      } else if (tabControl.SelectedIndex == 3) {
        //PopulateChannels();
        //PopulateMessages();
      }
    }

    private void tabBrowse_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
      if (!CheckMpdConnection())
        return;

      if (e.AddedItems.Count > 0) {
        TabItem tab = e.AddedItems[0] as TabItem;
        if (tab == null)
          return;
      } else
        return;

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

    private void lstPlaylist_Selected(object sender, RoutedEventArgs e)
    {
      ListBoxItem item = sender as ListBoxItem;
      if (item != null)
        item.IsSelected = false;
    }

    private void lstPlaylistContextMenu_Click(object sender, RoutedEventArgs args)
    {
      if (!CheckMpdConnection())
        return;

      MenuItem item = sender as MenuItem;
      if (item.Name == "mnuRemove"){
        MpdFile file = item.DataContext as MpdFile;
        if (file != null) {
          m_Mpc.Delete(file.Pos);
        }
      }        
    }

    private async void lstPlaylist_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
      if (!CheckMpdConnection())
        return;

      ListBoxItem item = sender as ListBoxItem;
      if (item != null) {
        MpdFile file = item.DataContext as MpdFile;
        if (file != null) {
          try{
            await Task.Factory.StartNew(() => m_Mpc.Play(file.Pos));
          }catch (Exception ex){
            ShowException(ex);
            return;
          }
        }
      }
    }

    private void hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
    {
      m_About.hyperlink_RequestNavigate(sender, e);
    }

    public static string MpdVersion()
    {
      if (This != null && This.m_Mpc != null && This.m_Mpc.Connected)
        return This.m_Mpc.Connection.Version;
      return string.Empty;
    }

    public static void Stop()
    {
      if (This.m_Mpc.Connected) {
        switch (This.m_Mpc.Status().State) {
          case MpdState.Play:
          case MpdState.Pause:
            try{
              This.m_Mpc.Stop();
            }catch (Exception ex){
              This.ShowException(ex);
              return;
            }
            break;
        }
      }
    }

    public static void PlayPause()
    {
      if (This.m_Mpc.Connected){
        switch (This.m_Mpc.Status().State){
          case MpdState.Play:
            This.mnuPause_Click(null, null);
            break;
          case MpdState.Pause:
          case MpdState.Stop:
            This.mnuPlay_Click(null, null);
            break;
        }
      }
    }

    public static void NextTrack()
    {
      This.mnuNext_Click(null, null);
    }

    public static void PreviousTrack()
    {
      This.mnuPrevious_Click(null, null);
    }

    private async void btnClear_Click(object sender, RoutedEventArgs e)
    {
      if (m_Mpc.Connected){
        try{
          await Task.Factory.StartNew(() => m_Mpc.Clear());
        }catch (Exception ex){
          ShowException(ex);
          return;
        }
      }
    }

    private async void btnSave_Click(object sender, RoutedEventArgs e)
    {
      if (m_Mpc.Connected){
        try{
          string name = txtPlaylist.Text;
          await Task.Factory.StartNew(() => m_Mpc.Save(name));
        }catch (Exception ex){
          ShowException(ex);
          return;
        }
        txtPlaylist.Clear();
        await PopulatePlaylists();
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

          if (m_Settings.ShowMiniPlayer){
            if (m_MiniPlayer == null){
              m_MiniPlayer = new MiniPlayerWindow(m_Mpc, m_Settings);
              if (m_Settings.MiniWindowLeft >= 0 && m_Settings.MiniWindowTop >= 0){
                m_MiniPlayer.Left = m_Settings.MiniWindowLeft;
                m_MiniPlayer.Top = m_Settings.MiniWindowTop;
              }
              m_MiniPlayer.Update(m_LastStatus, m_CurrentTrack);
            }
            m_MiniPlayer.Show();
          }
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
        m_Settings.TracksListView = lstTracks.GetColumnsInfo();
        m_Settings.PlayListView = lstPlaylist.GetColumnsInfo();

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
        if (m_MiniPlayer != null){
          m_MiniPlayer.Close();
          m_MiniPlayer = null;
        }
        Show();
        WindowState = m_StoredWindowState;
        Activate();
        Focus();
        m_NotifyIcon.Visible = false;
      }
    } // NotifyIcon_MouseDown

    private async void UpdateDbFinished()
    {
      await PopulateArtists();
      await PopulateGenres();
      await PopulatePlaylists();
      await PopulatePlaylist();
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
      if (track != null && (m_CurrentTrack == null || m_CurrentTrack.Artist != track.Artist))
        System.Threading.ThreadPool.QueueUserWorkItem(new System.Threading.WaitCallback(GetArtistInfo), track.Artist);
      else if (track == null)
        System.Threading.ThreadPool.QueueUserWorkItem(new System.Threading.WaitCallback(GetArtistInfo), string.Empty);

      if (track != null && (m_CurrentTrack == null || m_CurrentTrack.Artist != track.Artist || m_CurrentTrack.Album != track.Album))
        System.Threading.ThreadPool.QueueUserWorkItem(new System.Threading.WaitCallback(GetAlbumInfo), new List<string>() { track.Artist, track.Album});
      else if (track == null)
        System.Threading.ThreadPool.QueueUserWorkItem(new System.Threading.WaitCallback(GetAlbumInfo), new List<string>() { string.Empty, string.Empty});


      if (m_NotifyIcon != null && track != null && (m_MiniPlayer == null || !m_MiniPlayer.IsVisible)) {
        string trackText = string.Format("\"{0}\"\r\n{1}", track.Title, track.Artist);
        if (trackText.Length >= 64)
          m_NotifyIcon.Text = string.Format("{0}...", trackText.Substring(0, 60));
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
      m_NotifyIcon.Visible = false;
      m_Close = true;
      Application.Current.Shutdown();
    }

    private void mnuQuit_Click(object sender, RoutedEventArgs e)
    {
      Quit();
    }

    private async void mnuPrevious_Click(object sender, RoutedEventArgs e)
    {
      if (m_Mpc.Connected) {
        try {
          await Task.Factory.StartNew(() => m_Mpc.Previous());
        }
        catch (Exception ex) {
          ShowException(ex);
          return;
        }
      }
    }

    private async void mnuNext_Click(object sender, RoutedEventArgs e)
    {
      if (m_Mpc.Connected) {
        try {
          await Task.Factory.StartNew(() => m_Mpc.Next());
        }
        catch (Exception ex) {
          ShowException(ex);
          return;
        }
      }
    }

    private async void mnuPlay_Click(object sender, RoutedEventArgs e)
    {
      if (m_Mpc.Connected){
        try{
          await Task.Factory.StartNew(() => m_Mpc.Play());
        }catch (Exception ex){
          ShowException(ex);
          return;
        }
      }
    }

    private async void mnuPause_Click(object sender, RoutedEventArgs e)
    {
      if (m_Mpc.Connected){
        try{
          await Task.Factory.StartNew(() => m_Mpc.Pause(true));
        }catch (Exception ex){
          ShowException(ex);
          return;
        }
      }
    }

    private void GetArtistInfo(object state)
    {
      string artist = (string)state;
      Dispatcher.BeginInvoke(new Action(() =>
      {
        txtArtist.Text = !string.IsNullOrEmpty(artist) ? "Downloading info" : string.Empty;
      }));

      if (!string.IsNullOrEmpty(artist)) {
        string info = LastfmScrobbler.GetArtistInfo(m_Settings.InfoLanguage, artist);
        if (string.IsNullOrEmpty(info))
          info = "No info found";

        Dispatcher.BeginInvoke(new Action(() =>
        {
          Utilities.RenderHtml(txtArtist, info, hyperlink_RequestNavigate);
          scrArtist.ScrollToTop();
        }));
      }
    }

    private void GetAlbumInfo(object state)
    {
      List<string> values = state as List<string>;
      string artist = values[0];
      string album = values[1];
      Dispatcher.BeginInvoke(new Action(() =>
      {
        txtAlbum.Text = !string.IsNullOrEmpty(artist) && !string.IsNullOrEmpty(album) ? "Downloading info" : string.Empty;
      }));

      if (!string.IsNullOrEmpty(artist) && !string.IsNullOrEmpty(album)) {
        string info = LastfmScrobbler.GetAlbumInfo(artist, album, m_Settings.InfoLanguage);
        if (string.IsNullOrEmpty(info))
          info = "No info found";

        Dispatcher.BeginInvoke(new Action(() =>
        {
          Utilities.RenderHtml(txtAlbum, info, hyperlink_RequestNavigate);
          scrAlbum.ScrollToTop();
        }));
      }
    }

    private void GetLyrics(object state)
    {
      MpdFile track = state as MpdFile;
      Dispatcher.BeginInvoke(new Action(() =>
      {
        txtLyrics.Text = track != null ? "Downloading lyrics" : string.Empty;
      }));

      if (track == null)
        return;

      string lyrics = Utilities.GetLyrics(track.Artist, track.Title);
      if (string.IsNullOrEmpty(lyrics))
        lyrics = "No lyrics found";

      Dispatcher.BeginInvoke(new Action(() =>
      {
        txtLyrics.Text = lyrics;
        scrLyrics.ScrollToTop();
      }));
    }

    private async void btnSearch_Click(object sender, RoutedEventArgs e)
    {
      if (!CheckMpdConnection())
        return;

      if (!string.IsNullOrEmpty(txtSearch.Text)){
        lstTracks.ItemsSource = null;
        lstTracksStyled.ItemsSource = null;

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
        try{
          string search = txtSearch.Text;
          m_Tracks = await Task.Factory.StartNew(() => m_Mpc.Search(searchBy, search));
        }catch (Exception ex){
          ShowException(ex);
          return;
        }
        lstTracks.ItemsSource = m_Tracks;
        lstTracksStyled.ItemsSource = m_Tracks;
        ScrollTracksToLeft();
      }else{
        m_Tracks = null;
        lstTracks.ItemsSource = null;
        lstTracksStyled.ItemsSource = null;
      }
    }

    private void btnSearchClear_Click(object sender, RoutedEventArgs e)
    {
      txtSearch.Text = string.Empty;
      cmbSearch.SelectedIndex = 0;
      m_Tracks = null;
      lstTracks.ItemsSource = null;
      lstTracksStyled.ItemsSource = null;
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
      if (listViewScrollViewer != null)
        listViewScrollViewer.ScrollToLeftEnd();
    }

		private void dragMgr_ProcessDrop( object sender, ProcessDropEventArgs<MpdFile> e )
		{
      if (m_Mpc.Connected){
        try{
          m_Mpc.Move(e.OldIndex, e.NewIndex);
        }catch (Exception ex){
          ShowException(ex);
          return;
        }
      }
    }

    private void chkTray_Changed(object sender, RoutedEventArgs e)
    {
      chkShowMiniPlayer.IsEnabled = chkCloseToTray.IsChecked == true || chkMinimizeToTray.IsChecked == true;
    }

    private void ShowException(Exception ex)
    {
      Dispatcher.BeginInvoke(new Action(() =>
      {
        MessageBox.Show(ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
      }));
    }

    #region Client to client Messages
    private void PopulateChannels()
    {
      if (!CheckMpdConnection())
        return;

      List<string> channels = null;
      try{
        channels = m_Mpc.Channels();
      }catch (Exception ex){
        ShowException(ex);
        return;
      }
      List<MpdChannel> NewChannels = new List<MpdChannel>();
      foreach (string c in channels) {
        MpdChannel ch = GetChannel(c);
        NewChannels.Add(new MpdChannel() { Name = c, 
                                           Subscribed = ch != null ? ch.Subscribed : false });
      }

      m_Channels.Clear();
      foreach (MpdChannel c in NewChannels)
        m_Channels.Add(c);
    }

    private void PopulateMessages()
    {
      if (!CheckMpdConnection())
        return;

      List<MpdMessage> messages = null;
      try{
        messages = m_Mpc.ReadChannelsMessages();
      }catch (Exception ex){
        ShowException(ex);
        return;
      }
      foreach (MpdMessage m in messages)
        m_Messages.Add(m);
    }

    private void lstChannels_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {

    }

    private void btnSendMessage_Click(object sender, RoutedEventArgs e)
    {
      if (!CheckMpdConnection())
        return;

      string channel = cmbChannnels.Text;
      if (!string.IsNullOrEmpty(channel) && !string.IsNullOrEmpty(txtMessage.Text)) {
        channel = channel.Trim();
        try{
          m_Mpc.ChannelSubscribe(channel);
        }catch (Exception ex){
          ShowException(ex);
          return;
        }        
        if (m_Mpc.ChannelSendMessage(channel, txtMessage.Text)) {
          m_Messages.Add(new MpdMessage() { Channel = channel, Message = txtMessage.Text, DateTime = DateTime.Now });
          txtMessage.Clear();
          MpdChannel c = GetChannel(channel);
          if (c != null)
            c.Subscribed = true;
          else
            m_Channels.Add(new MpdChannel() { Name=channel, Subscribed=true });

          Expander exp = GetExpander(channel);
          if (exp != null)
            exp.IsExpanded = true;
        }      
      }
    }

    private Expander GetExpander(string name)
    {
      foreach (Expander e in m_MessagesExpanders) {
        if (e.Tag as string == name)
          return e;
      }
      return null;
    }

    private MpdChannel GetChannel(string name)
    {
      foreach (MpdChannel c in m_Channels) {
        if (c.Name == name)
          return c;
      }
      return null;       
    }

    private void Expander_Loaded(object sender, RoutedEventArgs e)
    {
      m_MessagesExpanders.Add(sender as Expander);
    }

    private void Expander_Unloaded(object sender, RoutedEventArgs e)
    {
      m_MessagesExpanders.Remove(sender as Expander);
    }

    private void ChannelItem_DoubleClick(object sender, MouseButtonEventArgs e)
    {
      ListBoxItem item = sender as ListBoxItem;
      if (item != null) {
        MpdChannel ch = item.Content as MpdChannel;
        if (ch != null){
          bool res = false;
          try{
            if (ch.Subscribed)
              res = m_Mpc.ChannelUnsubscribe(ch.Name);
            else
              res = m_Mpc.ChannelSubscribe(ch.Name);
          }catch (Exception ex){
            ShowException(ex);
            return;
          }
          if (res)
            ch.Subscribed = !ch.Subscribed;
        }
      }
    }    
    #endregion
  }
}
