/***********************************************************
 * This file is a part of TinyOPDS server project
 * 
 * Copyright (c) 2013 SeNSSoFT
 * All rights reserved.
 *
 * This code is licensed under the Microsoft Public License, 
 * see http://tinyopds.codeplex.com/license for the details.
 *
 * This module contains simple HTTP processor implementation
 * and abstract class for HTTP server
 * 
 * TODO: add HTTP authentification
 * 
 ************************************************************/

using System;
using System.Collections;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace TinyOPDS.Server
{
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

        // Maximum post size, 1 Mb
        private const int MAX_POST_SIZE = 1024 * 1024;

        // Output buffer size, 64 Kb max
        private const int OUTPUT_BUFFER_SIZE = 1024 * 32;

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
            int next_char;
            string data = string.Empty;
            while (true) 
            {
                next_char = inputStream.ReadByte();
                if (next_char == '\n') { break; }
                if (next_char == '\r') { continue; }
                if (next_char == -1) { Thread.Sleep(1); continue; };
                data += Convert.ToChar(next_char);
            }            
            return data;
        }

        public void Process(object param) 
        {                        
            // We can't use a StreamReader for input, because it buffers up extra data on us inside it's
            // "processed" view of the world, and we want the data raw after the headers
            _inputStream = new BufferedStream(Socket.GetStream());

            // We probably shouldn't be using a StreamWriter for all output from handlers either
            OutputStream = new StreamWriter(new BufferedStream(Socket.GetStream(), OUTPUT_BUFFER_SIZE));
            OutputStream.AutoFlush = true;

            try 
            {
                ParseRequest();
                ReadHeaders();
                if (HttpMethod.Equals("GET")) 
                {
                    HandleGETRequest();
                } 
                else if (HttpMethod.Equals("POST")) 
                {
                    HandlePOSTRequest();
                }
            } 
            catch (Exception e) 
            {
                Log.WriteLine(".Process(object param) exception: {0}", e.Message);
                WriteFailure();
            }

            try
            {
                if (OutputStream.BaseStream.CanWrite)
                {
                    try
                    {
                        OutputStream.Flush();
                    }
                    catch (Exception e)
                    {
                        Log.WriteLine(".Process(object param): outputStream.Flush() exception: {0}", e.Message);
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

        public void ParseRequest() 
        {
            String request = StreamReadLine(_inputStream);
            string[] tokens = request.Split(' ');
            if (tokens.Length != 3) 
            {
                throw new Exception("ParseRequest(): invalid HTTP request line");
            }
            HttpMethod = tokens[0].ToUpper();
            HttpUrl = tokens[1];
            HttpProtocolVersion = tokens[2];
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
                        Log.WriteLine("starting Read, to_read={0}", to_read);
                        int numread = this._inputStream.Read(buf, 0, Math.Min(BUF_SIZE, to_read));
                        Log.WriteLine("Read finished, bytes read={0}", numread);
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
    }

    /// <summary>
    /// Simple HTTP server
    /// </summary>
    public abstract class HttpServer
    {
        protected int _port;
        protected int _timeout;
        TcpListener _listener;
        internal bool _isActive = false;
       
        public HttpServer(int Port, int Timeout = 5000) 
        {
            _port = Port;
            _timeout = Timeout;
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
        }

        /// <summary>
        /// Server listener
        /// </summary>
        public void Listen() 
        {
            HttpProcessor processor = null;
            try
            {
                _isActive = true;
                _listener = new TcpListener(IPAddress.Any, _port);
                _listener.Start();
                while (_isActive)
                {
                    if (_listener.Pending())
                    {
                        TcpClient socket = _listener.AcceptTcpClient();
                        socket.SendTimeout = socket.ReceiveTimeout = _timeout;
                        socket.SendBufferSize = 1024 * 1024;
                        processor = new HttpProcessor(socket, this);
                        ThreadPool.QueueUserWorkItem(new WaitCallback(processor.Process));
                    }
                    else Thread.Sleep(10);
                }
            }
            catch (Exception e)
            {
                Log.WriteLine(LogLevel.Error, ".Listen() exception: {0}", e.Message);
                _isActive = false;
            }
            finally
            {
                if (processor != null) processor.Dispose();
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
