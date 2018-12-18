/***********************************************************
 * This file is a part of TinyOPDS server project
 * 
 * Copyright (c) 2013 SeNSSoFT
 *
 * This code is licensed under the Microsoft Public License, 
 * see http://tinyopds.codeplex.com/license for the details.
 *
 * This module contains simple HTTP processor implementation
 * and abstract class for HTTP server
 * Also, couple additional service classes are specified
 * 
 * 
 ************************************************************/

using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.ComponentModel;

namespace TinyOPDS.Server
{
    /// <summary>
    /// Basic credentials (unencrypted)
    /// </summary>
    public class Credential
    {
        public string User { get; set; }
        public string Password { get; set; }
        public Credential(string user, string password) { User = user; Password = password; }
    }

    /// <summary>
    /// Simple HTTP processor
    /// </summary>
    public class HttpProcessor : IDisposable
    {
        public TcpClient Socket;        
        public HttpServer Server;

        private Stream _inputStream;
        public StreamWriter OutputStream;

        public String HttpMethod;
        public String HttpUrl;
        public String HttpProtocolVersion;
        public Hashtable HttpHeaders = new Hashtable();

        public static BindingList<Credential> Credentials = new BindingList<Credential>();
        public static List<string> AuthorizedClients = new List<string>();
        public static Dictionary<string, int> BannedClients = new Dictionary<string, int>();

        // Maximum post size, 1 Mb
        private const int MAX_POST_SIZE = 1024 * 1024;

        // Output buffer size, 64 Kb max
        private const int OUTPUT_BUFFER_SIZE = 1024 * 1024;

        private bool _disposed = false;

        public HttpProcessor(TcpClient socket, HttpServer server)
        {
            this.Socket = socket;
            this.Server = server;                   
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            // Check to see if Dispose has already been called.

            if (!this._disposed)
            {
                if (disposing)
                {
                    if (OutputStream != null) OutputStream.Dispose();
                    if (_inputStream != null) _inputStream.Dispose();
                }
                _disposed = true;
            }
        }

        private string StreamReadLine(Stream inputStream) 
        {
            int next_char = -1;
            string data = string.Empty;
            if (inputStream.CanRead)
            {
                while (true)
                {
                    try { next_char = inputStream.ReadByte(); } catch { break; }
                    if (next_char == '\n') { break; }
                    if (next_char == '\r') { continue; }
                    if (next_char == -1) { Thread.Sleep(10); continue; };
                    data += Convert.ToChar(next_char);
                }
            }
            return data;
        }

        public void Process(object param) 
        {                        
            // We can't use a StreamReader for input, because it buffers up extra data on us inside it's
            // "processed" view of the world, and we want the data raw after the headers
            _inputStream = new BufferedStream(Socket.GetStream());

            if (ParseRequest())
            {
                // We probably shouldn't be using a StreamWriter for all output from handlers either
                OutputStream = new StreamWriter(new BufferedStream(Socket.GetStream(), OUTPUT_BUFFER_SIZE));
                OutputStream.AutoFlush = true;

                try
                {
                    ReadHeaders();

                    bool authorized = true;
                    bool checkLogin = true;

                    // Compute client hash string based on User-Agent + IP address
                    string clientHash = string.Empty;
                    if (HttpHeaders.ContainsKey("User-Agent")) clientHash += HttpHeaders["User-Agent"];
                    string remoteIP = (Socket.Client.RemoteEndPoint as IPEndPoint).Address.ToString();
                    clientHash += remoteIP;
                    clientHash = Utils.CreateGuid(Utils.IsoOidNamespace, clientHash).ToString();

                    if (Properties.Settings.Default.UseHTTPAuth)
                    {
                        authorized = false;

                        // Is remote IP banned?
                        if (Properties.Settings.Default.BanClients)
                        {
                            if (BannedClients.ContainsKey(remoteIP) && BannedClients[remoteIP] >= Properties.Settings.Default.WrongAttemptsCount)
                            {
                                checkLogin = false;
                            }
                        }

                        if (checkLogin)
                        {
                            // First, check authorized client list (if enabled)
                            if (Properties.Settings.Default.RememberClients)
                            {
                                if (AuthorizedClients.Contains(clientHash))
                                {
                                    authorized = true;
                                }
                            }

                            if (!authorized && HttpHeaders.ContainsKey("Authorization"))
                            {
                                string hash = HttpHeaders["Authorization"].ToString();
                                if (hash.StartsWith("Basic "))
                                {
                                    try
                                    {
                                        string[] credential = hash.Substring(6).DecodeFromBase64().Split(':');
                                        if (credential.Length == 2)
                                        {
                                            foreach (Credential cred in Credentials)
                                                if (cred.User.Equals(credential[0]))
                                                {
                                                    authorized = cred.Password.Equals(credential[1]);
                                                    if (authorized)
                                                    {
                                                        AuthorizedClients.Add(clientHash);
                                                        HttpServer.ServerStatistics.SuccessfulLoginAttempts++;
                                                    }
                                                    break;
                                                }
                                            if (!authorized)
                                                Log.WriteLine(LogLevel.Warning, "Authentication failed! IP: {0} user: {1} pass: {2}", remoteIP, credential[0], credential[1]);
                                        }
                                    }
                                    catch (Exception e)
                                    {
                                        Log.WriteLine(LogLevel.Error, "Authentication exception: IP: {0}, {1}", remoteIP, e.Message);
                                    }
                                }
                            }
                        }
                    }

                    if (authorized)
                    {
                        HttpServer.ServerStatistics.AddClient(clientHash);

                        if (HttpMethod.Equals("GET"))
                        {
                            HttpServer.ServerStatistics.GetRequests++;
                            HandleGETRequest();
                        }
                        else if (HttpMethod.Equals("POST"))
                        {
                            HttpServer.ServerStatistics.PostRequests++;
                            HandlePOSTRequest();
                        }
                    }
                    else
                    {
                        if (Properties.Settings.Default.BanClients)
                        {
                            if (!BannedClients.ContainsKey(remoteIP)) BannedClients[remoteIP] = 0;
                            BannedClients[remoteIP]++;
                            if (!checkLogin)
                            {
                                Log.WriteLine(LogLevel.Warning, "IP address {0} is banned!", remoteIP);
                                WriteForbidden();
                            }
                        }
                        if (checkLogin)
                        {
                            HttpServer.ServerStatistics.WrongLoginAttempts++;
                            WriteNotAuthorized();
                        }
                    }
                }
                catch (Exception e)
                {
                    Log.WriteLine(LogLevel.Error, ".Process(object param) exception: {0}", e.Message);
                    WriteFailure();
                }
            }

            try
            {
                if (OutputStream != null && OutputStream.BaseStream.CanWrite)
                {
                    try
                    {
                        OutputStream.Flush();
                    }
                    catch (Exception e)
                    {
                        Log.WriteLine(LogLevel.Error, ".Process(object param): outputStream.Flush() exception: {0}", e.Message);
                    }
                }
            }
            finally
            {
                Socket.Close();
                _inputStream = null;
                OutputStream = null;
                Socket = null;
            }
        }

        public bool ParseRequest() 
        {
            String request = StreamReadLine(_inputStream);
            if (string.IsNullOrEmpty(request)) return false;
            string[] tokens = request.Split(' ');
            if (tokens.Length != 3) return false;
            HttpMethod = tokens[0].ToUpper();
            HttpUrl = tokens[1];
            HttpProtocolVersion = tokens[2];
            return true;
        }

        public void ReadHeaders() 
        {
            string line = string.Empty;
            while ((line = StreamReadLine(_inputStream)) != null) 
            {
                if (string.IsNullOrEmpty(line)) return;
                
                int separator = line.IndexOf(':');
                if (separator == -1) 
                {
                    throw new Exception("ReadHeaders(): invalid HTTP header line: " + line);
                }
                String name = line.Substring(0, separator);
                int pos = separator + 1;
                // strip spaces
                while ((pos < line.Length) && (line[pos] == ' ')) pos++; 
                    
                string value = line.Substring(pos, line.Length - pos);
                HttpHeaders[name] = value;
            }
        }

        public void HandleGETRequest() 
        {
            Server.HandleGETRequest(this);
        }

        private const int BUF_SIZE = 1024;
        public void HandlePOSTRequest()
        {
            int content_len = 0;
            MemoryStream memStream = null;

            try
            {
                memStream = new MemoryStream();
                if (this.HttpHeaders.ContainsKey("Content-Length"))
                {
                    content_len = Convert.ToInt32(this.HttpHeaders["Content-Length"]);
                    if (content_len > MAX_POST_SIZE)
                    {
                        throw new Exception(String.Format("POST Content-Length({0}) too big for this simple server", content_len));
                    }
                    byte[] buf = new byte[BUF_SIZE];
                    int to_read = content_len;
                    while (to_read > 0)
                    {
                        int numread = this._inputStream.Read(buf, 0, Math.Min(BUF_SIZE, to_read));
                        if (numread == 0)
                        {
                            if (to_read == 0) break;
                            else throw new Exception("Client disconnected during post");
                        }
                        to_read -= numread;
                        memStream.Write(buf, 0, numread);
                    }
                    memStream.Seek(0, SeekOrigin.Begin);
                }
                using (StreamReader reader = new StreamReader(memStream))
                {
                    memStream = null;
                    Server.HandlePOSTRequest(this, reader);
                }
            }
            finally
            {
                if (memStream != null) memStream.Dispose();
            }
        }

        public void WriteSuccess(string contentType = "text/xml", bool isGZip = false) 
        {
            try
            {
                OutputStream.Write("HTTP/1.1 200 OK\n");
                OutputStream.Write("Content-Type: " + contentType + "\n");
                if (isGZip) OutputStream.Write("Content-Encoding: gzip\n");
                OutputStream.Write("Connection: close\n\n");
            }
            catch (Exception e)
            {
                Log.WriteLine(LogLevel.Error, ".WriteSuccess() exception: {0}", e.Message);
            }
        }

        public void WriteFailure() 
        {
            try
            {
                OutputStream.Write("HTTP/1.1 404 Bad request\n");
                OutputStream.Write("Connection: close\n\n");
            }
            catch (Exception e)
            {
                Log.WriteLine(LogLevel.Error, ".WriteFailure() exception: {0}", e.Message);
            }
        }

        public void WriteNotAuthorized()
        {
            try
            {
                OutputStream.Write("HTTP/1.1 401 Unauthorized\n");
                OutputStream.Write("WWW-Authenticate: Basic realm=TinyOPDS\n");
                OutputStream.Write("Connection: close\n\n");
            }
            catch (Exception e)
            {
                Log.WriteLine(LogLevel.Error, ".WriteNotAuthorized() exception: {0}", e.Message);
            }
        }

        public void WriteForbidden()
        {
            try
            {
                OutputStream.Write("HTTP/1.1 403 Forbidden\n");
                OutputStream.Write("Connection: close\n\n");
            }
            catch (Exception e)
            {
                Log.WriteLine(LogLevel.Error, ".WriteForbidden() exception: {0}", e.Message);
            }
        }
    }

    /// <summary>
    /// Server statistics class
    /// </summary>
    public class Statistics
    {
        public event EventHandler StatisticsUpdated;
        private int _booksSent  = 0;
        private int _imagesSent = 0;
        private int _getRequests  = 0;
        private int _postRequests  = 0;
        private int _successfulLoginAttempts = 0;
        private int _wrongLoginAttempts = 0;
        public int BooksSent { get { return _booksSent; } set { _booksSent = value; if (StatisticsUpdated != null) StatisticsUpdated(this, null); } }
        public int ImagesSent { get { return _imagesSent; } set { _imagesSent = value; if (StatisticsUpdated != null) StatisticsUpdated(this, null); } }
        public int GetRequests { get { return _getRequests; } set { _getRequests = value; if (StatisticsUpdated != null) StatisticsUpdated(this, null); } }
        public int PostRequests { get { return _postRequests; } set { _postRequests = value; if (StatisticsUpdated != null) StatisticsUpdated(this, null); } }
        public int SuccessfulLoginAttempts { get { return _successfulLoginAttempts; } set { _successfulLoginAttempts = value; if (StatisticsUpdated != null) StatisticsUpdated(this, null); } }
        public int WrongLoginAttempts { get { return _wrongLoginAttempts; } set { _wrongLoginAttempts = value; if (StatisticsUpdated != null) StatisticsUpdated(this, null); } }
        public int UniqueClientsCount { get { return _uniqueClients.Count; } }
        public int BannedClientsCount { get { return HttpProcessor.BannedClients.Count(сlient => сlient.Value >= Properties.Settings.Default.WrongAttemptsCount); } }
        public void AddClient(string newClient) { _uniqueClients[newClient] = true; }
        private Dictionary<string, bool> _uniqueClients = new Dictionary<string, bool>();
        public void Clear()
        {
            _booksSent = _imagesSent = _getRequests = _postRequests = _successfulLoginAttempts = _wrongLoginAttempts = 0;
            _uniqueClients.Clear();
            if (StatisticsUpdated != null) StatisticsUpdated(this, null);
        }
    }

    /// <summary>
    /// Simple HTTP server
    /// </summary>
    public abstract class HttpServer
    {
        protected int _port;
        protected int _timeout;
        protected IPAddress _interfaceIP = IPAddress.Any;
        TcpListener _listener;
        internal bool _isActive = false;
        public bool IsActive { get { return _isActive; } }
        public Exception ServerException = null;
        public AutoResetEvent ServerReady = null;
        public static Statistics ServerStatistics = new Statistics();

        public HttpServer(int Port, int Timeout = 10000)
        {
            _port = Port;
            _timeout = Timeout;
            ServerReady = new AutoResetEvent(false);
            ServerStatistics.Clear();
        }

        public HttpServer(IPAddress InterfaceIP, int Port, int Timeout = 10000) 
        {
            _interfaceIP = InterfaceIP;
            _port = Port;
            _timeout = Timeout;
            ServerReady = new AutoResetEvent(false);
            ServerStatistics.Clear();
        }

        ~HttpServer()
        {
            StopServer();
        }

        public virtual void StopServer()
        {
            _isActive = false;
            if (_listener != null)
            {
                _listener.Stop();
                _listener = null;
            }
            if (ServerReady != null)
            {
                ServerReady.Dispose();
                ServerReady = null;
            }
        }

        /// <summary>
        /// Server listener
        /// </summary>
        public void Listen() 
        {
            HttpProcessor processor = null;
            ServerException = null;
            try
            {
                _listener = new TcpListener(_interfaceIP, _port);
                _listener.Start();
                _isActive = true;
                ServerReady.Set();
                while (_isActive)
                {
                    if (_listener.Pending())
                    {
                        TcpClient socket = _listener.AcceptTcpClient();
                        socket.SendTimeout = socket.ReceiveTimeout = _timeout;
                        socket.SendBufferSize = 1024 * 1024;
                        socket.NoDelay = true;
                        processor = new HttpProcessor(socket, this);
                        ThreadPool.QueueUserWorkItem(new WaitCallback(processor.Process));
                    }
                    else Thread.Sleep(10);
                }
            }
            catch (Exception e)
            {
                Log.WriteLine(LogLevel.Error, ".Listen() exception: {0}", e.Message);
                ServerException = e;
                _isActive = false;
                ServerReady.Set();
            }
            finally
            {
                if (processor != null) processor.Dispose();
                _isActive = false;
            }
        }

        /// <summary>
        /// Abstract method to handle GET request
        /// </summary>
        /// <param name="p"></param>
        public abstract void HandleGETRequest(HttpProcessor processor);

        /// <summary>
        /// Abstract method to handle POST request
        /// </summary>
        /// <param name="p"></param>
        /// <param name="inputData"></param>
        public abstract void HandlePOSTRequest(HttpProcessor processor, StreamReader inputData);
    }
}
