/***********************************************************
 * This file is a part of TinyOPDS server project
 * 
 * Copyright (c) 2013 SeNSSoFT
 *
 * This code is licensed under the Microsoft Public License, 
 * see http://tinyopds.codeplex.com/license for the details.
 *
 * This module contains improved HTTP processor implementation
 * and abstract class for HTTP server with enhanced stability
 * 
 ************************************************************/

using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Concurrent;
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
    /// Improved HTTP processor with better error handling and thread safety
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

        // Thread-safe collections for better concurrent access
        public static BindingList<Credential> Credentials = new BindingList<Credential>();
        public static ConcurrentBag<string> AuthorizedClients = new ConcurrentBag<string>();
        public static ConcurrentDictionary<string, int> BannedClients = new ConcurrentDictionary<string, int>();

        // Improved constants
        private const int MAX_POST_SIZE = 1024 * 1024; // 1 MB
        private const int OUTPUT_BUFFER_SIZE = 1024 * 1024; // 1 MB
        private const int INPUT_BUFFER_SIZE = 64 * 1024; // 64 KB
        private const int MAX_HEADER_SIZE = 8192; // 8 KB
        private const int READ_TIMEOUT_MS = 30000; // 30 seconds

        private volatile bool _disposed = false;
        private readonly object _disposeLock = new object();

        public HttpProcessor(TcpClient socket, HttpServer server)
        {
            this.Socket = socket;
            this.Server = server;

            // Configure socket for better performance and stability
            if (socket != null && socket.Connected)
            {
                try
                {
                    socket.ReceiveTimeout = READ_TIMEOUT_MS;
                    socket.SendTimeout = READ_TIMEOUT_MS;
                    socket.ReceiveBufferSize = INPUT_BUFFER_SIZE;
                    socket.SendBufferSize = OUTPUT_BUFFER_SIZE;
                    socket.NoDelay = true;
                }
                catch (Exception ex)
                {
                    Log.WriteLine(LogLevel.Warning, "Failed to configure socket: {0}", ex.Message);
                }
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed) return;

            lock (_disposeLock)
            {
                if (_disposed) return;

                if (disposing)
                {
                    try
                    {
                        // Safely dispose OutputStream
                        if (OutputStream != null)
                        {
                            try
                            {
                                if (OutputStream.BaseStream != null && OutputStream.BaseStream.CanWrite)
                                {
                                    OutputStream.Flush();
                                }
                            }
                            catch (Exception ex)
                            {
                                Log.WriteLine(LogLevel.Warning, "Error flushing OutputStream: {0}", ex.Message);
                            }

                            try
                            {
                                OutputStream.Dispose();
                            }
                            catch (Exception ex)
                            {
                                Log.WriteLine(LogLevel.Warning, "Error disposing OutputStream: {0}", ex.Message);
                            }
                            OutputStream = null;
                        }

                        // Safely dispose InputStream
                        if (_inputStream != null)
                        {
                            try
                            {
                                _inputStream.Dispose();
                            }
                            catch (Exception ex)
                            {
                                Log.WriteLine(LogLevel.Warning, "Error disposing InputStream: {0}", ex.Message);
                            }
                            _inputStream = null;
                        }

                        // Safely close socket
                        if (Socket != null)
                        {
                            try
                            {
                                if (Socket.Connected)
                                {
                                    Socket.GetStream()?.Close();
                                }
                                Socket.Close();
                            }
                            catch (Exception ex)
                            {
                                Log.WriteLine(LogLevel.Warning, "Error closing socket: {0}", ex.Message);
                            }
                            Socket = null;
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.WriteLine(LogLevel.Error, "Error in HttpProcessor.Dispose: {0}", ex.Message);
                    }
                }
                _disposed = true;
            }
        }

        private string StreamReadLine(Stream inputStream)
        {
            if (inputStream == null || !inputStream.CanRead || _disposed)
                return null;

            int next_char = -1;
            string data = string.Empty;
            int totalBytes = 0;

            try
            {
                while (totalBytes < MAX_HEADER_SIZE)
                {
                    try
                    {
                        next_char = inputStream.ReadByte();
                    }
                    catch (Exception ex)
                    {
                        Log.WriteLine(LogLevel.Warning, "StreamReadLine ReadByte error: {0}", ex.Message);
                        break;
                    }

                    if (next_char == '\n') { break; }
                    if (next_char == '\r') { continue; }
                    if (next_char == -1)
                    {
                        if (totalBytes == 0)
                        {
                            Thread.Sleep(10);
                            continue;
                        }
                        break;
                    }

                    data += Convert.ToChar(next_char);
                    totalBytes++;
                }
            }
            catch (Exception ex)
            {
                Log.WriteLine(LogLevel.Error, "StreamReadLine error: {0}", ex.Message);
                return null;
            }

            return data;
        }

        public void Process(object param)
        {
            if (_disposed) return;

            try
            {
                // Enhanced input stream with better buffering
                _inputStream = new BufferedStream(Socket.GetStream(), INPUT_BUFFER_SIZE);

                if (ParseRequest())
                {
                    // Keep original output stream creation for compatibility
                    OutputStream = new StreamWriter(new BufferedStream(Socket.GetStream(), OUTPUT_BUFFER_SIZE));
                    OutputStream.AutoFlush = true;

                    try
                    {
                        ReadHeaders();

                        bool authorized = true;
                        bool checkLogin = true;

                        // Enhanced client identification with better error handling
                        string clientHash = string.Empty;
                        string remoteIP = "unknown";

                        try
                        {
                            if (HttpHeaders.ContainsKey("User-Agent"))
                                clientHash += HttpHeaders["User-Agent"]?.ToString() ?? "";

                            if (Socket?.Client?.RemoteEndPoint is IPEndPoint remoteEndPoint)
                            {
                                remoteIP = remoteEndPoint.Address.ToString();
                            }

                            clientHash += remoteIP;
                            clientHash = Utils.CreateGuid(Utils.IsoOidNamespace, clientHash).ToString();
                        }
                        catch (Exception ex)
                        {
                            Log.WriteLine(LogLevel.Warning, "Error generating client hash: {0}", ex.Message);
                            clientHash = Guid.NewGuid().ToString(); // Fallback
                        }

                        if (TinyOPDS.Properties.Settings.Default.UseHTTPAuth)
                        {
                            authorized = false;

                            // Check if remote IP is banned (thread-safe)
                            if (TinyOPDS.Properties.Settings.Default.BanClients)
                            {
                                if (BannedClients.TryGetValue(remoteIP, out int attempts) &&
                                    attempts >= TinyOPDS.Properties.Settings.Default.WrongAttemptsCount)
                                {
                                    checkLogin = false;
                                }
                            }

                            if (checkLogin)
                            {
                                // Check authorized client list (if enabled)
                                if (TinyOPDS.Properties.Settings.Default.RememberClients)
                                {
                                    if (AuthorizedClients.Contains(clientHash))
                                    {
                                        authorized = true;
                                    }
                                }

                                if (!authorized && HttpHeaders.ContainsKey("Authorization"))
                                {
                                    authorized = ProcessBasicAuth(clientHash, remoteIP);
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
                            else
                            {
                                WriteMethodNotAllowed();
                            }
                        }
                        else
                        {
                            HandleUnauthorizedRequest(remoteIP, checkLogin);
                        }
                    }
                    catch (Exception e)
                    {
                        Log.WriteLine(LogLevel.Error, "Process() request handling exception: {0}", e.Message);
                        WriteFailure();
                    }
                }
                else
                {
                    Log.WriteLine(LogLevel.Warning, "Failed to parse request");
                    if (OutputStream != null)
                    {
                        WriteBadRequest();
                    }
                }
            }
            catch (Exception e)
            {
                Log.WriteLine(LogLevel.Error, "Process() exception: {0}", e.Message);
                try
                {
                    if (OutputStream != null)
                    {
                        WriteFailure();
                    }
                }
                catch (Exception ex)
                {
                    Log.WriteLine(LogLevel.Error, "Error writing failure response: {0}", ex.Message);
                }
            }
            finally
            {
                // Enhanced cleanup with better error handling
                SafeCleanup();
            }
        }

        private bool ProcessBasicAuth(string clientHash, string remoteIP)
        {
            try
            {
                string hash = HttpHeaders["Authorization"]?.ToString();
                if (string.IsNullOrEmpty(hash) || !hash.StartsWith("Basic "))
                    return false;

                string[] credential = hash.Substring(6).DecodeFromBase64().Split(':');

                if (credential.Length == 2)
                {
                    string user = credential[0];
                    string password = credential[1];

                    // Thread-safe credential checking
                    lock (Credentials)
                    {
                        foreach (Credential cred in Credentials)
                        {
                            if (cred.User.Equals(user))
                            {
                                bool authorized = cred.Password.Equals(password);
                                if (authorized)
                                {
                                    AuthorizedClients.Add(clientHash);
                                    HttpServer.ServerStatistics.SuccessfulLoginAttempts++;
                                    Log.WriteLine(LogLevel.Authentication, "User {0} from {1} successfully logged in", user, remoteIP);
                                }
                                else
                                {
                                    Log.WriteLine(LogLevel.Authentication, "Authentication failed! IP: {0} user: {1}", remoteIP, user);
                                }
                                return authorized;
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Log.WriteLine(LogLevel.Authentication, "Authentication exception: IP: {0}, {1}", remoteIP, e.Message);
            }

            return false;
        }

        private void HandleUnauthorizedRequest(string remoteIP, bool checkLogin)
        {
            if (TinyOPDS.Properties.Settings.Default.BanClients)
            {
                // Thread-safe increment of banned attempts
                BannedClients.AddOrUpdate(remoteIP, 1, (key, value) => value + 1);

                if (!checkLogin)
                {
                    Log.WriteLine(LogLevel.Authentication, "IP address {0} is banned!", remoteIP);
                    WriteForbidden();
                    return;
                }
            }

            if (checkLogin)
            {
                HttpServer.ServerStatistics.WrongLoginAttempts++;
                WriteNotAuthorized();
            }
        }

        private void SafeCleanup()
        {
            try
            {
                if (OutputStream != null && OutputStream.BaseStream != null && OutputStream.BaseStream.CanWrite)
                {
                    try
                    {
                        OutputStream.Flush();
                    }
                    catch (Exception e)
                    {
                        Log.WriteLine(LogLevel.Error, "SafeCleanup flush exception: {0}", e.Message);
                    }
                }
            }
            catch (Exception e)
            {
                Log.WriteLine(LogLevel.Error, "SafeCleanup exception: {0}", e.Message);
            }
            finally
            {
                try
                {
                    Socket?.Close();
                }
                catch (Exception e)
                {
                    Log.WriteLine(LogLevel.Error, "SafeCleanup socket close exception: {0}", e.Message);
                }

                _inputStream = null;
                OutputStream = null;
                Socket = null;
            }
        }

        public bool ParseRequest()
        {
            try
            {
                String request = StreamReadLine(_inputStream);
                if (string.IsNullOrEmpty(request)) return false;

                string[] tokens = request.Split(' ');
                if (tokens.Length != 3) return false;

                HttpMethod = tokens[0].ToUpper();
                HttpUrl = tokens[1];
                HttpProtocolVersion = tokens[2];

                // Basic validation
                if (string.IsNullOrEmpty(HttpUrl) || HttpUrl.Length > 2048)
                    return false;

                return true;
            }
            catch (Exception ex)
            {
                Log.WriteLine(LogLevel.Error, "ParseRequest error: {0}", ex.Message);
                return false;
            }
        }

        public void ReadHeaders()
        {
            int totalHeaderSize = 0;
            int headerCount = 0;
            const int MAX_HEADERS = 100;

            try
            {
                string line;
                while ((line = StreamReadLine(_inputStream)) != null)
                {
                    totalHeaderSize += line.Length + 2; // +2 for CRLF
                    headerCount++;

                    if (totalHeaderSize > MAX_HEADER_SIZE)
                    {
                        Log.WriteLine(LogLevel.Warning, "Headers too large, truncating");
                        break;
                    }

                    if (headerCount > MAX_HEADERS)
                    {
                        Log.WriteLine(LogLevel.Warning, "Too many headers, truncating");
                        break;
                    }

                    if (string.IsNullOrEmpty(line)) return;

                    int separator = line.IndexOf(':');
                    if (separator == -1)
                    {
                        Log.WriteLine(LogLevel.Warning, "Invalid HTTP header line: {0}", line);
                        continue; // Skip invalid headers instead of throwing
                    }

                    String name = line.Substring(0, separator);
                    int pos = separator + 1;

                    // Strip spaces
                    while ((pos < line.Length) && (line[pos] == ' ')) pos++;

                    string value = line.Substring(pos, line.Length - pos);
                    HttpHeaders[name] = value;
                }
            }
            catch (Exception ex)
            {
                Log.WriteLine(LogLevel.Error, "ReadHeaders error: {0}", ex.Message);
                throw;
            }
        }

        public void HandleGETRequest()
        {
            Server?.HandleGETRequest(this);
        }

        private const int BUF_SIZE = 4096; // Increased buffer size
        public void HandlePOSTRequest()
        {
            int content_len = 0;
            MemoryStream memStream = null;

            try
            {
                memStream = new MemoryStream();
                if (this.HttpHeaders.ContainsKey("Content-Length"))
                {
                    if (!int.TryParse(this.HttpHeaders["Content-Length"]?.ToString(), out content_len) || content_len < 0)
                    {
                        throw new InvalidOperationException("Invalid Content-Length header");
                    }

                    if (content_len > MAX_POST_SIZE)
                    {
                        throw new InvalidOperationException(String.Format("POST Content-Length({0}) too big for this server (max: {1})", content_len, MAX_POST_SIZE));
                    }

                    byte[] buf = new byte[BUF_SIZE];
                    int to_read = content_len;
                    int totalRead = 0;

                    while (to_read > 0)
                    {
                        int numread = this._inputStream.Read(buf, 0, Math.Min(BUF_SIZE, to_read));
                        if (numread == 0)
                        {
                            if (totalRead < content_len)
                                throw new InvalidOperationException("Client disconnected during POST");
                            break;
                        }
                        to_read -= numread;
                        totalRead += numread;
                        memStream.Write(buf, 0, numread);
                    }
                    memStream.Seek(0, SeekOrigin.Begin);
                }

                using (StreamReader reader = new StreamReader(memStream))
                {
                    memStream = null;
                    Server?.HandlePOSTRequest(this, reader);
                }
            }
            catch (Exception ex)
            {
                Log.WriteLine(LogLevel.Error, "HandlePOSTRequest error: {0}", ex.Message);
                WriteFailure();
            }
            finally
            {
                memStream?.Dispose();
            }
        }

        // Original HTTP response methods for compatibility
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
                Log.WriteLine(LogLevel.Error, "WriteSuccess() exception: {0}", e.Message);
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
                Log.WriteLine(LogLevel.Error, "WriteFailure() exception: {0}", e.Message);
            }
        }

        public void WriteBadRequest()
        {
            try
            {
                OutputStream.Write("HTTP/1.1 400 Bad Request\n");
                OutputStream.Write("Connection: close\n\n");
            }
            catch (Exception e)
            {
                Log.WriteLine(LogLevel.Error, "WriteBadRequest() exception: {0}", e.Message);
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
                Log.WriteLine(LogLevel.Error, "WriteNotAuthorized() exception: {0}", e.Message);
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
                Log.WriteLine(LogLevel.Error, "WriteForbidden() exception: {0}", e.Message);
            }
        }

        public void WriteMethodNotAllowed()
        {
            try
            {
                OutputStream.Write("HTTP/1.1 405 Method Not Allowed\n");
                OutputStream.Write("Connection: close\n\n");
            }
            catch (Exception e)
            {
                Log.WriteLine(LogLevel.Error, "WriteMethodNotAllowed() exception: {0}", e.Message);
            }
        }
    }

    /// <summary>
    /// Enhanced server statistics class with better thread safety
    /// </summary>
    public class Statistics
    {
        public event EventHandler StatisticsUpdated;

        private volatile int _booksSent = 0;
        private volatile int _imagesSent = 0;
        private volatile int _getRequests = 0;
        private volatile int _postRequests = 0;
        private volatile int _successfulLoginAttempts = 0;
        private volatile int _wrongLoginAttempts = 0;

        // Thread-safe unique clients tracking
        private readonly ConcurrentDictionary<string, bool> _uniqueClients = new ConcurrentDictionary<string, bool>();

        public int BooksSent
        {
            get { return _booksSent; }
            set { _booksSent = value; OnStatisticsUpdated(); }
        }

        public int ImagesSent
        {
            get { return _imagesSent; }
            set { _imagesSent = value; OnStatisticsUpdated(); }
        }

        public int GetRequests
        {
            get { return _getRequests; }
            set { _getRequests = value; OnStatisticsUpdated(); }
        }

        public int PostRequests
        {
            get { return _postRequests; }
            set { _postRequests = value; OnStatisticsUpdated(); }
        }

        public int SuccessfulLoginAttempts
        {
            get { return _successfulLoginAttempts; }
            set { _successfulLoginAttempts = value; OnStatisticsUpdated(); }
        }

        public int WrongLoginAttempts
        {
            get { return _wrongLoginAttempts; }
            set { _wrongLoginAttempts = value; OnStatisticsUpdated(); }
        }

        public int UniqueClientsCount
        {
            get { return _uniqueClients.Count; }
        }

        public int BannedClientsCount
        {
            get
            {
                return HttpProcessor.BannedClients.Count(client =>
                    client.Value >= TinyOPDS.Properties.Settings.Default.WrongAttemptsCount);
            }
        }

        // Thread-safe increment methods for high-load scenarios
        public void IncrementBooksSent()
        {
            Interlocked.Increment(ref _booksSent);
            OnStatisticsUpdated();
        }

        public void IncrementImagesSent()
        {
            Interlocked.Increment(ref _imagesSent);
            OnStatisticsUpdated();
        }

        public void AddClient(string newClient)
        {
            if (!string.IsNullOrEmpty(newClient))
            {
                _uniqueClients.TryAdd(newClient, true);
            }
        }

        public void Clear()
        {
            _booksSent = _imagesSent = _getRequests = _postRequests = _successfulLoginAttempts = _wrongLoginAttempts = 0;
            _uniqueClients.Clear();
            OnStatisticsUpdated();
        }

        private void OnStatisticsUpdated()
        {
            try
            {
                StatisticsUpdated?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                Log.WriteLine(LogLevel.Error, "StatisticsUpdated event error: {0}", ex.Message);
            }
        }
    }

    /// <summary>
    /// Enhanced HTTP server with improved stability and error handling
    /// </summary>
    public abstract class HttpServer
    {
        protected int _port;
        protected int _timeout;
        protected IPAddress _interfaceIP = IPAddress.Any;
        private TcpListener _listener;
        internal volatile bool _isActive = false;

        public bool IsActive { get { return _isActive; } }
        public Exception ServerException = null;
        public AutoResetEvent ServerReady = null;
        public static Statistics ServerStatistics = new Statistics();

        private volatile bool _isIdle = true;
        private TimeSpan _idleTimeout = TimeSpan.FromMinutes(10);
        public bool IsIdle { get { return _isIdle; } }

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
                try
                {
                    _listener.Stop();
                }
                catch (Exception ex)
                {
                    Log.WriteLine(LogLevel.Error, "Error stopping listener: {0}", ex.Message);
                }
                _listener = null;
            }

            if (ServerReady != null)
            {
                try
                {
                    ServerReady.Dispose();
                }
                catch { }
                ServerReady = null;
            }
        }

        /// <summary>
        /// Enhanced server listener with better connection handling
        /// </summary>
        public void Listen()
        {
            DateTime requestTime = DateTime.Now;
            int loopCount = 0;
            ServerException = null;

            try
            {
                _listener = new TcpListener(_interfaceIP, _port);
                _listener.Start();
                _isActive = true;
                ServerReady.Set();

                Log.WriteLine(LogLevel.Info, "HTTP server started on {0}:{1}", _interfaceIP, _port);

                while (_isActive)
                {
                    try
                    {
                        if (_listener.Pending())
                        {
                            TcpClient socket = _listener.AcceptTcpClient();
                            ProcessConnection(socket);

                            // Reset idle state
                            _isIdle = false;
                            requestTime = DateTime.Now;
                            loopCount = 0;
                        }
                        else
                        {
                            Thread.Sleep(100);
                        }

                        // Check the idle state once a minute
                        if (loopCount++ > 600)
                        {
                            loopCount = 0;
                            if (DateTime.Now.Subtract(requestTime) > _idleTimeout)
                                _isIdle = true;
                        }
                    }
                    catch (ObjectDisposedException)
                    {
                        // Normal shutdown
                        break;
                    }
                    catch (SocketException ex)
                    {
                        if (_isActive)
                        {
                            Log.WriteLine(LogLevel.Error, "Socket exception in Listen(): {0}", ex.Message);
                        }
                        break;
                    }
                    catch (Exception ex)
                    {
                        Log.WriteLine(LogLevel.Error, "Unexpected exception in Listen(): {0}", ex.Message);
                        // Continue listening for other connections
                    }
                }
            }
            catch (Exception e)
            {
                Log.WriteLine(LogLevel.Error, "Listen() exception: {0}", e.Message);
                ServerException = e;
                _isActive = false;
                ServerReady.Set();
            }
            finally
            {
                _isActive = false;
                Log.WriteLine(LogLevel.Info, "HTTP server stopped");
            }
        }

        private void ProcessConnection(TcpClient socket)
        {
            if (socket == null) return;

            try
            {
                // Enhanced socket configuration
                socket.SendTimeout = socket.ReceiveTimeout = _timeout;
                socket.SendBufferSize = 1024 * 1024;
                socket.ReceiveBufferSize = 64 * 1024;
                socket.NoDelay = true;

                HttpProcessor processor = new HttpProcessor(socket, this);

                // Queue work item with proper error handling
                ThreadPool.QueueUserWorkItem(new WaitCallback(SafeProcessorWrapper), processor);
            }
            catch (Exception ex)
            {
                Log.WriteLine(LogLevel.Error, "Error configuring connection: {0}", ex.Message);
                try
                {
                    socket?.Close();
                }
                catch { }
            }
        }

        private void SafeProcessorWrapper(object processorObj)
        {
            HttpProcessor processor = processorObj as HttpProcessor;
            try
            {
                processor?.Process(null);
            }
            catch (Exception ex)
            {
                Log.WriteLine(LogLevel.Error, "SafeProcessorWrapper exception: {0}", ex.Message);
            }
            finally
            {
                processor?.Dispose();
            }
        }

        /// <summary>
        /// Abstract method to handle GET request
        /// </summary>
        public abstract void HandleGETRequest(HttpProcessor processor);

        /// <summary>
        /// Abstract method to handle POST request
        /// </summary>
        public abstract void HandlePOSTRequest(HttpProcessor processor, StreamReader inputData);
    }
}