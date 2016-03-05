/*
 * Copyright 2008 Matthias Sessler
 * 
 * This file is part of LibMpc.net.
 *
 * LibMpc.net is free software: you can redistribute it and/or modify
 * it under the terms of the GNU Lesser General Public License as published by
 * the Free Software Foundation, either version 2.1 of the License, or
 * (at your option) any later version.
 *
 * LibMpc.net is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU Lesser General Public License for more details.
 *
 * You should have received a copy of the GNU Lesser General Public License
 * along with LibMpc.net.  If not, see <http://www.gnu.org/licenses/>.
 */
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace Libmpc
{
  /// <summary>
  /// The delegate for the <see cref="MpcConnection.OnConnected"/> and <see cref="MpcConnection.OnDisconnected"/> events.
  /// </summary>
  /// <param name="connection">The connection firing the event.</param>
  public delegate void MpcConnectionEventDelegate(MpcConnection connection);
  public delegate void MpcConnectionIdleEventDelegate(MpcConnection connection, Mpc.Subsystems subsystems);

  /// <summary>
  /// Keeps the connection to the MPD server and handels the most basic structure of the
  /// MPD protocol. The high level commands are handeled in the <see cref="Libmpc.Mpc"/>
  /// class.
  /// </summary>
  public class MpcConnection
  {
    /// <summary>
    /// Is fired when a connection to a MPD server is established.
    /// </summary>
    public event MpcConnectionEventDelegate OnConnected;
    /// <summary>
    /// Is fired when the connection to the MPD server is closed.
    /// </summary>
    public event MpcConnectionEventDelegate OnDisconnected;
    public event MpcConnectionIdleEventDelegate OnSubsystemsChanged;

    private static readonly string FIRST_LINE_PREFIX = "OK MPD ";

    private static readonly string OK = "OK";
    private static readonly string ACK = "ACK";

    private static readonly Regex ACK_REGEX = new Regex("^ACK \\[(?<code>[0-9]*)@(?<nr>[0-9]*)] \\{(?<command>[a-z]*)} (?<message>.*)$");

    private List<string> m_Commands = null;
    private Mutex m_Mutex = new Mutex();
    private IPEndPoint ipEndPoint = null;

    private SocketManager m_SocketManager = null;

    private string version;
    /// <summary>
    /// If the connection to the MPD is connected.
    /// </summary>
    public bool Connected { get { return (m_SocketManager != null) && m_SocketManager.Connected; } }
    /// <summary>
    /// The version of the MPD.
    /// </summary>
    public string Version { get { return this.version; } }

    private bool autoConnect = false;
    /// <summary>
    /// If a connection should be established when a command is to be
    /// executed in disconnected state.
    /// </summary>
    public bool AutoConnect
    {
      get { return this.autoConnect; }
      set { this.autoConnect = value; }
    }

    public List<string> Commands
    {
      get { return m_Commands; }
    }

    /// <summary>
    /// Creates a new MpdConnection.
    /// </summary>
    public MpcConnection() { }
    /// <summary>
    /// Creates a new MpdConnection.
    /// </summary>
    /// <param name="server">The IPEndPoint of the MPD server.</param>
    public MpcConnection(IPEndPoint server) { this.Connect(server); }

    /// <summary>
    /// The IPEndPoint of the MPD server.
    /// </summary>
    /// <exception cref="AlreadyConnectedException">When a conenction to a MPD server is already established.</exception>
    public IPEndPoint Server
    {
      get { return this.ipEndPoint; }
      set
      {
        if (this.Connected)
          throw new AlreadyConnectedException();

        this.ipEndPoint = value;

        this.ClearConnectionFields();
      }
    }
    /// <summary>
    /// Connects to a MPD server.
    /// </summary>
    /// <param name="server">The IPEndPoint of the server.</param>
    public void Connect(IPEndPoint server)
    {
      this.Server = server;
      this.Connect();
    }
    /// <summary>
    /// Connects to the MPD server who's IPEndPoint was set in the Server property.
    /// </summary>
    /// <exception cref="InvalidOperationException">If no IPEndPoint was set to the Server property.</exception>
    public void Connect()
    {
      if (this.ipEndPoint == null)
        throw new InvalidOperationException("Server IPEndPoint not set.");

      if (this.Connected)
        throw new AlreadyConnectedException();

      if (m_SocketManager != null) {
        m_SocketManager.Dispose();
      }

      m_SocketManager = new SocketManager();
      m_SocketManager.Connect(ipEndPoint);

      string firstLine = m_SocketManager.ReadLine();
      if (!firstLine.StartsWith(FIRST_LINE_PREFIX)) {
        this.Disconnect();
        throw new InvalidDataException("Response of mpd does not start with \"" + FIRST_LINE_PREFIX + "\".");
      }
      this.version = firstLine.Substring(FIRST_LINE_PREFIX.Length);

      //m_SocketManager.WriteLine(string.Empty);
      //this.readResponse();

      MpdResponse response = Exec("commands");
      m_Commands = response.getValueList();

      if (this.OnConnected != null)
        this.OnConnected.Invoke(this);
    }
    /// <summary>
    /// Disconnects from the current MPD server.
    /// </summary>
    public void Disconnect()
    {
      if (m_SocketManager == null)
        return;

      m_SocketManager.Socket.Close();
      m_SocketManager.Socket.Dispose();
      this.ClearConnectionFields();

      if (this.OnDisconnected != null)
        this.OnDisconnected.Invoke(this);
    }

    /// <summary>
    /// Puts the client in idle mode for the given subsystems
    /// </summary>
    /// <param name="subsystems">The subsystems to listen to.</param>
    public void Idle(Mpc.Subsystems subsystems)
    {      
      StringBuilder subs = new StringBuilder();
      foreach (Mpc.Subsystems s in Enum.GetValues(typeof(Mpc.Subsystems))){
        if (s != Mpc.Subsystems.All && (subsystems & s) != 0)
          subs.AppendFormat(" {0}", s.ToString());
      }
      string command = string.Format("idle {0}", subs.ToString());

      try {
        while (true){
          this.CheckConnected();
          m_SocketManager.WriteLine(command);
          MpdResponse res = this.readResponse();

          Mpc.Subsystems eventSubsystems = Mpc.Subsystems.None;
          foreach (string m in res.Message){
            List<string> values = res.getValueList();
            foreach (string sub in values){
              Mpc.Subsystems s = Mpc.Subsystems.None;
              if (Enum.TryParse<Mpc.Subsystems>(sub, out s)){
                eventSubsystems |= s;
              }
            }
          }

          if (eventSubsystems != Mpc.Subsystems.None && this.OnSubsystemsChanged != null)
            this.OnSubsystemsChanged(this, eventSubsystems);
        }
      }
      catch (Exception) {
        try {
          this.Disconnect();
        }
        catch (Exception) { }
      }
    }

    /// <summary>
    /// Executes a simple command without arguments on the MPD server and returns the response.
    /// </summary>
    /// <param name="command">The command to execute.</param>
    /// <returns>The MPD server response parsed into a basic object.</returns>
    /// <exception cref="ArgumentException">If the command contains a space of a newline charakter.</exception>
    public MpdResponse Exec(string command)
    {
      if (command == null)
        throw new ArgumentNullException("command");
      if (command.Contains(" "))
        throw new ArgumentException("command contains space");
      if (command.Contains("\n"))
        throw new ArgumentException("command contains newline");

      if (m_Commands != null && !m_Commands.Contains(command))
        return new MpdResponse(new ReadOnlyCollection<string>(new List<string>()));

      try {
        this.CheckConnected();
        m_Mutex.WaitOne();
        m_SocketManager.WriteLine(command);

        MpdResponse res = this.readResponse();
        m_Mutex.ReleaseMutex();
        return res;
      }
      catch (Exception ex) {
        System.Diagnostics.Debug.WriteLine(string.Format("Exec: {0}", ex.Message));
        try {
          this.Disconnect();
        }
        catch (Exception) { }
        return new MpdResponse(new ReadOnlyCollection<string>(new List<string>()));
        //throw;
      }
    }
    /// <summary>
    /// Executes a MPD command with arguments on the MPD server.
    /// </summary>
    /// <param name="command">The command to execute.</param>
    /// <param name="argument">The arguments of the command.</param>
    /// <returns>The MPD server response parsed into a basic object.</returns>
    /// <exception cref="ArgumentException">If the command contains a space of a newline charakter.</exception>
    public MpdResponse Exec(string command, string[] argument)
    {
      if (command == null)
        throw new ArgumentNullException("command");
      if (command.Contains(" "))
        throw new ArgumentException("command contains space");
      if (command.Contains("\n"))
        throw new ArgumentException("command contains newline");

      if (argument == null)
        throw new ArgumentNullException("argument");
      for (int i = 0; i < argument.Length; i++) {
        if (argument[i] == null)
          throw new ArgumentNullException("argument[" + i + "]");
        if (argument[i].Contains("\n"))
          throw new ArgumentException("argument[" + i + "] contains newline");
      }

      if (m_Commands != null && !m_Commands.Contains(command))
        return new MpdResponse(new ReadOnlyCollection<string>(new List<string>()));

      try {
        this.CheckConnected();
        m_Mutex.WaitOne();
        m_SocketManager.WriteLine(string.Format("{0} {1}", command, string.Join(" ", argument)));

        MpdResponse res = this.readResponse();
        m_Mutex.ReleaseMutex();
        return res;
      }
      catch (Exception) {
        try { this.Disconnect(); }
        catch (Exception) { }
        return new MpdResponse(new ReadOnlyCollection<string>(new List<string>()));
        //throw;
      }
    }

    private void CheckConnected()
    {
      if (!this.Connected) {
        if (this.autoConnect)
          this.Connect();
        else
          if (this.OnDisconnected != null)
            this.OnDisconnected.Invoke(this);
          throw new NotConnectedException();
      }

    }

    private void WriteToken(string token)
    {
      if (token.Contains(" ")) {
        m_SocketManager.Write("\"");
        foreach (char chr in token)
          if (chr == '"')
            m_SocketManager.Write("\\\"");
          else
            m_SocketManager.Write(chr);
      }
      else
        m_SocketManager.Write(token);
    }

    private MpdResponse readResponse()
    {
      List<string> ret = new List<string>();
      string line = m_SocketManager.ReadLine();
      while (line != null && !(line.Equals(OK) || line.StartsWith(ACK))) {
        ret.Add(line);
        line = m_SocketManager.ReadLine();
      }
      if (line == null)
        line = string.Empty;

      if (line.Equals(OK))
        return new MpdResponse(new ReadOnlyCollection<string>(ret));
      else {
        Match match = ACK_REGEX.Match(line);

        if (match.Groups.Count != 5)
          throw new InvalidDataException("Error response not as expected");

        return new MpdResponse(
            int.Parse(match.Result("${code}")),
            int.Parse(match.Result("${nr}")),
            match.Result("${command}"),
            match.Result("${message}"),
            new ReadOnlyCollection<string>(ret)
            );
      }
    }

    private void ClearConnectionFields()
    {
      m_SocketManager = null;
      this.version = null;
    }
  }
}
