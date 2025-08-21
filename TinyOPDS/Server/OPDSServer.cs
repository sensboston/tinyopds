/***********************************************************
 * This file is a part of TinyOPDS server project
 * 
 * Copyright (c) 2013 SeNSSoFT
 *
 * This code is licensed under the Microsoft Public License, 
 * see http://tinyopds.codeplex.com/license for the details.
 *
 * Enhanced OPDS HTTP server with improved stability and error handling
 * 
 ************************************************************/

using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Xml;
using System.Xml.Xsl;
using System.Xml.XPath;
using System.Reflection;
using System.Collections.Generic;

using Ionic.Zip;
using TinyOPDS.OPDS;
using TinyOPDS.Data;
using System.Xml.Linq;

namespace TinyOPDS.Server
{
    public class OPDSServer : HttpServer
    {
        private readonly string[] _extensions = { ".zip", ".epub", ".jpeg", ".ico", ".xml" };
        private XslCompiledTransform _xslTransform = new XslCompiledTransform();
        private readonly object _xslLock = new object();
        private Dictionary<string, bool> _opdsStructure;

        public OPDSServer(IPAddress interfaceIP, int port, int timeout = 5000) : base(interfaceIP, port, timeout)
        {
            InitializeXslTransform();
            LoadOPDSStructure();
        }

        private void LoadOPDSStructure()
        {
            try
            {
                string structureString = Properties.Settings.Default.OPDSStructure;
                _opdsStructure = new Dictionary<string, bool>();

                if (string.IsNullOrEmpty(structureString))
                {
                    InitializeDefaultOPDSStructure();
                }
                else
                {
                    ParseOPDSStructure(structureString);
                }
            }
            catch (Exception ex)
            {
                Log.WriteLine(LogLevel.Warning, "Error loading OPDS structure: {0}", ex.Message);
                InitializeDefaultOPDSStructure();
            }
        }

        private void InitializeDefaultOPDSStructure()
        {
            _opdsStructure = new Dictionary<string, bool>
            {
                {"newdate", true},
                {"newtitle", true},
                {"authorsindex", true},
                {"author-details", true},
                {"author-series", true},
                {"author-no-series", true},
                {"author-alphabetic", true},
                {"author-by-date", true},
                {"sequencesindex", true},
                {"genres", true}
            };
        }

        private void ParseOPDSStructure(string structure)
        {
            InitializeDefaultOPDSStructure();

            string[] parts = structure.Split(';');
            foreach (string part in parts)
            {
                string[] keyValue = part.Split(':');
                if (keyValue.Length == 2 && _opdsStructure.ContainsKey(keyValue[0]))
                {
                    _opdsStructure[keyValue[0]] = keyValue[1] == "1";
                }
            }
        }

        private bool IsRouteEnabled(string route)
        {
            return _opdsStructure.ContainsKey(route) && _opdsStructure[route];
        }

        private void InitializeXslTransform()
        {
            try
            {
                string xslFileName = Path.Combine(Utils.ServiceFilesLocation, "xml2html.xsl");

                if (File.Exists(xslFileName))
                {
                    _xslTransform.Load(xslFileName);
                    Log.WriteLine(LogLevel.Info, "Loaded external XSL template: {0}", xslFileName);
                }
                else
                {
                    using (Stream resStream = Assembly.GetExecutingAssembly().GetManifestResourceStream(
                        Assembly.GetExecutingAssembly().GetName().Name + ".xml2html.xsl"))
                    {
                        if (resStream != null)
                        {
                            using (XmlReader reader = XmlReader.Create(resStream))
                                _xslTransform.Load(reader);
                            Log.WriteLine(LogLevel.Info, "Loaded embedded XSL template");
                        }
                        else
                        {
                            Log.WriteLine(LogLevel.Warning, "XSL template not found");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.WriteLine(LogLevel.Error, "Error loading XSL template: {0}", ex.Message);
            }
        }

        public override void HandlePOSTRequest(HttpProcessor processor, StreamReader inputData)
        {
            Log.WriteLine(LogLevel.Warning, "HTTP POST request from {0}: {1} : NOT IMPLEMENTED",
                GetClientIP(processor), processor.HttpUrl);
            processor.WriteMethodNotAllowed();
        }

        public override void HandleGETRequest(HttpProcessor processor)
        {
            string clientIP = GetClientIP(processor);
            Log.WriteLine("HTTP GET request from {0}: {1}", clientIP, processor.HttpUrl);

            try
            {
                string request = NormalizeRequest(processor.HttpUrl);
                string ext = GetFileExtension(request);

                if (!IsValidRequest(request, ext))
                {
                    processor.WriteBadRequest();
                    return;
                }

                bool isWWWRequest = IsWebRequest(processor.HttpUrl);
                bool acceptFB2 = DetectFB2Support(processor.HttpHeaders["User-Agent"] as string) || isWWWRequest;
                int threshold = (int)(isWWWRequest ?
                    Properties.Settings.Default.ItemsPerWebPage :
                    Properties.Settings.Default.ItemsPerOPDSPage);

                if (string.IsNullOrEmpty(ext))
                {
                    HandleOPDSRequest(processor, request, isWWWRequest, acceptFB2, threshold);
                }
                else if (request.Contains("opds-opensearch.xml"))
                {
                    HandleOpenSearchRequest(processor);
                }
                else if ((request.Contains(".fb2.zip") && ext.Equals(".zip")) || ext.Equals(".epub"))
                {
                    HandleBookDownloadRequest(processor, request, ext, acceptFB2);
                }
                else if (ext.Equals(".jpeg"))
                {
                    HandleImageRequest(processor, request);
                }
                else if (ext.Equals(".ico"))
                {
                    HandleIconRequest(processor, request);
                }
                else
                {
                    processor.WriteFailure();
                }
            }
            catch (Exception e)
            {
                Log.WriteLine(LogLevel.Error, "HandleGETRequest() exception: {0}", e.Message);
                processor.WriteFailure();
            }
        }

        #region Request Processing Helpers

        private string GetClientIP(HttpProcessor processor)
        {
            try
            {
                return ((IPEndPoint)processor.Socket.Client.RemoteEndPoint).Address.ToString();
            }
            catch
            {
                return "unknown";
            }
        }

        private string NormalizeRequest(string httpUrl)
        {
            string request = httpUrl;

            if (!request.Contains("opds-opensearch.xml") && !string.IsNullOrEmpty(Properties.Settings.Default.RootPrefix))
            {
                request = request.Replace("/" + Properties.Settings.Default.RootPrefix, "");
            }
            if (!string.IsNullOrEmpty(Properties.Settings.Default.HttpPrefix))
            {
                request = request.Replace("/" + Properties.Settings.Default.HttpPrefix, "");
            }

            while (request.Contains("//"))
                request = request.Replace("//", "/");

            if (!request.StartsWith("/"))
                request = "/" + request;

            int paramPos = request.IndexOf('?');
            if (paramPos >= 0)
            {
                if (request.Contains("pageNumber") || request.Contains("searchTerm"))
                {
                    int endParam = request.IndexOf('&', paramPos + 1);
                    if (endParam > 0 && !request.Substring(endParam).Contains("pageNumber") && !request.Substring(endParam).Contains("searchTerm"))
                    {
                        request = request.Substring(0, endParam);
                    }
                }
                else
                {
                    request = request.Substring(0, paramPos);
                }
            }

            return request;
        }

        private string GetFileExtension(string request)
        {
            string ext = Path.GetExtension(request).ToLower();
            return _extensions.Contains(ext) ? ext : string.Empty;
        }

        private bool IsValidRequest(string request, string ext)
        {
            return !string.IsNullOrEmpty(request) && request.Length <= 2048;
        }

        private bool IsWebRequest(string httpUrl)
        {
            return httpUrl.StartsWith("/" + Properties.Settings.Default.HttpPrefix) &&
                   !httpUrl.StartsWith("/" + Properties.Settings.Default.RootPrefix);
        }

        private bool DetectFB2Support(string userAgent)
        {
            return Utils.DetectFB2Reader(userAgent);
        }

        #endregion

        #region OPDS Request Handlers

        private void HandleOPDSRequest(HttpProcessor processor, string request, bool isWWWRequest, bool acceptFB2, int threshold)
        {
            try
            {
                LoadOPDSStructure();

                string xml = GenerateOPDSResponse(request, acceptFB2, threshold);

                if (string.IsNullOrEmpty(xml))
                {
                    processor.WriteFailure();
                    return;
                }

                xml = FixNamespace(xml);
                xml = ApplyUriPrefixes(xml, processor, isWWWRequest);

                if (isWWWRequest)
                {
                    string html = TransformToHtml(xml);
                    if (!string.IsNullOrEmpty(html))
                    {
                        processor.WriteSuccess("text/html");
                        processor.OutputStream.Write(html);
                    }
                    else
                    {
                        processor.WriteFailure();
                    }
                }
                else
                {
                    processor.WriteSuccess("application/atom+xml;charset=utf-8");
                    processor.OutputStream.Write(xml);
                }
            }
            catch (Exception e)
            {
                Log.WriteLine(LogLevel.Error, "OPDS catalog exception: {0}", e.Message);
                processor.WriteFailure();
            }
        }

        private string GenerateOPDSResponse(string request, bool acceptFB2, int threshold)
        {
            string[] pathParts = request.Split(new char[] { '?', '=', '&' }, StringSplitOptions.RemoveEmptyEntries);

            if (request.Equals("/"))
            {
                return new RootCatalogWithStructure(_opdsStructure).GetCatalog().ToStringWithDeclaration();
            }
            else if (request.StartsWith("/newdate") && IsRouteEnabled("newdate"))
            {
                return new NewBooksCatalog().GetCatalog(request.Substring(8), true, acceptFB2, threshold).ToStringWithDeclaration();
            }
            else if (request.StartsWith("/newtitle") && IsRouteEnabled("newtitle"))
            {
                return new NewBooksCatalog().GetCatalog(request.Substring(9), false, acceptFB2, threshold).ToStringWithDeclaration();
            }
            else if (request.StartsWith("/authorsindex") && IsRouteEnabled("authorsindex"))
            {
                int numChars = request.StartsWith("/authorsindex/") ? 14 : 13;
                string searchPattern = request.Substring(numChars);
                return new AuthorsCatalogWithStructure(_opdsStructure).GetCatalog(searchPattern, false, threshold).ToStringWithDeclaration();
            }
            else if (request.StartsWith("/author-details/") && IsRouteEnabled("author-details"))
            {
                return new AuthorDetailsCatalogWithStructure(_opdsStructure).GetCatalog(request.Substring(16)).ToStringWithDeclaration();
            }
            else if (request.StartsWith("/author-series/") && IsRouteEnabled("author-series"))
            {
                return new AuthorBooksCatalog().GetSeriesCatalog(request.Substring(15), acceptFB2, threshold).ToStringWithDeclaration();
            }
            else if (request.StartsWith("/author-no-series/") && IsRouteEnabled("author-no-series"))
            {
                return new AuthorBooksCatalog().GetBooksCatalog(request.Substring(18), AuthorBooksCatalog.ViewType.NoSeries, acceptFB2, threshold).ToStringWithDeclaration();
            }
            else if (request.StartsWith("/author-alphabetic/") && IsRouteEnabled("author-alphabetic"))
            {
                return new AuthorBooksCatalog().GetBooksCatalog(request.Substring(19), AuthorBooksCatalog.ViewType.Alphabetic, acceptFB2, threshold).ToStringWithDeclaration();
            }
            else if (request.StartsWith("/author-by-date/") && IsRouteEnabled("author-by-date"))
            {
                return new AuthorBooksCatalog().GetBooksCatalog(request.Substring(16), AuthorBooksCatalog.ViewType.ByDate, acceptFB2, threshold).ToStringWithDeclaration();
            }
            else if (request.StartsWith("/author/"))
            {
                string authorName = request.Substring(8);
                if (IsRouteEnabled("author-details"))
                {
                    return new AuthorDetailsCatalogWithStructure(_opdsStructure).GetCatalog(authorName).ToStringWithDeclaration();
                }
                else
                {
                    return new AuthorBooksCatalog().GetBooksCatalog(authorName, AuthorBooksCatalog.ViewType.Alphabetic, acceptFB2, threshold).ToStringWithDeclaration();
                }
            }
            else if (request.StartsWith("/sequencesindex") && IsRouteEnabled("sequencesindex"))
            {
                int numChars = request.StartsWith("/sequencesindex/") ? 16 : 15;
                return new SequencesCatalog().GetCatalog(request.Substring(numChars), threshold).ToStringWithDeclaration();
            }
            else if (request.Contains("/sequence/"))
            {
                return new BooksCatalog().GetCatalogBySequence(request.Substring(10), acceptFB2, threshold).ToStringWithDeclaration();
            }
            else if (request.StartsWith("/genres") && IsRouteEnabled("genres"))
            {
                int numChars = request.Contains("/genres/") ? 8 : 7;
                return new GenresCatalog().GetCatalog(request.Substring(numChars)).ToStringWithDeclaration();
            }
            else if (request.StartsWith("/genre/"))
            {
                return new BooksCatalog().GetCatalogByGenre(request.Substring(7), acceptFB2, threshold).ToStringWithDeclaration();
            }
            else if (request.StartsWith("/search"))
            {
                return HandleSearchRequest(pathParts, acceptFB2);
            }

            return null;
        }

        private string HandleSearchRequest(string[] pathParts, bool acceptFB2)
        {
            if (pathParts.Length > 1)
            {
                if (pathParts[1].Equals("searchTerm") && pathParts.Length > 2)
                {
                    return new OpenSearch().Search(pathParts[2], "", acceptFB2).ToStringWithDeclaration();
                }
                else if (pathParts[1].Equals("searchType") && pathParts.Length > 4)
                {
                    int pageNumber = 0;
                    if (pathParts.Length > 6 && pathParts[5].Equals("pageNumber"))
                    {
                        int.TryParse(pathParts[6], out pageNumber);
                    }
                    return new OpenSearch().Search(pathParts[4], pathParts[2], acceptFB2, pageNumber).ToStringWithDeclaration();
                }
            }
            return null;
        }

        private string FixNamespace(string xml)
        {
            if (xml.Contains("<feed ") && !xml.Contains("xmlns=\"http://www.w3.org/2005/Atom\""))
            {
                int feedPos = xml.IndexOf("<feed ");
                if (feedPos >= 0)
                {
                    xml = xml.Insert(feedPos + 5, " xmlns=\"http://www.w3.org/2005/Atom\"");
                }
            }
            return xml;
        }

        private string ApplyUriPrefixes(string xml, HttpProcessor processor, bool isWWWRequest)
        {
            try
            {
                if (Properties.Settings.Default.UseAbsoluteUri)
                {
                    string host = processor.HttpHeaders["Host"]?.ToString();
                    if (!string.IsNullOrEmpty(host))
                    {
                        string prefix = isWWWRequest ?
                            Properties.Settings.Default.HttpPrefix :
                            Properties.Settings.Default.RootPrefix;
                        xml = xml.Replace("href=\"", $"href=\"http://{host.UrlCombine(prefix)}");
                    }
                }
                else
                {
                    string prefix = isWWWRequest ?
                        Properties.Settings.Default.HttpPrefix :
                        Properties.Settings.Default.RootPrefix;
                    if (!string.IsNullOrEmpty(prefix))
                    {
                        prefix = "/" + prefix;
                        xml = xml.Replace("href=\"", "href=\"" + prefix);
                        xml = xml.Replace(prefix + "/opds-opensearch.xml", "/opds-opensearch.xml");
                    }
                }
            }
            catch (Exception ex)
            {
                Log.WriteLine(LogLevel.Warning, "Error applying URI prefixes: {0}", ex.Message);
            }
            return xml;
        }

        private string TransformToHtml(string xml)
        {
            try
            {
                lock (_xslLock)
                {
#if DEBUG
                    InitializeXslTransform();
#endif
                    using (var htmlStream = new MemoryStream())
                    using (var stringReader = new StringReader(xml))
                    {
                        var xPathDoc = new XPathDocument(stringReader);
                        var writer = new XmlTextWriter(htmlStream, null);

                        _xslTransform.Transform(xPathDoc, null, writer);
                        htmlStream.Position = 0;

                        using (var streamReader = new StreamReader(htmlStream))
                        {
                            return streamReader.ReadToEnd();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.WriteLine(LogLevel.Error, "XSL transformation error: {0}", ex.Message);
                return null;
            }
        }

        #endregion

        #region Other Request Handlers

        private void HandleOpenSearchRequest(HttpProcessor processor)
        {
            try
            {
                string xml = new OpenSearch().OpenSearchDescription().ToStringWithDeclaration();
                xml = xml.Insert(xml.IndexOf("<OpenSearchDescription") + 22,
                    " xmlns=\"http://a9.com/-/spec/opensearch/1.1/\"");

                xml = ApplyUriPrefixes(xml, processor, false);

                processor.WriteSuccess("application/atom+xml;charset=utf-8");
                processor.OutputStream.Write(xml);
            }
            catch (Exception ex)
            {
                Log.WriteLine(LogLevel.Error, "OpenSearch request error: {0}", ex.Message);
                processor.WriteFailure();
            }
        }

        private void HandleBookDownloadRequest(HttpProcessor processor, string request, string ext, bool acceptFB2)
        {
            try
            {
                string bookID = ExtractBookIdFromRequest(request);
                if (string.IsNullOrEmpty(bookID))
                {
                    Log.WriteLine(LogLevel.Warning, "Invalid book ID in download request: {0}", request);
                    processor.WriteBadRequest();
                    return;
                }

                Book book = Library.GetBook(bookID);
                if (book == null)
                {
                    Log.WriteLine(LogLevel.Warning, "Book {0} not found in library", bookID);
                    processor.WriteFailure();
                    return;
                }

                if (request.Contains(".fb2.zip"))
                {
                    HandleFB2Download(processor, book);
                }
                else if (ext.Equals(".epub"))
                {
                    HandleEpubDownload(processor, book, acceptFB2);
                }

                ServerStatistics.IncrementBooksSent();
            }
            catch (Exception e)
            {
                Log.WriteLine(LogLevel.Error, "Book download error: {0}", e.Message);
                processor.WriteFailure();
            }
        }

        private string ExtractBookIdFromRequest(string request)
        {
            try
            {
                int startPos = request.IndexOf('/') + 1;
                int endPos = request.IndexOf('/', startPos);
                if (endPos == -1) endPos = request.Length;

                if (endPos > startPos)
                {
                    string guid = request.Substring(startPos, endPos - startPos);
                    return guid.Replace("%7B", "{").Replace("%7D", "}");
                }
            }
            catch (Exception ex)
            {
                Log.WriteLine(LogLevel.Warning, "Error extracting book ID: {0}", ex.Message);
            }
            return null;
        }
        private void HandleFB2Download(HttpProcessor processor, Book book)
        {
            using (var memStream = new MemoryStream())
            {
                if (!ExtractBookContent(book, memStream))
                {
                    processor.WriteFailure();
                    return;
                }

                using (var zip = new ZipFile())
                {
                    string fileName = Transliteration.Front(
                        $"{book.Authors.FirstOrDefault() ?? "Unknown"}_{book.Title}.fb2");
                    zip.AddEntry(fileName, memStream);

                    using (var outputStream = new MemoryStream())
                    {
                        zip.Save(outputStream);
                        outputStream.Position = 0;

                        processor.WriteSuccess("application/fb2+zip");
                        outputStream.CopyTo(processor.OutputStream.BaseStream);
                        processor.OutputStream.BaseStream.Flush();
                    }
                }
            }
        }

        private void HandleEpubDownload(HttpProcessor processor, Book book, bool acceptFB2)
        {
            using (var memStream = new MemoryStream())
            {
                if (!ExtractBookContent(book, memStream))
                {
                    processor.WriteFailure();
                    return;
                }

                if (book.BookType == BookType.FB2 && !acceptFB2)
                {
                    if (!ConvertFB2ToEpub(book, memStream))
                    {
                        processor.WriteFailure();
                        return;
                    }
                }

                processor.WriteSuccess("application/epub+zip");
                memStream.Position = 0;
                memStream.CopyTo(processor.OutputStream.BaseStream);
                processor.OutputStream.BaseStream.Flush();
            }
        }

        private bool ExtractBookContent(Book book, MemoryStream memStream)
        {
            try
            {
                if (book.FilePath.ToLower().Contains(".zip@"))
                {
                    string[] pathParts = book.FilePath.Split('@');
                    using (var zipFile = new ZipFile(pathParts[0]))
                    {
                        var entry = zipFile.Entries.FirstOrDefault(e => e.FileName.Contains(pathParts[1]));
                        if (entry != null)
                        {
                            entry.Extract(memStream);
                            return true;
                        }
                    }
                }
                else
                {
                    using (var stream = new FileStream(book.FilePath, FileMode.Open, FileAccess.Read, FileShare.Read))
                    {
                        stream.CopyTo(memStream);
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                Log.WriteLine(LogLevel.Error, "Error extracting book content: {0}", ex.Message);
            }
            return false;
        }

        private bool ConvertFB2ToEpub(Book book, MemoryStream memStream)
        {
            try
            {
                if (string.IsNullOrEmpty(Properties.Settings.Default.ConvertorPath))
                {
                    Log.WriteLine(LogLevel.Error, "No FB2 to EPUB converter configured");
                    return false;
                }

                string tempDir = Path.GetTempPath();
                string inFileName = Path.Combine(tempDir, book.ID + ".fb2");
                string outFileName = Path.Combine(tempDir, book.ID + ".epub");

                try
                {
                    using (var fileStream = new FileStream(inFileName, FileMode.Create, FileAccess.Write))
                    {
                        memStream.Position = 0;
                        memStream.CopyTo(fileStream);
                    }

                    string command = Path.Combine(Properties.Settings.Default.ConvertorPath,
                        Utils.IsLinux ? "fb2toepub" : "Fb2ePub.exe");
                    string arguments = Utils.IsLinux ?
                        $"{inFileName} {outFileName}" :
                        $"\"{inFileName}\" \"{outFileName}\"";

                    using (var converter = new ProcessHelper(command, arguments))
                    {
                        converter.Run();

                        if (File.Exists(outFileName))
                        {
                            memStream.SetLength(0);
                            using (var fileStream = new FileStream(outFileName, FileMode.Open, FileAccess.Read))
                            {
                                fileStream.CopyTo(memStream);
                            }
                            return true;
                        }
                        else
                        {
                            string error = string.Join(" ", converter.ProcessOutput);
                            Log.WriteLine(LogLevel.Error, "EPUB conversion failed: {0}", error);
                        }
                    }
                }
                finally
                {
                    try { File.Delete(inFileName); } catch { }
                    try { File.Delete(outFileName); } catch { }
                }
            }
            catch (Exception ex)
            {
                Log.WriteLine(LogLevel.Error, "FB2 to EPUB conversion error: {0}", ex.Message);
            }
            return false;
        }

        private void HandleImageRequest(HttpProcessor processor, string request)
        {
            try
            {
                bool getCover = request.Contains("/cover/");
                string bookID = ExtractBookIdFromImageRequest(request, getCover);

                if (string.IsNullOrEmpty(bookID))
                {
                    Log.WriteLine(LogLevel.Warning, "Invalid book ID in image request: {0}", request);
                    processor.WriteBadRequest();
                    return;
                }

                Book book = Library.GetBook(bookID);
                if (book == null)
                {
                    Log.WriteLine(LogLevel.Warning, "Book {0} not found for image request", bookID);
                    processor.WriteFailure();
                    return;
                }

                CoverImage image = GetOrCreateCoverImage(bookID, book);
                if (image != null && image.HasImages)
                {
                    Stream imageStream = null;
                    try
                    {
                        imageStream = getCover ? image.CoverImageStream : image.ThumbnailImageStream;

                        if (imageStream != null && imageStream.Length > 0)
                        {
                            if (!processor.OutputStream.BaseStream.CanWrite)
                            {
                                Log.WriteLine(LogLevel.Info, "Client disconnected before sending image for book {0}", bookID);
                                return;
                            }

                            processor.WriteSuccess("image/jpeg");

                            const int bufferSize = 8192;
                            byte[] buffer = new byte[bufferSize];
                            int bytesRead;
                            long totalBytesSent = 0;

                            imageStream.Position = 0;

                            while ((bytesRead = imageStream.Read(buffer, 0, bufferSize)) > 0)
                            {
                                try
                                {
                                    if (!processor.OutputStream.BaseStream.CanWrite)
                                    {
                                        Log.WriteLine(LogLevel.Info, "Client disconnected during image transfer for book {0} after {1} bytes", bookID, totalBytesSent);
                                        break;
                                    }

                                    processor.OutputStream.BaseStream.Write(buffer, 0, bytesRead);
                                    totalBytesSent += bytesRead;
                                }
                                catch (IOException ioEx) when (ioEx.InnerException is SocketException)
                                {
                                    Log.WriteLine(LogLevel.Info, "Client disconnected during image transfer for book {0} after {1} bytes", bookID, totalBytesSent);
                                    break;
                                }
                                catch (ObjectDisposedException)
                                {
                                    Log.WriteLine(LogLevel.Info, "Stream disposed during image transfer for book {0} after {1} bytes", bookID, totalBytesSent);
                                    break;
                                }
                            }

                            if (processor.OutputStream.BaseStream.CanWrite && totalBytesSent == imageStream.Length)
                            {
                                processor.OutputStream.BaseStream.Flush();
                                ServerStatistics.IncrementImagesSent();
                                Log.WriteLine(LogLevel.Info, "Successfully sent {0} image for book {1} ({2} bytes)",
                                    getCover ? "cover" : "thumbnail", bookID, totalBytesSent);
                            }
                        }
                        else
                        {
                            Log.WriteLine(LogLevel.Warning, "Empty or null image stream for book {0}", bookID);
                            processor.WriteFailure();
                        }
                    }
                    catch (IOException ioEx) when (ioEx.InnerException is SocketException)
                    {
                        Log.WriteLine(LogLevel.Info, "Client disconnected while preparing image for book {0}: {1}", bookID, ioEx.Message);
                    }
                    catch (ObjectDisposedException)
                    {
                        Log.WriteLine(LogLevel.Info, "Stream disposed while preparing image for book {0}", bookID);
                    }
                    catch (Exception imgEx)
                    {
                        Log.WriteLine(LogLevel.Error, "Unexpected error sending image for book {0}: {1}", bookID, imgEx.Message);

                        try
                        {
                            if (processor.OutputStream.BaseStream.CanWrite)
                            {
                                processor.WriteFailure();
                            }
                        }
                        catch
                        {
                        }
                    }
                    finally
                    {
                        imageStream?.Dispose();
                    }
                }
                else
                {
                    Log.WriteLine(LogLevel.Warning, "No image available for book {0}", bookID);
                    processor.WriteFailure();
                }
            }
            catch (Exception ex)
            {
                Log.WriteLine(LogLevel.Error, "Image request error: {0}", ex.Message);
                try
                {
                    processor.WriteFailure();
                }
                catch
                {
                }
            }
        }

        private string ExtractBookIdFromImageRequest(string request, bool isCover)
        {
            try
            {
                string prefix = isCover ? "/cover/" : "/thumbnail/";
                int startPos = request.IndexOf(prefix) + prefix.Length;
                int endPos = request.LastIndexOf(".jpeg");

                if (startPos > 0 && endPos > startPos)
                {
                    return request.Substring(startPos, endPos - startPos)
                        .Replace("%7B", "{").Replace("%7D", "}");
                }
            }
            catch (Exception ex)
            {
                Log.WriteLine(LogLevel.Warning, "Error extracting book ID from image request: {0}", ex.Message);
            }
            return null;
        }

        private CoverImage GetOrCreateCoverImage(string bookID, Book book)
        {
            try
            {
                if (ImagesCache.HasImage(bookID))
                {
                    return ImagesCache.GetImage(bookID);
                }

                var image = new CoverImage(book);

                if (image != null && image.HasImages)
                {
                    ImagesCache.Add(image);
                }

                return image;
            }
            catch (Exception ex)
            {
                Log.WriteLine(LogLevel.Error, "Error creating cover image for book {0}: {1}", bookID, ex.Message);
                return null;
            }
        }

        private void HandleIconRequest(HttpProcessor processor, string request)
        {
            try
            {
                string iconName = Path.GetFileName(request);
                string resourceName = Assembly.GetExecutingAssembly().GetName().Name + ".Icons." + iconName;

                using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName))
                {
                    if (stream != null && stream.Length > 0)
                    {
                        processor.WriteSuccess("image/x-icon");
                        stream.CopyTo(processor.OutputStream.BaseStream);
                        processor.OutputStream.BaseStream.Flush();
                        return;
                    }
                }

                Log.WriteLine(LogLevel.Warning, "Icon not found: {0}", iconName);
                processor.WriteFailure();
            }
            catch (Exception ex)
            {
                Log.WriteLine(LogLevel.Error, "Icon request error: {0}", ex.Message);
                processor.WriteFailure();
            }
        }

        #endregion
    }

    // Helper classes for structure-aware catalogs
    internal class RootCatalogWithStructure
    {
        private Dictionary<string, bool> _opdsStructure;

        public RootCatalogWithStructure(Dictionary<string, bool> opdsStructure)
        {
            _opdsStructure = opdsStructure;
        }

        public XDocument GetCatalog()
        {
            var rootCatalog = new RootCatalog().GetCatalog();

            // Remove disabled entries from root catalog
            var entries = rootCatalog.Root.Elements("entry").ToList();
            foreach (var entry in entries)
            {
                var link = entry.Element("link");
                if (link != null)
                {
                    string href = link.Attribute("href")?.Value;
                    if (!string.IsNullOrEmpty(href))
                    {
                        bool shouldRemove = false;

                        if (href.Contains("/newdate") && !IsEnabled("newdate")) shouldRemove = true;
                        else if (href.Contains("/newtitle") && !IsEnabled("newtitle")) shouldRemove = true;
                        else if (href.Contains("/authorsindex") && !IsEnabled("authorsindex")) shouldRemove = true;
                        else if (href.Contains("/sequencesindex") && !IsEnabled("sequencesindex")) shouldRemove = true;
                        else if (href.Contains("/genres") && !IsEnabled("genres")) shouldRemove = true;

                        if (shouldRemove)
                        {
                            entry.Remove();
                        }
                    }
                }
            }

            return rootCatalog;
        }

        private bool IsEnabled(string route)
        {
            return _opdsStructure.ContainsKey(route) && _opdsStructure[route];
        }
    }

    internal class AuthorsCatalogWithStructure
    {
        private Dictionary<string, bool> _opdsStructure;

        public AuthorsCatalogWithStructure(Dictionary<string, bool> opdsStructure)
        {
            _opdsStructure = opdsStructure;
        }

        public XDocument GetCatalog(string searchPattern, bool isOpenSearch, int threshold)
        {
            var authorsCatalog = new AuthorsCatalog().GetCatalog(searchPattern, isOpenSearch, threshold);

            // If author-details is disabled, modify links to go directly to alphabetic books
            if (!IsEnabled("author-details"))
            {
                var entries = authorsCatalog.Root.Elements("entry").ToList();
                foreach (var entry in entries)
                {
                    var link = entry.Element("link");
                    if (link != null && link.Attribute("href")?.Value?.Contains("/author-details/") == true)
                    {
                        string href = link.Attribute("href").Value;
                        string author = href.Replace("/author-details/", "");
                        link.SetAttributeValue("href", "/author-alphabetic/" + author);
                    }
                }
            }

            return authorsCatalog;
        }

        private bool IsEnabled(string route)
        {
            return _opdsStructure.ContainsKey(route) && _opdsStructure[route];
        }
    }

    internal class AuthorDetailsCatalogWithStructure
    {
        private Dictionary<string, bool> _opdsStructure;

        public AuthorDetailsCatalogWithStructure(Dictionary<string, bool> opdsStructure)
        {
            _opdsStructure = opdsStructure;
        }

        public XDocument GetCatalog(string author)
        {
            var authorDetails = new AuthorDetailsCatalog().GetCatalog(author);

            // Remove disabled entries
            var entries = authorDetails.Root.Elements("entry").ToList();
            foreach (var entry in entries)
            {
                var link = entry.Element("link");
                if (link != null)
                {
                    string href = link.Attribute("href")?.Value;
                    if (!string.IsNullOrEmpty(href))
                    {
                        bool shouldRemove = false;

                        if (href.Contains("/author-series/") && !IsEnabled("author-series")) shouldRemove = true;
                        else if (href.Contains("/author-no-series/") && !IsEnabled("author-no-series")) shouldRemove = true;
                        else if (href.Contains("/author-alphabetic/") && !IsEnabled("author-alphabetic")) shouldRemove = true;
                        else if (href.Contains("/author-by-date/") && !IsEnabled("author-by-date")) shouldRemove = true;

                        if (shouldRemove)
                        {
                            entry.Remove();
                        }
                    }
                }
            }

            return authorDetails;
        }

        private bool IsEnabled(string route)
        {
            return _opdsStructure.ContainsKey(route) && _opdsStructure[route];
        }
    }
}