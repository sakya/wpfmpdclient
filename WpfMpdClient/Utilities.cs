using System;
using System.Collections.Generic;
using System.Text;
using System.Net;
using System.IO;
using System.Xml;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using System.Web;
using System.Windows;
using System.Text.RegularExpressions;
using System.Security.Cryptography;
using System.Windows.Controls;
using System.Threading;
using System.Windows.Data;
using System.Windows.Media;

namespace WpfMpdClient
{
  public class Utilities
  {
    public static string FormatSeconds(int seconds)
    {
      DateTime d = new DateTime(2000, 1, 1, 0, 0, 0);
      d = d.AddSeconds(seconds);
      if (d.Hour > 0)
        return d.ToString("hh:mm:ss");
      else
        return d.ToString("mm:ss");
    } // FormatSeconds

    public static string GetLyricsWikia(string artist, string title)
    {
      if (string.IsNullOrEmpty(artist) || string.IsNullOrEmpty(title))
        return string.Empty;

      string url = string.Format("http://lyrics.wikia.com/api.php?artist={0}&song={1}&fmt=xml", 
                                 Uri.EscapeDataString(artist), Uri.EscapeDataString(title));
      try {
        using (WebClient client = new WebClient()) {
          using (Stream data = client.OpenRead(url)) {
            StreamReader reader = new StreamReader(data);
            string str = null;
            StringBuilder sb = new StringBuilder();
            while ((str = reader.ReadLine()) != null)
              sb.AppendLine(str);

            string xml = sb.ToString();
            XmlDocument doc = new XmlDocument();
            doc.LoadXml(xml);
            XmlNodeList xnList = doc.SelectNodes("/LyricsResult/url");
            if (xnList != null && xnList.Count == 1) {
              string lurl = xnList[0].InnerText;
              if (!lurl.EndsWith("action=edit")){
                using (Stream ldata = client.OpenRead(lurl)) {
                  StreamReader lreader = new StreamReader(ldata);
                  StringBuilder lsb = new StringBuilder();
                  while ((str = lreader.ReadLine()) != null)
                    lsb.AppendLine(str);

                  string lpage = lsb.ToString();
                  int start = lpage.IndexOf("</div>&#");
                  if (start < 0)
                    start = lpage.IndexOf("</div><b>&#");
                  if (start >= 0) {
                    start += 6;
                    int end = lpage.IndexOf(";<!--", start);
                    if (end >= 0) {
                      end++;
                      lpage = lpage.Substring(start, end - start);
                      lpage = lpage.Replace("<br />", "\r\n");
                      lpage = lpage.Replace("<br\r\n/>", "\r\n");
                      string res = WebUtility.HtmlDecode(lpage);
                      // Remove html tags (like <b>...</b>)
                      res = Regex.Replace(res, @"<[^>]*>", string.Empty);
                      return res;
                    }
                  }
                }
              }
            }
          }
        }
      }
      catch (Exception) {
        return string.Empty;
      }
      return string.Empty;
    } // GetLyricsWikia

    public static string GetLyricsChartlyrics(string artist, string title)
    {
      if (string.IsNullOrEmpty(artist) || string.IsNullOrEmpty(title))
        return string.Empty;

      string url = string.Format("http://api.chartlyrics.com/apiv1.asmx/SearchLyricDirect?artist={0}&song={1}",
                                  Uri.EscapeDataString(artist), Uri.EscapeDataString(title)); 
      try {
        using (WebClient client = new WebClient()) {
          using (Stream data = client.OpenRead(url)) {
            StreamReader reader = new StreamReader(data);
            string str = null;
            StringBuilder sb = new StringBuilder();
            while ((str = reader.ReadLine()) != null)
              sb.AppendLine(str);

            string xml = sb.ToString();
            XmlDocument doc = new XmlDocument();
            doc.LoadXml(xml);
            XmlNamespaceManager nsmgr = new XmlNamespaceManager(doc.NameTable);
            nsmgr.AddNamespace("ab", "http://api.chartlyrics.com/");
            XmlNodeList xnList = doc.SelectNodes("//ab:Lyric", nsmgr);
            if (xnList != null && xnList.Count == 1)
              return WebUtility.HtmlDecode(xnList[0].InnerText);
          }
        }
      }
      catch (Exception) {
        return string.Empty;
      }
      return string.Empty;
    } // GetLyricsChartlyrics

    public static string GetLyrics(string artist, string title)
    {
      if (string.IsNullOrEmpty(artist) || string.IsNullOrEmpty(title))
        return string.Empty;

      string lyrics = GetLyricsWikia(artist, title);
      if (string.IsNullOrEmpty(lyrics)) {
        lyrics = GetLyricsChartlyrics(artist, title);
      }
      return lyrics;
    } // GetLyrics

    public static bool Confirm(string title, string message)
    {
      return MessageBox.Show(message, title, MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;
    } // Confirm

    public static string EncryptString(string ClearText)
    {
      if (string.IsNullOrEmpty(ClearText))
        return string.Empty;

      byte[] clearTextBytes = Encoding.UTF8.GetBytes(ClearText);
      System.Security.Cryptography.SymmetricAlgorithm rijn = SymmetricAlgorithm.Create();
      MemoryStream ms = new MemoryStream();

      byte[] rgbIV = Encoding.ASCII.GetBytes("ryojvlzmdalyglrj");
      byte[] key = Encoding.ASCII.GetBytes("hcxilkqbbhczfeultgbskdmaunivmfuo");
      CryptoStream cs = new CryptoStream(ms, rijn.CreateEncryptor(key, rgbIV), CryptoStreamMode.Write);

      cs.Write(clearTextBytes, 0, clearTextBytes.Length);
      cs.Close();

      return Convert.ToBase64String(ms.ToArray());
    } // EncryptString

    public static string DecryptString(string EncryptedText)
    {
      if (string.IsNullOrEmpty(EncryptedText))
        return string.Empty;
      try{
        byte[] encryptedTextBytes = Convert.FromBase64String(EncryptedText);
        MemoryStream ms = new MemoryStream();
        System.Security.Cryptography.SymmetricAlgorithm rijn = SymmetricAlgorithm.Create();

        byte[] rgbIV = Encoding.ASCII.GetBytes("ryojvlzmdalyglrj");
        byte[] key = Encoding.ASCII.GetBytes("hcxilkqbbhczfeultgbskdmaunivmfuo");

        CryptoStream cs = new CryptoStream(ms, rijn.CreateDecryptor(key, rgbIV),
        CryptoStreamMode.Write);
        cs.Write(encryptedTextBytes, 0, encryptedTextBytes.Length);
        cs.Close();

        return Encoding.UTF8.GetString(ms.ToArray());
      }catch (Exception){
        return string.Empty;
      }
    } // DecryptString

    public static T GetVisualChild<T>(DependencyObject parent) where T : Visual
    {
      T child = default(T);
      int numVisuals = VisualTreeHelper.GetChildrenCount(parent);
      for (int i = 0; i < numVisuals; i++) {
        Visual v = (Visual)VisualTreeHelper.GetChild(parent, i);
        child = v as T;
        if (child == null) {
          child = GetVisualChild<T>(v);
        }
        if (child != null) {
          break;
        }
      }
      return child;
    } // GetVisualChild
  }

  public class TrackConverter : System.Windows.Data.IValueConverter
  {
    public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
    {
      string val = (string)value;
      if (!string.IsNullOrEmpty(val)) {
        int index = val.IndexOf("/");
        string t = val;
        if (index >= 0) {
          t = val.Substring(0, index);
        }

        int intValue = int.Parse(t);
        return intValue.ToString();
      }
      return val;
    }

    public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
    {
      throw new NotImplementedException();
    }
  }

  public class TimeConverter : System.Windows.Data.IValueConverter
  {
    public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
    {
      if (value == null)
        return DateTime.MinValue;

      Int32 val = (Int32)value;

      DateTime d = new DateTime(2000, 1, 1, 0, 0, 0);
      d = d.AddSeconds(val);
      if (d.Hour > 0)
        return d.ToString("hh:mm:ss");
      else
        return d.ToString("mm:ss");
    }

    public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
    {
      throw new NotImplementedException();
    }
  }

  public class IdConverter : IMultiValueConverter
  {
    public object Convert(object[] values, Type targetType, object parameter,
        System.Globalization.CultureInfo culture)
    {
      try {
        if (values[0] == null || values[1] == null)
          return false;

        Int32 id1 = (Int32)values[0];
        Int32 id2 = (Int32)values[1];
        return id1 == id2;
      }catch (Exception){
        return false;
      }
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter,
        System.Globalization.CultureInfo culture)
    {
      throw new NotImplementedException();
    }
  }

  public class ExpanderWidthConverter : System.Windows.Data.IValueConverter
  {
    public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
    {
      if (value == null)
        return 100.0;

      double width = (double)value;
      return width / 2;
    }

    public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
    {
      throw new NotImplementedException();
    }
  }


  /// <summary>
  /// Listens keyboard globally.
  /// 
  /// <remarks>Uses WH_KEYBOARD_LL.</remarks>
  /// </summary>
  public class KeyboardListener : IDisposable
  {
    /// <summary>
    /// Creates global keyboard listener.
    /// </summary>
    public KeyboardListener()
    {
      // We have to store the HookCallback, so that it is not garbage collected runtime
      hookedLowLevelKeyboardProc = (InterceptKeys.LowLevelKeyboardProc)LowLevelKeyboardProc;

      // Set the hook
      hookId = InterceptKeys.SetHook(hookedLowLevelKeyboardProc);

      // Assign the asynchronous callback event
      hookedKeyboardCallbackAsync = new KeyboardCallbackAsync(KeyboardListener_KeyboardCallbackAsync);
    }

    /// <summary>
    /// Destroys global keyboard listener.
    /// </summary>
    ~KeyboardListener()
    {
      Dispose();
    }

    /// <summary>
    /// Fired when any of the keys is pressed down.
    /// </summary>
    public event RawKeyEventHandler KeyDown;

    /// <summary>
    /// Fired when any of the keys is released.
    /// </summary>
    public event RawKeyEventHandler KeyUp;

    #region Inner workings
    /// <summary>
    /// Hook ID
    /// </summary>
    private IntPtr hookId = IntPtr.Zero;

    /// <summary>
    /// Asynchronous callback hook.
    /// </summary>
    /// <param name="nCode"></param>
    /// <param name="wParam"></param>
    /// <param name="lParam"></param>
    private delegate void KeyboardCallbackAsync(InterceptKeys.KeyEvent keyEvent, int vkCode);

    /// <summary>
    /// Actual callback hook.
    /// 
    /// <remarks>Calls asynchronously the asyncCallback.</remarks>
    /// </summary>
    /// <param name="nCode"></param>
    /// <param name="wParam"></param>
    /// <param name="lParam"></param>
    /// <returns></returns>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private IntPtr LowLevelKeyboardProc(int nCode, UIntPtr wParam, IntPtr lParam)
    {
      if (nCode >= 0)
        if (wParam.ToUInt32() == (int)InterceptKeys.KeyEvent.WM_KEYDOWN ||
            wParam.ToUInt32() == (int)InterceptKeys.KeyEvent.WM_KEYUP ||
            wParam.ToUInt32() == (int)InterceptKeys.KeyEvent.WM_SYSKEYDOWN ||
            wParam.ToUInt32() == (int)InterceptKeys.KeyEvent.WM_SYSKEYUP)
          hookedKeyboardCallbackAsync.BeginInvoke((InterceptKeys.KeyEvent)wParam.ToUInt32(), Marshal.ReadInt32(lParam), null, null);

      return InterceptKeys.CallNextHookEx(hookId, nCode, wParam, lParam);
    }

    /// <summary>
    /// Event to be invoked asynchronously (BeginInvoke) each time key is pressed.
    /// </summary>
    private KeyboardCallbackAsync hookedKeyboardCallbackAsync;

    /// <summary>
    /// Contains the hooked callback in runtime.
    /// </summary>
    private InterceptKeys.LowLevelKeyboardProc hookedLowLevelKeyboardProc;

    /// <summary>
    /// HookCallbackAsync procedure that calls accordingly the KeyDown or KeyUp events.
    /// </summary>
    /// <param name="keyEvent">Keyboard event</param>
    /// <param name="vkCode">VKCode</param>
    void KeyboardListener_KeyboardCallbackAsync(InterceptKeys.KeyEvent keyEvent, int vkCode)
    {
      switch (keyEvent) {
        // KeyDown events
        case InterceptKeys.KeyEvent.WM_KEYDOWN:
          if (KeyDown != null)
            KeyDown(this, new RawKeyEventArgs(vkCode, false));
          break;
        case InterceptKeys.KeyEvent.WM_SYSKEYDOWN:
          if (KeyDown != null)
            KeyDown(this, new RawKeyEventArgs(vkCode, true));
          break;

        // KeyUp events
        case InterceptKeys.KeyEvent.WM_KEYUP:
          if (KeyUp != null)
            KeyUp(this, new RawKeyEventArgs(vkCode, false));
          break;
        case InterceptKeys.KeyEvent.WM_SYSKEYUP:
          if (KeyUp != null)
            KeyUp(this, new RawKeyEventArgs(vkCode, true));
          break;

        default:
          break;
      }
    }

    #endregion

    #region IDisposable Members

    /// <summary>
    /// Disposes the hook.
    /// <remarks>This call is required as it calls the UnhookWindowsHookEx.</remarks>
    /// </summary>
    public void Dispose()
    {
      InterceptKeys.UnhookWindowsHookEx(hookId);
    }

    #endregion
  }
  /// <summary>
  /// Raw KeyEvent arguments.
  /// </summary>
  public class RawKeyEventArgs : EventArgs
  {
    /// <summary>
    /// VKCode of the key.
    /// </summary>
    public int VKCode;

    /// <summary>
    /// WPF Key of the key.
    /// </summary>
    public Key Key;

    /// <summary>
    /// Is the hitted key system key.
    /// </summary>
    public bool IsSysKey;

    /// <summary>
    /// Create raw keyevent arguments.
    /// </summary>
    /// <param name="VKCode"></param>
    /// <param name="isSysKey"></param>
    public RawKeyEventArgs(int VKCode, bool isSysKey)
    {
      this.VKCode = VKCode;
      this.IsSysKey = isSysKey;
      this.Key = System.Windows.Input.KeyInterop.KeyFromVirtualKey(VKCode);
    }
  }

  /// <summary>
  /// Raw keyevent handler.
  /// </summary>
  /// <param name="sender">sender</param>
  /// <param name="args">raw keyevent arguments</param>
  public delegate void RawKeyEventHandler(object sender, RawKeyEventArgs args);

  #region WINAPI Helper class
  /// <summary>
  /// Winapi Key interception helper class.
  /// </summary>
  internal static class InterceptKeys
  {
    public delegate IntPtr LowLevelKeyboardProc(int nCode, UIntPtr wParam, IntPtr lParam);
    public static int WH_KEYBOARD_LL = 13;

    public enum KeyEvent : int
    {
      WM_KEYDOWN = 256,
      WM_KEYUP = 257,
      WM_SYSKEYUP = 261,
      WM_SYSKEYDOWN = 260
    }

    public static IntPtr SetHook(LowLevelKeyboardProc proc)
    {
      using (Process curProcess = Process.GetCurrentProcess())
      using (ProcessModule curModule = curProcess.MainModule) {
        return SetWindowsHookEx(WH_KEYBOARD_LL, proc,
            GetModuleHandle(curModule.ModuleName), 0);
      }
    }

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    public static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    public static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, UIntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    public static extern IntPtr GetModuleHandle(string lpModuleName);
  }
  #endregion
}
