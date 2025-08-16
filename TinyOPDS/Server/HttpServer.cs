/***********************************************************
 * This file is a part of TinyOPDS server project
 *
 * Enhanced HTTP server implementation with improved
 * stability, thread safety, and resource management
 *
 ************************************************************/

using System;
using System.Linq;
using System.Collections;
using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using System.ComponentModel;
using System.Text;

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
    /// Enhanced HTTP processor with improved stability and thread safety
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

        // Thread-safe collections replacing static ones
        public static BindingList<Credential> Credentials = new BindingList<Credential>();
        public static ConcurrentDictionary<string, bool> AuthorizedClients = new ConcurrentDictionary<string, bool>();
        public static ConcurrentDictionary<string, int> BannedClients = new ConcurrentDictionary<string, int>();

        // Enhanced constants
        private const int MAX_POST_SIZE = 1024 * 1024; // 1 MB
        private const int OUTPUT_BUFFER_SIZE = 64 * 1024; // 64 KB
        private const int INPUT_BUFFER_SIZE = 32 * 1024; // 32 KB
        private const int READ_TIMEOUT_MS = 30000; // 30 seconds
        private const int MAX_HEADER_SIZE = 8192; // 8 KB max for headers
        private const int MAX_REQUEST_LINE_LENGTH = 2048; // 2 KB max for request line

        private volatile bool _disposed = false;
        private readonly object _disposeLock = new object();
        private CancellationTokenSource _cancellationTokenSource;

        public HttpProcessor(TcpClient socket, HttpServer server)
        {
            this.Socket = socket ?? throw new ArgumentNullException(nameof(socket));
            this.Server = server ?? throw new ArgumentNullException(nameof(server));
            this._cancellationTokenSource = new CancellationTokenSource();

            // Configure socket for better performance
            if (socket.Connected)
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
                        _cancellationTokenSource?.Cancel();

                        if (OutputStream != null)
                        {
                            try
                            {
                                // Check if the underlying stream is still writable before flushing
                                if (OutputStream.BaseStream != null && OutputStream.BaseStream.CanWrite)
                                {
                                    OutputStream.Flush();
                                }
                            }
                            catch (ObjectDisposedException)
                            {
                                // Stream already disposed, ignore
                            }
                            catch (InvalidOperationException)
                            {
                                // Stream in invalid state, ignore
                            }
                            catch (Exception ex)
                            {
                                Log.WriteLine(LogLevel.Warning, "Error flushing OutputStream: {0}", ex.Message);
                            }

                            try
                            {
                                OutputStream.Close();
                            }
                            catch (ObjectDisposedException)
                            {
                                // Already disposed, ignore
                            }
                            catch (Exception ex)
                            {
                                Log.WriteLine(LogLevel.Warning, "Error closing OutputStream: {0}", ex.Message);
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

                        if (_inputStream != null)
                        {
                            try { _inputStream.Close(); } catch { }
                            _inputStream.Dispose();
                            _inputStream = null;
                        }

                        if (Socket != null)
                        {
                            try
                            {
                                if (Socket.Connected)
                                    Socket.GetStream()?.Close();
                                Socket.Close();
                            }
                            catch { }
                            Socket = null;
                        }

                        _cancellationTokenSource?.Dispose();
                    }
                    catch (Exception ex)
                    {
                        Log.WriteLine(LogLevel.Error, "Error disposing HttpProcessor: {0}", ex.Message);
                    }
                }
                _disposed = true;
            }
        }

        /// <summary>
        /// Safely ensure OutputStream is created even for early-error paths.
        /// </summary>
        private bool EnsureOutputStream()
        {
            if (OutputStream != null) return true;
            if (Socket == null || _disposed) return false;

            try
            {
                var netStream = Socket.GetStream();
                if (netStream == null || !netStream.CanWrite) return false;

                OutputStream = new StreamWriter(new BufferedStream(netStream, OUTPUT_BUFFER_SIZE), Encoding.UTF8)
                {
                    AutoFlush = false
                };
                return true;
            }
            catch (Exception e)
            {
                Log.WriteLine(LogLevel.Error, "EnsureOutputStream() failed: {0}", e.Message);
                return false;
            }
        }

        /// <summary>
        /// Enhanced stream reading with timeout and proper error handling
        /// </summary>
        private string StreamReadLine(Stream inputStream, CancellationToken cancellationToken)
        {
            if (inputStream == null || !inputStream.CanRead || _disposed)
                return null;

            var buffer = new StringBuilder(256);
            int totalBytes = 0;

            try
            {
                while (!cancellationToken.IsCancellationRequested && totalBytes < MAX_REQUEST_LINE_LENGTH)
                {
                    int nextChar = inputStream.ReadByte();

                    if (nextChar == -1) // End of stream
                        break;

                    totalBytes++;

                    if (nextChar == '\n')
                        break;

                    if (nextChar == '\r')
                        continue;

                    buffer.Append((char)nextChar);
                }

                return totalBytes > 0 ? buffer.ToString() : null;
            }
            catch (Exception ex)
            {
                Log.WriteLine(LogLevel.Error, "StreamReadLine error: {0}", ex.Message);
                return null;
            }
        }

        public void Process(object param)
        {
            if (_disposed) return;

            try
            {
                // Set timeout for the entire operation
                _cancellationTokenSource.CancelAfter(TimeSpan.FromSeconds(60));

                // Enhanced input stream with proper buffering
                _inputStream = new BufferedStream(Socket.GetStream(), INPUT_BUFFER_SIZE);

                if (ParseRequest())
                {
                    if (EnsureOutputStream())
                    {
                        ProcessRequestSafely();
                    }
                    else
                    {
                        Log.WriteLine(LogLevel.Error, "Failed to create output stream");
                    }
                }
                else
                {
                    // Malformed start-line
                    if (EnsureOutputStream())
                    {
                        WriteBadRequest();
                    }
                }
            }
            catch (ObjectDisposedException)
            {
                // Normal disposal, ignore
            }
            catch (OperationCanceledException)
            {
                Log.WriteLine(LogLevel.Warning, "Request processing cancelled due to timeout");
            }
            catch (Exception e)
            {
                Log.WriteLine(LogLevel.Error, "Process() exception: {0}", e.Message);
                try
                {
                    if (EnsureOutputStream())
                    {
                        WriteFailure();
                    }
                }
                catch
                {
                    Log.WriteLine(LogLevel.Error, "Failed to write error response");
                }
            }
            finally
            {
                // Ensure proper cleanup
                CleanupResources();
            }
        }

        private void CleanupResources()
        {
            try
            {
                if (OutputStream != null)
                {
                    try
                    {
                        // Check if the underlying stream is still writable before flushing
                        if (OutputStream.BaseStream != null && OutputStream.BaseStream.CanWrite)
                        {
                            OutputStream.Flush();
                        }
                    }
                    catch (ObjectDisposedException)
                    {
                        // Stream already disposed, ignore
                    }
                    catch (InvalidOperationException)
                    {
                        // Stream in invalid state, ignore  
                    }
                    catch (Exception ex)
                    {
                        Log.WriteLine(LogLevel.Error, "Error flushing output during cleanup: {0}", ex.Message);
                    }

                    try
                    {
                        OutputStream.Close();
                    }
                    catch (ObjectDisposedException)
                    {
                        // Already disposed, ignore
                    }
                    catch (Exception ex)
                    {
                        Log.WriteLine(LogLevel.Error, "Error closing output during cleanup: {0}", ex.Message);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.WriteLine(LogLevel.Error, "Error during resource cleanup: {0}", ex.Message);
            }

            Dispose();
        }

        private void ProcessRequestSafely()
        {
            try
            {
                ReadHeaders();

                bool authorized = true;
                bool checkLogin = true;

                // Enhanced client identification
                string clientHash = GenerateClientHash();
                string remoteIP = GetRemoteIPAddress();

                if (TinyOPDS.Properties.Settings.Default.UseHTTPAuth)
                {
                    authorized = ProcessAuthentication(clientHash, remoteIP, out checkLogin);
                }

                if (authorized)
                {
                    HttpServer.ServerStatistics.AddClient(clientHash);
                    HandleAuthorizedRequest();
                }
                else
                {
                    HandleUnauthorizedRequest(remoteIP, checkLogin);
                }
            }
            catch (Exception e)
            {
                Log.WriteLine(LogLevel.Error, "ProcessRequestSafely() exception: {0}", e.Message);
                WriteFailure();
            }
        }

        private string GenerateClientHash()
        {
            var clientInfo = new StringBuilder();

            if (HttpHeaders.ContainsKey("User-Agent"))
                clientInfo.Append(HttpHeaders["User-Agent"].ToString());

            string remoteIP = GetRemoteIPAddress();
            clientInfo.Append(remoteIP);

            return Utils.CreateGuid(Utils.IsoOidNamespace, clientInfo.ToString()).ToString();
        }

        private string GetRemoteIPAddress()
        {
            try
            {
                if (Socket?.Client?.RemoteEndPoint is IPEndPoint remoteEndPoint)
                {
                    return remoteEndPoint.Address.ToString();
                }
            }
            catch (Exception ex)
            {
                Log.WriteLine(LogLevel.Warning, "Failed to get remote IP: {0}", ex.Message);
            }
            return "unknown";
        }

        private bool ProcessAuthentication(string clientHash, string remoteIP, out bool checkLogin)
        {
            checkLogin = true;

            // Check if IP is banned
            if (TinyOPDS.Properties.Settings.Default.BanClients)
            {
                if (BannedClients.TryGetValue(remoteIP, out int attempts) &&
                    attempts >= TinyOPDS.Properties.Settings.Default.WrongAttemptsCount)
                {
                    checkLogin = false;
                    return false;
                }
            }

            // Check authorized clients cache
            if (TinyOPDS.Properties.Settings.Default.RememberClients)
            {
                if (AuthorizedClients.ContainsKey(clientHash))
                {
                    return true;
                }
            }

            // Check HTTP Basic Auth
            return ProcessBasicAuth(clientHash, remoteIP);
        }

        private bool ProcessBasicAuth(string clientHash, string remoteIP)
        {
            if (!HttpHeaders.ContainsKey("Authorization"))
                return false;

            string authHeader = HttpHeaders["Authorization"]?.ToString();
            if (string.IsNullOrEmpty(authHeader))
                return false;

            if (!authHeader.StartsWith("Basic ", StringComparison.OrdinalIgnoreCase))
                return false;

            try
            {
                string[] credentials = authHeader.Substring(6).DecodeFromBase64().Split(':');

                if (credentials.Length != 2)
                    return false;

                string user = credentials[0];
                string password = credentials[1];

                // Thread-safe credential checking
                lock (Credentials)
                {
                    foreach (Credential cred in Credentials)
                    {
                        if (cred.User.Equals(user, StringComparison.Ordinal))
                        {
                            bool authorized = cred.Password.Equals(password, StringComparison.Ordinal);
                            if (authorized)
                            {
                                AuthorizedClients.TryAdd(clientHash, true);
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
            catch (Exception e)
            {
                Log.WriteLine(LogLevel.Authentication, "Authentication exception: IP: {0}, {1}", remoteIP, e.Message);
            }

            return false;
        }

        private void HandleAuthorizedRequest()
        {
            if (HttpMethod.Equals("GET", StringComparison.OrdinalIgnoreCase))
            {
                HttpServer.ServerStatistics.GetRequests++;
                HandleGETRequest();
            }
            else if (HttpMethod.Equals("POST", StringComparison.OrdinalIgnoreCase))
            {
                HttpServer.ServerStatistics.PostRequests++;
                HandlePOSTRequest();
            }
            else
            {
                WriteMethodNotAllowed();
            }
        }

        private void HandleUnauthorizedRequest(string remoteIP, bool checkLogin)
        {
            if (TinyOPDS.Properties.Settings.Default.BanClients)
            {
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

        public bool ParseRequest()
        {
            try
            {
                string request = StreamReadLine(_inputStream, _cancellationTokenSource.Token);

                if (string.IsNullOrWhiteSpace(request))
                    return false;

                string[] tokens = request.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

                if (tokens.Length != 3)
                    return false;

                HttpMethod = tokens[0].ToUpperInvariant();
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
                while ((line = StreamReadLine(_inputStream, _cancellationTokenSource.Token)) != null)
                {
                    totalHeaderSize += line.Length + 2; // +2 for CRLF
                    headerCount++;

                    if (totalHeaderSize > MAX_HEADER_SIZE)
                    {
                        throw new InvalidOperationException("Headers too large");
                    }

                    if (headerCount > MAX_HEADERS)
                    {
                        throw new InvalidOperationException("Too many headers");
                    }

                    if (string.IsNullOrEmpty(line))
                        return; // End of headers

                    int separator = line.IndexOf(':');
                    if (separator == -1 || separator == 0)
                    {
                        Log.WriteLine(LogLevel.Warning, "Invalid HTTP header line: {0}", line);
                        continue; // Skip invalid headers instead of throwing
                    }

                    string name = line.Substring(0, separator).Trim();
                    string value = line.Length > separator + 1 ? line.Substring(separator + 1).Trim() : string.Empty;

                    if (!string.IsNullOrEmpty(name) && name.Length <= 256) // Reasonable header name limit
                    {
                        HttpHeaders[name] = value;
                    }
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

        private const int BUF_SIZE = 8192; // Increased buffer size

        public void HandlePOSTRequest()
        {
            MemoryStream memStream = null;

            try
            {
                int contentLength = 0;

                if (HttpHeaders.ContainsKey("Content-Length"))
                {
                    if (!int.TryParse(HttpHeaders["Content-Length"]?.ToString(), out contentLength) || contentLength < 0)
                    {
                        throw new InvalidOperationException("Invalid Content-Length header");
                    }

                    if (contentLength > MAX_POST_SIZE)
                    {
                        throw new InvalidOperationException($"POST Content-Length({contentLength}) too big for this server (max: {MAX_POST_SIZE})");
                    }
                }

                memStream = new MemoryStream();

                if (contentLength > 0)
                {
                    byte[] buffer = new byte[Math.Min(BUF_SIZE, contentLength)];
                    int totalRead = 0;

                    while (totalRead < contentLength && !_cancellationTokenSource.Token.IsCancellationRequested)
                    {
                        int toRead = Math.Min(buffer.Length, contentLength - totalRead);
                        int bytesRead = _inputStream.Read(buffer, 0, toRead);

                        if (bytesRead == 0)
                        {
                            if (totalRead < contentLength)
                                throw new InvalidOperationException("Client disconnected during POST");
                            break;
                        }

                        memStream.Write(buffer, 0, bytesRead);
                        totalRead += bytesRead;
                    }

                    memStream.Seek(0, SeekOrigin.Begin);
                }

                using (StreamReader reader = new StreamReader(memStream, Encoding.UTF8, false, BUF_SIZE, leaveOpen: false))
                {
                    memStream = null; // Prevent double disposal
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

        /// <summary>
        /// Safely write to OutputStream with proper error handling
        /// </summary>
        private bool SafeWriteToOutputStream(Action writeAction)
        {
            if (_disposed || OutputStream == null) return false;

            try
            {
                // Check if the underlying stream is still writable
                if (OutputStream.BaseStream != null && OutputStream.BaseStream.CanWrite)
                {
                    writeAction();
                    return true;
                }
            }
            catch (ObjectDisposedException)
            {
                // Stream disposed, ignore
            }
            catch (InvalidOperationException)
            {
                // Stream in invalid state, ignore
            }
            catch (IOException ex)
            {
                Log.WriteLine(LogLevel.Warning, "IO error writing to OutputStream: {0}", ex.Message);
            }
            catch (Exception ex)
            {
                Log.WriteLine(LogLevel.Error, "Unexpected error writing to OutputStream: {0}", ex.Message);
            }

            return false;
        }

        // Enhanced HTTP response methods with better error handling
        public void WriteSuccess(string contentType = "text/xml", bool isGZip = false)
        {
            if (!EnsureOutputStream()) return;

            SafeWriteToOutputStream(() =>
            {
                OutputStream.WriteLine("HTTP/1.1 200 OK");
                OutputStream.WriteLine($"Content-Type: {contentType}");
                OutputStream.WriteLine($"Date: {DateTime.UtcNow:R}");
                OutputStream.WriteLine("Server: TinyOPDS/2.0");
                if (isGZip) OutputStream.WriteLine("Content-Encoding: gzip");
                OutputStream.WriteLine("Connection: close");
                OutputStream.WriteLine("Cache-Control: no-cache");
                OutputStream.WriteLine();
                OutputStream.Flush();
            });
        }

        public void WriteFailure()
        {
            if (!EnsureOutputStream()) return;

            SafeWriteToOutputStream(() =>
            {
                OutputStream.WriteLine("HTTP/1.1 500 Internal Server Error");
                OutputStream.WriteLine($"Date: {DateTime.UtcNow:R}");
                OutputStream.WriteLine("Server: TinyOPDS/2.0");
                OutputStream.WriteLine("Connection: close");
                OutputStream.WriteLine("Content-Length: 0");
                OutputStream.WriteLine();
                OutputStream.Flush();
            });
        }

        public void WriteBadRequest()
        {
            if (!EnsureOutputStream()) return;

            SafeWriteToOutputStream(() =>
            {
                OutputStream.WriteLine("HTTP/1.1 400 Bad Request");
                OutputStream.WriteLine($"Date: {DateTime.UtcNow:R}");
                OutputStream.WriteLine("Server: TinyOPDS/2.0");
                OutputStream.WriteLine("Connection: close");
                OutputStream.WriteLine("Content-Length: 0");
                OutputStream.WriteLine();
                OutputStream.Flush();
            });
        }

        public void WriteNotAuthorized()
        {
            if (!EnsureOutputStream()) return;

            SafeWriteToOutputStream(() =>
            {
                OutputStream.WriteLine("HTTP/1.1 401 Unauthorized");
                OutputStream.WriteLine("WWW-Authenticate: Basic realm=\"TinyOPDS\"");
                OutputStream.WriteLine($"Date: {DateTime.UtcNow:R}");
                OutputStream.WriteLine("Server: TinyOPDS/2.0");
                OutputStream.WriteLine("Connection: close");
                OutputStream.WriteLine("Content-Length: 0");
                OutputStream.WriteLine();
                OutputStream.Flush();
            });
        }

        public void WriteForbidden()
        {
            if (!EnsureOutputStream()) return;

            SafeWriteToOutputStream(() =>
            {
                OutputStream.WriteLine("HTTP/1.1 403 Forbidden");
                OutputStream.WriteLine($"Date: {DateTime.UtcNow:R}");
                OutputStream.WriteLine("Server: TinyOPDS/2.0");
                OutputStream.WriteLine("Connection: close");
                OutputStream.WriteLine("Content-Length: 0");
                OutputStream.WriteLine();
                OutputStream.Flush();
            });
        }

        public void WriteMethodNotAllowed()
        {
            if (!EnsureOutputStream()) return;

            SafeWriteToOutputStream(() =>
            {
                OutputStream.WriteLine("HTTP/1.1 405 Method Not Allowed");
                OutputStream.WriteLine("Allow: GET, POST");
                OutputStream.WriteLine($"Date: {DateTime.UtcNow:R}");
                OutputStream.WriteLine("Server: TinyOPDS/2.0");
                OutputStream.WriteLine("Connection: close");
                OutputStream.WriteLine("Content-Length: 0");
                OutputStream.WriteLine();
                OutputStream.Flush();
            });
        }
    }

    /// <summary>
    /// Enhanced server statistics class with thread safety
    /// </summary>
    public class Statistics
    {
        public event EventHandler StatisticsUpdated;

        private long _booksSent = 0;
        private long _imagesSent = 0;
        private long _getRequests = 0;
        private long _postRequests = 0;
        private long _successfulLoginAttempts = 0;
        private long _wrongLoginAttempts = 0;
        private readonly ConcurrentDictionary<string, bool> _uniqueClients = new ConcurrentDictionary<string, bool>();

        public long BooksSent
        {
            get { return Interlocked.Read(ref _booksSent); }
            set { Interlocked.Exchange(ref _booksSent, value); OnStatisticsUpdated(); }
        }

        public long ImagesSent
        {
            get { return Interlocked.Read(ref _imagesSent); }
            set { Interlocked.Exchange(ref _imagesSent, value); OnStatisticsUpdated(); }
        }

        public long GetRequests
        {
            get { return Interlocked.Read(ref _getRequests); }
            set { Interlocked.Exchange(ref _getRequests, value); OnStatisticsUpdated(); }
        }

        public long PostRequests
        {
            get { return Interlocked.Read(ref _postRequests); }
            set { Interlocked.Exchange(ref _postRequests, value); OnStatisticsUpdated(); }
        }

        public long SuccessfulLoginAttempts
        {
            get { return Interlocked.Read(ref _successfulLoginAttempts); }
            set { Interlocked.Exchange(ref _successfulLoginAttempts, value); OnStatisticsUpdated(); }
        }

        public long WrongLoginAttempts
        {
            get { return Interlocked.Read(ref _wrongLoginAttempts); }
            set { Interlocked.Exchange(ref _wrongLoginAttempts, value); OnStatisticsUpdated(); }
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

        public void IncrementGetRequests()
        {
            Interlocked.Increment(ref _getRequests);
            OnStatisticsUpdated();
        }

        public void IncrementPostRequests()
        {
            Interlocked.Increment(ref _postRequests);
            OnStatisticsUpdated();
        }

        public void IncrementSuccessfulLoginAttempts()
        {
            Interlocked.Increment(ref _successfulLoginAttempts);
            OnStatisticsUpdated();
        }

        public void IncrementWrongLoginAttempts()
        {
            Interlocked.Increment(ref _wrongLoginAttempts);
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
            Interlocked.Exchange(ref _booksSent, 0);
            Interlocked.Exchange(ref _imagesSent, 0);
            Interlocked.Exchange(ref _getRequests, 0);
            Interlocked.Exchange(ref _postRequests, 0);
            Interlocked.Exchange(ref _successfulLoginAttempts, 0);
            Interlocked.Exchange(ref _wrongLoginAttempts, 0);

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
    /// Enhanced HTTP server with improved connection management and stability
    /// </summary>
    public abstract class HttpServer
    {
        protected int _port;
        protected int _timeout;
        protected IPAddress _interfaceIP = IPAddress.Any;
        private TcpListener _listener;
        internal volatile bool _isActive = false;

        public bool IsActive { get { return _isActive; } }
        public Exception ServerException { get; private set; }
        public AutoResetEvent ServerReady { get; private set; }
        public static Statistics ServerStatistics { get; private set; }

        private volatile bool _isIdle = true;
        private readonly TimeSpan _idleTimeout = TimeSpan.FromMinutes(10);
        private readonly object _serverLock = new object();
        private volatile bool _stopRequested = false;

        public bool IsIdle { get { return _isIdle; } }

        public HttpServer(int port, int timeout = 10000)
        {
            _port = port;
            _timeout = timeout;
            ServerReady = new AutoResetEvent(false);
            if (ServerStatistics == null)
                ServerStatistics = new Statistics();
        }

        public HttpServer(IPAddress interfaceIP, int port, int timeout = 10000)
        {
            _interfaceIP = interfaceIP ?? IPAddress.Any;
            _port = port;
            _timeout = timeout;
            ServerReady = new AutoResetEvent(false);
            if (ServerStatistics == null)
                ServerStatistics = new Statistics();
        }

        ~HttpServer()
        {
            StopServer();
        }

        public virtual void StopServer()
        {
            lock (_serverLock)
            {
                _stopRequested = true;
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
                        ServerReady.Set();
                        ServerReady.Dispose();
                    }
                    catch { }
                    ServerReady = null;
                }
            }
        }

        /// <summary>
        /// Enhanced server listener with better error handling and resource management
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
                _stopRequested = false;
                ServerReady.Set();

                Log.WriteLine(LogLevel.Info, "HTTP server started on {0}:{1}", _interfaceIP, _port);

                while (_isActive && !_stopRequested)
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
                        if (_isActive && !_stopRequested)
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
                ServerReady?.Set();
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
                // Configure socket timeouts and buffer sizes
                socket.SendTimeout = socket.ReceiveTimeout = _timeout;
                socket.SendBufferSize = 64 * 1024;
                socket.ReceiveBufferSize = 32 * 1024;
                socket.NoDelay = true;

                // Create processor and queue for processing on ThreadPool
                var processor = new HttpProcessor(socket, this);

                // Use Task.Run for better async handling
                Task.Run(() => processor.Process(null));
            }
            catch (Exception ex)
            {
                Log.WriteLine(LogLevel.Error, "Error processing connection: {0}", ex.Message);
                try
                {
                    socket?.Close();
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