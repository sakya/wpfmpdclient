using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Security.Cryptography;
using System.Xml;
using System.Net;
using System.IO;
using System.Web;
using System.Collections.Specialized;
using System.Xml.Serialization;

namespace WpfMpdClient.scrobbler
{
  public class ScrobblerTrack
  {
    [XmlAttribute("artist")]
    public string Artist
    {
      get;
      set;
    }

    [XmlAttribute("album")]
    public string Album
    {
      get;
      set;
    }

    [XmlAttribute("title")]
    public string Title
    {
      get;
      set;
    }

    [XmlAttribute("listened")]
    public DateTime Listened
    {
      get;
      set;
    }
  }

  public class ScrobblerCache
  {
    [XmlElement("track")]
    public List<ScrobblerTrack> Tracks
    {
      get;
      set;
    }

    public static string GetCacheFileName()
    {
      return string.Format("{0}\\wpfmpdclient\\scrobbler.xml", Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData));
    } 

    public static ScrobblerCache Deserialize(string fileName)
    {
      if (!File.Exists(fileName))
        return null;

      try {
        XmlSerializer serializer = new XmlSerializer(typeof(ScrobblerCache));

        ScrobblerCache result;
        using (FileStream fs = new FileStream(fileName, FileMode.Open, FileAccess.Read))
          result = serializer.Deserialize(fs) as ScrobblerCache;
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

        XmlSerializer serializer = new XmlSerializer(typeof(ScrobblerCache));
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
  public class Scrobbler
  {    
    private const string api_key = "";
    private const string api_secret = "";
    private const string m_BaseUrl = "http://ws.audioscrobbler.com/2.0/";

    ScrobblerCache m_Cache = null;
    string m_Token = string.Empty;
    string m_SessionKey = string.Empty;

    public Scrobbler(string sessionKey)
    {
      m_SessionKey = sessionKey;

      m_Cache = ScrobblerCache.Deserialize(ScrobblerCache.GetCacheFileName());
      if (m_Cache == null)
        m_Cache = new ScrobblerCache();
      if (m_Cache.Tracks == null)
        m_Cache.Tracks = new List<ScrobblerTrack>();
    }

    #region Public Operations
    public bool SaveCache()
    {
      return m_Cache.Serialize(ScrobblerCache.GetCacheFileName());
    }

    public string GetAuthorizationUrl(string token)
    {
      if (string.IsNullOrEmpty(token))
        token = GetToken();
      m_Token = token;

      if (!string.IsNullOrEmpty(token))
        return string.Format("http://www.last.fm/api/auth/?api_key={0}&token={1}", api_key, token);
      return string.Empty;
    } // GetAuthorizationUrl

    public string GetToken()
    {
      Dictionary<string, string> parameters = new Dictionary<string,string>();
      parameters["method"] = "auth.getToken";
      parameters["api_key"] = api_key;
      parameters["api_sig"] = GetSignature(parameters);

      XmlDocument xml = GetResponse(GetUrl(m_BaseUrl, parameters));
      if (xml != null){
        XmlNodeList list = xml.SelectNodes("/lfm/token");
        if (list != null && list.Count > 0){
          return list[0].InnerText;
        }
      }
      return string.Empty;
    } // GetToken

    public string GetSession()
    {
      if (string.IsNullOrEmpty(m_Token))
        return string.Empty;

      Dictionary<string, string> parameters = new Dictionary<string,string>();
      parameters["method"] = "auth.getSession";
      parameters["api_key"] = api_key;
      parameters["token"] = m_Token;
      parameters["api_sig"] = GetSignature(parameters);

      XmlDocument xml = GetResponse(GetUrl(m_BaseUrl, parameters));
      if (xml != null){
        XmlNodeList list = xml.SelectNodes("/lfm/session/key");
        if (list != null && list.Count > 0){
          m_SessionKey = list[0].InnerText;
          return list[0].InnerText;
        }
      }
      return string.Empty;
    } // GetSession

    public bool UpdateNowPlaying(string artist, string title, string album)
    {
      if (string.IsNullOrEmpty(m_SessionKey))
        return false;

      Dictionary<string, string> parameters = new Dictionary<string,string>();
      parameters["method"] = "track.updateNowPlaying";
      parameters["artist"] = artist;
      parameters["track"] = title;
      parameters["album"] = album;
      parameters["api_key"] = api_key;
      parameters["sk"] = m_SessionKey;
      parameters["api_sig"] = GetSignature(parameters);
      XmlDocument xml = GetPostResponse(m_BaseUrl, parameters);
      if (xml != null){
        return true;
      }
      return false;
    } // UpdateNowPlaying

    public bool Scrobble(string artist, string title, string album, DateTime started)
    {
      if (string.IsNullOrEmpty(m_SessionKey))
        return false;

      Dictionary<string, string> parameters = new Dictionary<string,string>();
      parameters["method"] = "track.scrobble";

      m_Cache.Tracks.Add(new ScrobblerTrack() 
                         {
                           Artist = artist,
                           Album = album,
                           Title = title,
                           Listened = started
                         });

      DateTime epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

      int index = 0;
      foreach (ScrobblerTrack t in m_Cache.Tracks){
        parameters[string.Format("artist[{0}]", index)] = t.Artist;
        parameters[string.Format("track[{0}]", index)] = t.Title;
        parameters[string.Format("album[{0}]", index)] = t.Album;

        Int64 startedEpoch = Convert.ToInt64((t.Listened.ToUniversalTime() - epoch).TotalSeconds);
        parameters[string.Format("timestamp[{0}]", index)] = startedEpoch.ToString("################");

        parameters[string.Format("chosenByUser[{0}]", index)] = "1";
        index++;
      }
      parameters["api_key"] = api_key;
      parameters["sk"] = m_SessionKey;
      parameters["api_sig"] = GetSignature(parameters);

      XmlDocument xml = GetPostResponse(m_BaseUrl, parameters);
      if (xml != null){
        m_Cache.Tracks.Clear();
        XmlNodeList list = xml.SelectNodes("/lfm/scrobbles");
        if (list != null && list.Count > 0)
          return list[0].Attributes["accepted"].Value != "0";
      }
      return false;
    } // Scrobble
    #endregion

    #region Static operations
    public static string GetAlbumArt(string artist, string album)
    {
      Dictionary<string, string> parameters = new Dictionary<string,string>();
      parameters["method"] = "album.getinfo";
      parameters["api_key"] = api_key;
      parameters["artist"] = artist;
      parameters["album"] = album;

      XmlDocument xml = GetResponse(GetUrl(m_BaseUrl, parameters));
      if (xml != null){
        XmlNodeList xnList = xml.SelectNodes("/lfm/album/image");
        foreach (XmlNode xn in xnList) {
          if (xn.Attributes["size"].Value == "mega")
            return xn.InnerText;
        }
      }
      return string.Empty;
    } // GetAlbumArt

    private static XmlDocument GetResponse(Uri url)
    {
      using (WebClient client = new WebClient()) {
        using (Stream data = client.OpenRead(url)) {
          StreamReader reader = new StreamReader(data);
          string str = null;
          StringBuilder sb = new StringBuilder();
          while ((str = reader.ReadLine()) != null)
            sb.AppendLine(str);

          try{
            string xml = sb.ToString();
            XmlDocument doc = new XmlDocument();
            doc.LoadXml(xml);
            return doc;
          }catch (Exception){
            return null;
          }
        }
      }
    } // GetResponse

    private static Uri GetUrl(string address, Dictionary<string, string> parameters)
    {
      StringBuilder sb = new StringBuilder();
      sb.Append(address);
      if (parameters.Keys.Count > 0){
        sb.Append("?");
        bool first = true;
        foreach (string k in parameters.Keys){
          if (!first)
            sb.Append("&");
          first = false;
          sb.Append(string.Format("{0}={1}", k, HttpUtility.UrlEncode(parameters[k])));
        }
      }

      return new Uri(sb.ToString());
    } // GetUrl
    #endregion

    #region Private Operations
    private string GetSignature(Dictionary<string, string> parameters)
    {
      StringBuilder sb = new StringBuilder();

      List<string> keys = parameters.Keys.ToList();
	    keys.Sort();
      foreach (string k in keys){
        sb.Append(k);
        sb.Append(parameters[k]);
      }
      sb.Append(api_secret);

      string res = sb.ToString();
      using (MD5 md5Hash = MD5.Create()) {
        byte[] data = md5Hash.ComputeHash(Encoding.UTF8.GetBytes(res));
        StringBuilder sBuilder = new StringBuilder();
        for (int i = 0; i < data.Length; i++)
          sBuilder.Append(data[i].ToString("x2"));
        res = sBuilder.ToString();
      }
      return res;
    } // GetSignature

    private XmlDocument GetPostResponse(string url, Dictionary<string, string> parameters)
    {
      using (WebClient client = new WebClient()) {
        NameValueCollection data = new NameValueCollection();
        foreach (string k in parameters.Keys)
          data[k] = parameters[k];

        byte[] response = client.UploadValues(new Uri(url), "POST", data);
        try {
          XmlDocument doc = new XmlDocument();
          doc.LoadXml(System.Text.Encoding.UTF8.GetString(response));
          return doc;
        }
        catch (Exception) {
          return null;
        }
      }
    } // GetPostResponse
    #endregion
  }
}
