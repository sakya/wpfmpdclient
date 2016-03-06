using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Libmpc
{
  public class SocketManager : IDisposable
  {
    Mutex m_Mutex = null;
    Socket m_Socket = null;
    StringBuilder m_TempData = new StringBuilder();

    public SocketManager()
    {
      Encoding = Encoding.UTF8;
      Timeout = TimeSpan.FromSeconds(30);
      m_Mutex = new Mutex();
    }

    public Encoding Encoding { get; set; }

    public TimeSpan Timeout { get; set; }

    public Socket Socket {
      get
      {
        return m_Socket;
      }
    }

    public bool Connected
    {
      get
      {
        return m_Socket != null && m_Socket.Connected;
      }
    }

    public void Dispose()
    {
      if (m_Socket != null)
        m_Socket.Dispose();
    }

    public void Connect(IPEndPoint ep)
    {
      if (ep == null)
        throw new ArgumentNullException("ep");

      if (m_Socket != null)
        m_Socket.Dispose();
      m_Socket = new Socket(SocketType.Stream, ProtocolType.IP);
      m_Socket.NoDelay = true;
      m_Socket.Connect(ep);
    } // Connect

    public string ReadLine()
    {
      m_Mutex.WaitOne();

      StringBuilder sb = new StringBuilder();
      sb.Append(m_TempData.ToString());
      m_TempData.Clear();

      // Line from temp data:
      string temp = sb.ToString();
      int idx = temp.IndexOf('\n');
      if (idx >= 0) {
        string res = temp.Substring(0, idx);
        m_TempData.Append(temp.Substring(idx + 1));
        m_Mutex.ReleaseMutex();
        return res;
      }

      bool line = false;
      byte[] socketBuffer = new byte[256];

      // Line from network:
      DateTime started = DateTime.UtcNow;
      while (true) {
        // Check timeout:
        if ((DateTime.UtcNow - started) > Timeout)
          throw new Exception("Socket timeout.");

        int bytes = m_Socket.Receive(socketBuffer, socketBuffer.Length, SocketFlags.None);
        if (bytes > 0) {
          string read = Encoding.GetString(socketBuffer, 0, bytes);
          for (int i = 0; i < read.Length; i++) {
            if (read[i] == '\n') {
              line = true;
              m_TempData.Append(read.Substring(i + 1));
              break;
            } else {
              sb.Append(read[i]);
            }
          }
          if (line)
            break;
        } else
          Thread.Sleep(30);
      }

      m_Mutex.ReleaseMutex();
      return line ? sb.ToString() : null;
    } // ReadLine

    public void WriteLine(string line)
    {
      if (!line.EndsWith("\n"))
        line = string.Format("{0}\n", line);
      Write(line);
    }

    public void Write(char c)
    {
      Write(c.ToString());
    }

    public void Write(string line)
    {
      m_Mutex.WaitOne();
      if (!line.EndsWith("\n"))
        line = string.Format("{0}\n", line);

      byte[] toSend = Encoding.GetBytes(line);
      int sent = 0;

      DateTime started = DateTime.UtcNow;
      while (sent < toSend.Length) {
        // Check timeout:
        if ((DateTime.UtcNow - started) > Timeout)
          throw new Exception("Socket timeout.");

        try {
          sent += m_Socket.Send(toSend, sent, toSend.Length - sent, SocketFlags.None);
        } catch (SocketException ex) {
          if (ex.SocketErrorCode == SocketError.WouldBlock ||
              ex.SocketErrorCode == SocketError.IOPending ||
              ex.SocketErrorCode == SocketError.NoBufferSpaceAvailable) {
            Thread.Sleep(30);
          } else
            throw ex; 
        }
      }

      m_Mutex.ReleaseMutex();
    } // WriteLine
  }
}
