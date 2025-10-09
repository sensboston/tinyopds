/*
 * This file is part of TinyOPDS server project
 * https://github.com/sensboston/tinyopds
 *
 * Copyright (c) 2013-2025 SeNSSoFT
 * SPDX-License-Identifier: MIT
 *
 * This module contains improved HTTP processor implementation
 * and abstract class for HTTP server with enhanced stability
 * and request cancellation support
 * 
 * MODIFIED: Fixed ClientHash to use session tokens instead of User-Agent
 * FIXED: Allow anonymous access to images for OPDS clients compatibility
 * MODIFIED: Added case-insensitive HTTP headers processing
 * MODIFIED: Added persistent storage for authorized clients in Settings
 * 
 */

using System;
using System.Linq;
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
    /// Session info for better authorization persistence
    /// </summary>
    public class SessionInfo
    {
        public string Token { get; set; }
        public string IpAddress { get; set; }
        public DateTime Created { get; set; }
        public DateTime LastAccess { get; set; }
        public string Username { get; set; }

        public bool IsValid()
        {
            return (DateTime.Now - LastAccess).TotalDays < 30;
        }

        public void UpdateLastAccess()
        {
            LastAccess = DateTime.Now;
        }
    }

    /// <summary>
    /// Simple HTTP processor with client tracking
    /// </summary>
    public class HttpProcessor : IDisposable
    {
        public TcpClient Socket;
        public HttpServer Server;

        private Stream inputStream;
        public StreamWriter OutputStream;

        public string HttpMethod;
        public string HttpUrl;
        public string HttpProtocolVersion;
        public Dictionary<string, string> HttpHeaders = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public string ClientHash { get; private set; }

        public static BindingList<Credential> Credentials = new BindingList<Credential>();

        public static Dictionary<string, SessionInfo> AuthorizedSessions = new Dictionary<string, SessionInfo>();
        private static readonly object sessionLock = new object();

        public static List<string> AuthorizedClients = new List<string>();
        private static readonly object clientsLock = new object();

        public static Dictionary<string, int> BannedClients = new Dictionary<string, int>();

        private static readonly Dictionary<string, string> credentialCache = new Dictionary<string, string>();
        private static readonly object credentialCacheLock = new object();

        private const int MAX_POST_SIZE = 1024 * 64;
        private const int OUTPUT_BUFFER_SIZE = 1024 * 128;

        private bool disposed = false;

        static HttpProcessor()
        {
            if (!string.IsNullOrEmpty(Properties.Settings.Default.AuthorizedClients))
            {
                var clients = Properties.Settings.Default.AuthorizedClients.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                lock (clientsLock)
                {
                    AuthorizedClients.AddRange(clients);
                }
            }
        }

        public HttpProcessor(TcpClient socket, HttpServer server)
        {
            Socket = socket;
            Server = server;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposed)
            {
                if (disposing)
                {
                    OutputStream?.Dispose();
                    inputStream?.Dispose();
                }
                disposed = true;
            }
        }

        private string StreamReadLine(Stream inputStream)
        {
            if (!inputStream.CanRead) return string.Empty;

            var buffer = new System.Text.StringBuilder(256);
            int attempts = 0;
            const int maxAttempts = 100;

            while (attempts < maxAttempts)
            {
                int next_char;
                try
                {
                    next_char = inputStream.ReadByte();
                }
                catch
                {
                    break;
                }

                if (next_char == -1)
                {
                    Thread.Sleep(10);
                    attempts++;
                    continue;
                }

                attempts = 0;

                if (next_char == '\n') break;
                if (next_char == '\r') continue;

                buffer.Append((char)next_char);

                if (buffer.Length > 8192) break;
            }

            return buffer.ToString();
        }

        private static string GenerateSessionToken()
        {
            var random = new Random();
            var bytes = new byte[32];
            random.NextBytes(bytes);
            return Convert.ToBase64String(bytes).Replace("+", "-").Replace("/", "_").TrimEnd('=');
        }

        private string GetSessionTokenFromCookie()
        {
            if (HttpHeaders.ContainsKey("Cookie"))
            {
                var cookies = HttpHeaders["Cookie"].Split(';');
                foreach (var cookie in cookies)
                {
                    var parts = cookie.Trim().Split('=');
                    if (parts.Length == 2 && parts[0] == "TinyOPDS_Session")
                    {
                        return parts[1];
                    }
                }
            }
            return null;
        }

        private static void CleanupExpiredSessions()
        {
            lock (sessionLock)
            {
                var expiredTokens = AuthorizedSessions
                    .Where(kvp => !kvp.Value.IsValid())
                    .Select(kvp => kvp.Key)
                    .ToList();

                foreach (var token in expiredTokens)
                {
                    AuthorizedSessions.Remove(token);
                }
            }
        }

        public void Process()
        {
            try
            {
                ProcessInternal();
            }
            catch (SocketException se)
            {
                Log.WriteLine(LogLevel.Error, "Socket error in Process(): {0}", se.Message);
            }
            catch (IOException ioe)
            {
                Log.WriteLine(LogLevel.Error, "IO error in Process(): {0}", ioe.Message);
            }
            catch (Exception e)
            {
                Log.WriteLine(LogLevel.Error, "Unexpected error in Process(): {0}", e.Message);
            }
            finally
            {
                CleanupConnection();
            }
        }

        private void ProcessInternal()
        {
            inputStream = new BufferedStream(Socket.GetStream());

            if (ParseRequest())
            {
                OutputStream = new StreamWriter(new BufferedStream(Socket.GetStream(), OUTPUT_BUFFER_SIZE))
                {
                    AutoFlush = true
                };

                ReadHeaders();

                bool authorized = true;
                bool checkLogin = true;

                string remoteIP = (Socket.Client.RemoteEndPoint as IPEndPoint).Address.ToString();

                this.ClientHash = Utils.CreateGuid(Utils.IsoOidNamespace, remoteIP).ToString();

                string sessionToken = null;
                bool sendSessionCookie = false;

                if (Properties.Settings.Default.UseHTTPAuth)
                {
                    bool isImageRequest = HttpUrl.Contains("/cover/") ||
                                        HttpUrl.Contains("/thumbnail/") ||
                                        HttpUrl.EndsWith(".jpeg") ||
                                        HttpUrl.EndsWith(".jpg") ||
                                        HttpUrl.EndsWith(".png");

                    if (isImageRequest)
                    {
                        authorized = true;
                        Log.WriteLine(LogLevel.Info, "Bypassing auth for image request from {0}: {1}", remoteIP, HttpUrl);
                    }
                    else
                    {
                        authorized = false;

                        if (Properties.Settings.Default.BanClients)
                        {
                            lock (BannedClients)
                            {
                                if (BannedClients.Count > 0 && BannedClients.ContainsKey(remoteIP) &&
                                    BannedClients[remoteIP] >= TinyOPDS.Properties.Settings.Default.WrongAttemptsCount)
                                {
                                    checkLogin = false;
                                }
                            }
                        }

                        if (checkLogin)
                        {
                            if (Properties.Settings.Default.RememberClients)
                            {
                                sessionToken = GetSessionTokenFromCookie();

                                if (!string.IsNullOrEmpty(sessionToken))
                                {
                                    lock (sessionLock)
                                    {
                                        if (AuthorizedSessions.ContainsKey(sessionToken))
                                        {
                                            var session = AuthorizedSessions[sessionToken];

                                            bool ipCheck = true;

                                            if (session.IsValid() && (!ipCheck || session.IpAddress == remoteIP))
                                            {
                                                session.UpdateLastAccess();
                                                authorized = true;
                                                Log.WriteLine(LogLevel.Authentication,
                                                    "Session authenticated for user {0} from {1}",
                                                    session.Username, remoteIP);
                                            }
                                            else if (!session.IsValid())
                                            {
                                                AuthorizedSessions.Remove(sessionToken);
                                                Log.WriteLine(LogLevel.Authentication,
                                                    "Expired session removed for user {0}", session.Username);
                                            }
                                            else
                                            {
                                                Log.WriteLine(LogLevel.Authentication,
                                                    "IP mismatch for session. Expected: {0}, Got: {1}",
                                                    session.IpAddress, remoteIP);
                                            }
                                        }
                                    }
                                }

                                if (!authorized)
                                {
                                    lock (clientsLock)
                                    {
                                        if (AuthorizedClients.Contains(ClientHash))
                                        {
                                            authorized = true;
                                            sendSessionCookie = true;
                                            Log.WriteLine(LogLevel.Authentication,
                                                "Client {0} authorized from persistent storage", remoteIP);
                                        }
                                    }
                                }
                            }

                            if (!authorized && HttpHeaders.ContainsKey("Authorization"))
                            {
                                string hash = HttpHeaders["Authorization"];

                                if (hash.StartsWith("Basic "))
                                {
                                    try
                                    {
                                        string[] credential = hash.Substring(6).DecodeFromBase64().Split(':');

                                        if (credential.Length == 2)
                                        {
                                            string user = credential[0];
                                            string password = credential[1];

                                            authorized = ValidateCredentials(user, password);
                                            if (authorized)
                                            {
                                                if (Properties.Settings.Default.RememberClients)
                                                {
                                                    sessionToken = GenerateSessionToken();
                                                    var sessionInfo = new SessionInfo
                                                    {
                                                        Token = sessionToken,
                                                        IpAddress = remoteIP,
                                                        Created = DateTime.Now,
                                                        LastAccess = DateTime.Now,
                                                        Username = user
                                                    };

                                                    lock (sessionLock)
                                                    {
                                                        AuthorizedSessions[sessionToken] = sessionInfo;

                                                        if (AuthorizedSessions.Count % 100 == 0)
                                                        {
                                                            CleanupExpiredSessions();
                                                        }
                                                    }

                                                    sendSessionCookie = true;
                                                }

                                                lock (clientsLock)
                                                {
                                                    if (!AuthorizedClients.Contains(ClientHash))
                                                    {
                                                        AuthorizedClients.Add(ClientHash);
                                                        Properties.Settings.Default.AuthorizedClients = string.Join(",", AuthorizedClients);
                                                        Properties.Settings.Default.Save();
                                                    }
                                                }

                                                HttpServer.ServerStatistics.IncrementSuccessfulLoginAttempts();
                                                Log.WriteLine(LogLevel.Authentication,
                                                    "User {0} from {1} successfully logged in", user, remoteIP);
                                            }
                                            else
                                            {
                                                Log.WriteLine(LogLevel.Authentication,
                                                    "Authentication failed! IP: {0} user: {1}", remoteIP, user);
                                            }
                                        }
                                    }
                                    catch (Exception e)
                                    {
                                        Log.WriteLine(LogLevel.Authentication,
                                            "Authentication exception: IP: {0}, {1}", remoteIP, e.Message);
                                    }
                                }
                            }
                        }
                    }
                }

                if (authorized)
                {
                    HttpServer.ServerStatistics.AddClient(ClientHash);

                    if (HttpMethod.Equals("GET"))
                    {
                        HttpServer.ServerStatistics.IncrementGetRequests();

                        if (sendSessionCookie && !string.IsNullOrEmpty(sessionToken))
                        {
                            HandleGETRequestWithSession(sessionToken);
                        }
                        else
                        {
                            HandleGETRequest();
                        }
                    }
                    else if (HttpMethod.Equals("POST"))
                    {
                        HttpServer.ServerStatistics.IncrementPostRequests();
                        HandlePOSTRequest();
                    }
                }
                else
                {
                    if (Properties.Settings.Default.BanClients)
                    {
                        lock (BannedClients)
                        {
                            if (!BannedClients.ContainsKey(remoteIP)) BannedClients[remoteIP] = 0;
                            BannedClients[remoteIP]++;
                        }
                        if (!checkLogin)
                        {
                            Log.WriteLine(LogLevel.Authentication, "IP address {0} is banned!", remoteIP);
                            WriteForbidden();
                            return;
                        }
                    }
                    if (checkLogin)
                    {
                        HttpServer.ServerStatistics.IncrementWrongLoginAttempts();
                        WriteNotAuthorized();
                    }
                }
            }

            if (OutputStream != null && OutputStream.BaseStream.CanWrite)
            {
                try
                {
                    OutputStream.Flush();
                }
                catch (Exception e)
                {
                    Log.WriteLine(LogLevel.Error, "outputStream.Flush() exception: {0}", e.Message);
                }
            }
        }

        private void CleanupConnection()
        {
            try { Socket?.Close(); } catch { }
            try { inputStream?.Dispose(); OutputStream?.Dispose(); }
            catch { }
            finally
            {
                inputStream = null;
                OutputStream = null;
                Socket = null;
            }
        }

        private bool ValidateCredentials(string user, string password)
        {
            lock (credentialCacheLock)
            {
                if (credentialCache.Count != Credentials.Count)
                {
                    credentialCache.Clear();
                    foreach (Credential cred in Credentials)
                    {
                        credentialCache[cred.User] = cred.Password;
                    }
                }

                return credentialCache.ContainsKey(user) && credentialCache[user].Equals(password);
            }
        }

        public bool ParseRequest()
        {
            string request = StreamReadLine(inputStream);
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
            string line;
            while ((line = StreamReadLine(inputStream)) != null)
            {
                if (string.IsNullOrEmpty(line)) return;

                int separator = line.IndexOf(':');
                if (separator == -1)
                {
                    throw new Exception("ReadHeaders(): invalid HTTP header line: " + line);
                }
                string name = line.Substring(0, separator);
                int pos = separator + 1;
                while ((pos < line.Length) && (line[pos] == ' ')) pos++;

                string value = line.Substring(pos, line.Length - pos);
                HttpHeaders[name] = value;
            }
        }

        public void HandleGETRequest()
        {
            Server.HandleGETRequest(this);
        }

        private void HandleGETRequestWithSession(string sessionToken)
        {
            this.pendingSessionToken = sessionToken;
            Server.HandleGETRequest(this);
        }

        private string pendingSessionToken = null;

        private const int BUF_SIZE = 4096;
        public void HandlePOSTRequest()
        {
            MemoryStream memStream = null;
            StreamReader reader = null;

            try
            {
                memStream = new MemoryStream();
                if (HttpHeaders.ContainsKey("Content-Length"))
                {
                    int content_len = Convert.ToInt32(HttpHeaders["Content-Length"]);
                    if (content_len > MAX_POST_SIZE)
                    {
                        throw new Exception(String.Format("POST Content-Length({0}) too big for this server", content_len));
                    }
                    byte[] buf = new byte[BUF_SIZE];
                    int to_read = content_len;
                    while (to_read > 0)
                    {
                        int numread = inputStream.Read(buf, 0, Math.Min(BUF_SIZE, to_read));
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
                reader = new StreamReader(memStream);
                Server.HandlePOSTRequest(this, reader);
            }
            finally
            {
                reader?.Dispose();
                memStream?.Dispose();
            }
        }

        private void WriteHttpResponse(string statusLine, string additionalHeaders = null, string methodName = null, bool keepAlive = false)
        {
            try
            {
                OutputStream.Write("HTTP/1.1 " + statusLine + "\n");

                if (!string.IsNullOrEmpty(pendingSessionToken))
                {
                    OutputStream.Write($"Set-Cookie: TinyOPDS_Session={pendingSessionToken}; " +
                                     $"HttpOnly; Path=/; Max-Age=2592000\n");
                    pendingSessionToken = null;
                }

                if (!string.IsNullOrEmpty(additionalHeaders))
                {
                    OutputStream.Write(additionalHeaders + "\n");
                }

                if (keepAlive && HttpHeaders.ContainsKey("Connection") &&
                    HttpHeaders["Connection"].ToLower().Contains("keep-alive"))
                {
                    OutputStream.Write("Connection: keep-alive\n");
                    OutputStream.Write("Keep-Alive: timeout=30, max=100\n");
                }
                else
                {
                    OutputStream.Write("Connection: close\n");
                }

                OutputStream.Write("\n");
            }
            catch (Exception e)
            {
                string logMethodName = methodName ?? new System.Diagnostics.StackTrace().GetFrame(1).GetMethod().Name;
                Log.WriteLine(LogLevel.Error, "{0} exception: {1}", logMethodName, e.Message);
            }
        }

        public void WriteSuccess(string contentType = "text/xml", bool isGZip = false)
        {
            string headers = "Content-Type: " + contentType;
            if (isGZip) headers += "\nContent-Encoding: gzip";
            WriteHttpResponse("200 OK", headers, "WriteSuccess", true);
        }

        public void WriteFailure()
        {
            WriteHttpResponse("404 Bad request");
        }

        public void WriteNotAuthorized()
        {
            WriteHttpResponse("401 Unauthorized", "WWW-Authenticate: Basic realm=TinyOPDS");
        }

        public void WriteForbidden()
        {
            WriteHttpResponse("403 Forbidden");
        }

        public void WriteBadRequest()
        {
            WriteHttpResponse("400 Bad Request");
        }

        public void WriteMethodNotAllowed()
        {
            WriteHttpResponse("405 Method Not Allowed");
        }
    }

    /// <summary>
    /// Server statistics class
    /// </summary>
    public class Statistics
    {
        public event EventHandler StatisticsUpdated;
        private volatile int booksSent = 0;
        private volatile int imagesSent = 0;
        private volatile int getRequests = 0;
        private volatile int postRequests = 0;
        private volatile int successfulLoginAttempts = 0;
        private volatile int wrongLoginAttempts = 0;

        public int BooksSent { get { return booksSent; } set { booksSent = value; StatisticsUpdated?.Invoke(this, null); } }
        public int ImagesSent { get { return imagesSent; } set { imagesSent = value; StatisticsUpdated?.Invoke(this, null); } }
        public int GetRequests { get { return getRequests; } set { getRequests = value; StatisticsUpdated?.Invoke(this, null); } }
        public int PostRequests { get { return postRequests; } set { postRequests = value; StatisticsUpdated?.Invoke(this, null); } }
        public int SuccessfulLoginAttempts { get { return successfulLoginAttempts; } set { successfulLoginAttempts = value; StatisticsUpdated?.Invoke(this, null); } }
        public int WrongLoginAttempts { get { return wrongLoginAttempts; } set { wrongLoginAttempts = value; StatisticsUpdated?.Invoke(this, null); } }
        public int UniqueClientsCount { get { return uniqueClients.Count; } }
        public int BannedClientsCount { get { lock (HttpProcessor.BannedClients) { return HttpProcessor.BannedClients.Count(client => client.Value >= Properties.Settings.Default.WrongAttemptsCount); } } }

        public void AddClient(string newClient)
        {
            lock (uniqueClients)
            {
                uniqueClients[newClient] = true;
            }
        }

        private readonly Dictionary<string, bool> uniqueClients = new Dictionary<string, bool>();

        public void Clear()
        {
            booksSent = imagesSent = getRequests = postRequests = successfulLoginAttempts = wrongLoginAttempts = 0;
            lock (uniqueClients)
            {
                uniqueClients.Clear();
            }
            StatisticsUpdated?.Invoke(this, null);
        }

        public void IncrementBooksSent()
        {
            Interlocked.Increment(ref booksSent);
            StatisticsUpdated?.Invoke(this, null);
        }

        public void IncrementImagesSent()
        {
            Interlocked.Increment(ref imagesSent);
            StatisticsUpdated?.Invoke(this, null);
        }

        public void IncrementGetRequests()
        {
            Interlocked.Increment(ref getRequests);
            StatisticsUpdated?.Invoke(this, null);
        }

        public void IncrementPostRequests()
        {
            Interlocked.Increment(ref postRequests);
            StatisticsUpdated?.Invoke(this, null);
        }

        public void IncrementSuccessfulLoginAttempts()
        {
            Interlocked.Increment(ref successfulLoginAttempts);
            StatisticsUpdated?.Invoke(this, null);
        }

        public void IncrementWrongLoginAttempts()
        {
            Interlocked.Increment(ref wrongLoginAttempts);
            StatisticsUpdated?.Invoke(this, null);
        }
    }

    /// <summary>
    /// Simple HTTP server with connection pooling and enhanced error handling
    /// </summary>
    public abstract class HttpServer
    {
        protected int port;
        protected int timeout;
        protected IPAddress interfaceIP = IPAddress.Any;
        private TcpListener listener;
        internal bool isActive = false;
        public bool IsActive { get { return isActive; } }
        public Exception ServerException = null;
        public AutoResetEvent ServerReady = null;
        public static Statistics ServerStatistics = new Statistics();

        private bool isIdle = true;
        private TimeSpan idleTimeout = TimeSpan.FromMinutes(10);
        public bool IsIdle { get { return isIdle; } }

        private readonly SemaphoreSlim connectionSemaphore;
        private const int MAX_CONCURRENT_CONNECTIONS = 100;
        private const int SOCKET_BUFFER_SIZE = 1024 * 512;
        private const int IDLE_CHECK_INTERVAL = 600;

        public HttpServer(int Port, int Timeout = 10000)
        {
            port = Port;
            timeout = Timeout;
            ServerReady = new AutoResetEvent(false);
            connectionSemaphore = new SemaphoreSlim(MAX_CONCURRENT_CONNECTIONS, MAX_CONCURRENT_CONNECTIONS);
            ServerStatistics.Clear();
        }

        public HttpServer(IPAddress InterfaceIP, int Port, int Timeout = 10000)
        {
            interfaceIP = InterfaceIP;
            port = Port;
            timeout = Timeout;
            ServerReady = new AutoResetEvent(false);
            connectionSemaphore = new SemaphoreSlim(MAX_CONCURRENT_CONNECTIONS, MAX_CONCURRENT_CONNECTIONS);
            ServerStatistics.Clear();
        }

        ~HttpServer()
        {
            StopServer();
        }

        public virtual void StopServer()
        {
            isActive = false;
            if (listener != null)
            {
                listener.Stop();
                listener = null;
            }
            if (ServerReady != null)
            {
                ServerReady.Dispose();
                ServerReady = null;
            }
            connectionSemaphore?.Dispose();
        }

        /// <summary>
        /// Server listener with connection throttling
        /// </summary>
        public void Listen()
        {
            DateTime requestTime = DateTime.Now;
            int loopCount = 0;
            ServerException = null;
            try
            {
                listener = new TcpListener(interfaceIP, port);
                listener.Start();
                isActive = true;
                ServerReady.Set();

                while (isActive)
                {
                    if (listener.Pending())
                    {
                        TcpClient socket = listener.AcceptTcpClient();
                        socket.SendTimeout = socket.ReceiveTimeout = timeout;
                        socket.SendBufferSize = SOCKET_BUFFER_SIZE;
                        socket.NoDelay = true;

                        isIdle = false;
                        requestTime = DateTime.Now;
                        loopCount = 0;

                        ThreadPool.QueueUserWorkItem(ProcessConnectionWithThrottling, socket);
                    }
                    else
                    {
                        Thread.Sleep(100);
                    }

                    if (loopCount++ > IDLE_CHECK_INTERVAL)
                    {
                        loopCount = 0;
                        if (DateTime.Now.Subtract(requestTime) > idleTimeout)
                            isIdle = true;
                    }
                }
            }
            catch (Exception e)
            {
                Log.WriteLine(LogLevel.Error, ".Listen() exception: {0}", e.Message);
                ServerException = e;
                isActive = false;
                ServerReady.Set();
            }
            finally
            {
                isActive = false;
            }
        }

        private void ProcessConnectionWithThrottling(object socketObj)
        {
            TcpClient socket = (TcpClient)socketObj;
            HttpProcessor processor = null;

            try
            {
                connectionSemaphore?.Wait();

                processor = new HttpProcessor(socket, this);
                processor.Process();
            }
            finally
            {
                try
                {
                    processor?.Dispose();
                    connectionSemaphore?.Release();
                }
                catch { }
            }
        }

        /// <summary>
        /// Abstract method to handle GET request
        /// </summary>
        /// <param name="processor"></param>
        public abstract void HandleGETRequest(HttpProcessor processor);

        /// <summary>
        /// Abstract method to handle POST request
        /// </summary>
        /// <param name="processor"></param>
        /// <param name="inputData"></param>
        public abstract void HandlePOSTRequest(HttpProcessor processor, StreamReader inputData);
    }
}