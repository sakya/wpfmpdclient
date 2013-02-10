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
using System.Xml.Serialization;
using System.IO;

namespace WpfMpdClient
{
  public class Settings
  {
    public Settings()
    {
      ServerAddress = "localhost";
      ServerPort = 6600;
    }

    [XmlAttribute("ServerAddress")]
    public string ServerAddress
    {
      get;
      set;
    }

    [XmlAttribute("Password")]
    public string Password
    {
      get;
      set;
    }

    [XmlAttribute("ServerPort")]
    public int ServerPort
    {
      get;
      set;
    }

    [XmlAttribute("AutoReconnect")]
    public bool AutoReconnect
    {
      get;
      set;
    }

    [XmlAttribute("AutoReconnectDelay")]
    public int AutoReconnectDelay
    {
      get;
      set;
    }

    [XmlAttribute("showstopbutton")]
    public bool ShowStopButton
    {
      get;
      set;
    }

    [XmlAttribute("showfilesystemtab")]
    public bool ShowFilesystemTab
    {
      get;
      set;
    }

    [XmlAttribute("minimizetotray")]
    public bool MinimizeToTray {
      get;
      set;
    }

    [XmlAttribute("closetotray")]
    public bool CloseToTray {
      get;
      set;
    }

    [XmlAttribute("showminiplayer")]
    public bool ShowMiniPlayer
    {
      get;
      set;
    }

    [XmlAttribute("windowmaximized")]
    public bool WindowMaximized
    {
      get;
      set;
    }

    [XmlAttribute("windowwidth")]
    public double WindowWidth 
    {
      get;
      set;
    }

    [XmlAttribute("windowheight")]
    public double WindowHeight 
    {
      get;
      set;
    }

    private double m_WindowLeft = -1;
    [XmlAttribute("windowleft")]
    public double WindowLeft 
    {
      get { return m_WindowLeft; }
      set { m_WindowLeft = value; }
    }

    private double m_WindowTop = -1;
    [XmlAttribute("windowtop")]
    public double WindowTop
    {
      get { return m_WindowTop; }
      set { m_WindowTop = value; }
    }

    private double m_MiniWindowLeft = -1;
    [XmlAttribute("miniwindowleft")]
    public double MiniWindowLeft 
    {
      get { return m_MiniWindowLeft; }
      set { m_MiniWindowLeft = value; }
    }

    private double m_MiniWindowTop = -1;
    [XmlAttribute("miniwindowtop")]
    public double MiniWindowTop
    {
      get { return m_MiniWindowTop; }
      set { m_MiniWindowTop = value; }
    }

    [XmlAttribute("scrobbler")]
    public bool Scrobbler {
      get;
      set;
    }

    [XmlAttribute("scrobblerkey")]
    public string ScrobblerSessionKey
    {
      get;
      set;
    }

    [XmlElement("trackslistview")]
    public List<ConfigurableListView.ColumnInfo> TracksListView
    {
      get;
      set;
    }

    [XmlElement("playlistview")]
    public List<ConfigurableListView.ColumnInfo> PlayListView
    {
      get;
      set;
    }

    public static string GetSettingsFileName()
    {
      return string.Format("{0}\\wpfmpdclient\\settings.xml", Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData));
    } // GetSettingsFileName

    public static Settings Deserialize(string fileName)
    {
      if (!File.Exists(fileName))
        return null;

      try {
        XmlSerializer serializer = new XmlSerializer(typeof(Settings));

        Settings result;
        using (FileStream fs = new FileStream(fileName, FileMode.Open, FileAccess.Read))
          result = serializer.Deserialize(fs) as Settings;
        return result;
      }
      catch (Exception) {
        return null;
      }
    } // Deserialize

    public bool Serialize(string fileName)
    {
      try {
        string path = Path.GetDirectoryName(fileName);
        if (!Directory.Exists(path))
          Directory.CreateDirectory(path);

        XmlSerializer serializer = new XmlSerializer(typeof(Settings));
        using (TextWriter textWriter = new StreamWriter(fileName)) {
          serializer.Serialize(textWriter, this);
        }
      }
      catch (Exception) {
        return false;
      }
      return true;
    } // Serialize

  }
}
