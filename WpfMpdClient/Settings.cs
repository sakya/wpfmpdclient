﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Serialization;
using System.IO;

namespace WpfMpdClient
{
  public class Settings
  {
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
