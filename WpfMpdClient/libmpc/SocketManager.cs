using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Libmpc
{
  public class SocketManager
  {
    Mutex m_Mutex = null;
    Socket m_Socket = null;
    StringBuilder m_TempData = new StringBuilder();

    public SocketManager(Socket socket)
    {
      if (socket == null)
        throw new ArgumentNullException("socket", "No socket");
      m_Socket = socket;

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

    public string ReadLine()
    {
      m_Mutex.WaitOne();

      bool line = false;
      byte[] socketBuffer = new byte[256];

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

      // Line from network:
      //while (m_Socket.Available > 0) {
      DateTime started = DateTime.UtcNow;
      while (true) {
        // Check timeout:
        if ((DateTime.UtcNow - started) > Timeout){
          throw new Exception("Timeout.");
        }

        int bytes = m_Socket.Receive(socketBuffer, socketBuffer.Length, SocketFlags.None);

        if (bytes > 0) {
          string read = Encoding.GetString(socketBuffer, 0, bytes);
          for (int i=0; i<read.Length; i++) {
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
        }
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

      while (sent < toSend.Length) {
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
