/***********************************************************
 * This file is a part of TinyOPDS server project
 * 
 * Copyright (c) 2013 SeNSSoFT
 *
 * This code is licensed under the Microsoft Public License, 
 * see http://tinyopds.codeplex.com/license for the details.
 *
 * This module defines OPDS HTTP server class
 * 
 ************************************************************/

using System;
using System.IO;
using System.IO.Compression;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Net;
using System.Xml;
using System.Xml.Xsl;
using System.Xml.XPath;
using System.Reflection;

using Ionic.Zip;
using TinyOPDS.OPDS;
using TinyOPDS.Data;

namespace TinyOPDS.Server
{
    public class OPDSServer : HttpServer
    {
        private string[] _extensions = { ".zip", ".epub", ".jpeg", ".ico", ".xml" };
        private XslCompiledTransform _xslTransform = new XslCompiledTransform();

        public OPDSServer(IPAddress interfaceIP, int port, int timeout = 5000) : base(interfaceIP, port, timeout)
        {
            // Check external template (will be used instead of built-in)
            string xslFileName = Path.Combine(Utils.ServiceFilesLocation, "xml2html.xsl");

            if (File.Exists(xslFileName))
            {
                _xslTransform.Load(xslFileName);
            }
            else
            {
                using (Stream resStream = Assembly.GetExecutingAssembly().GetManifestResourceStream(Assembly.GetExecutingAssembly().GetName().Name + ".xml2html.xsl"))
                {
                    using (XmlReader reader = XmlReader.Create(resStream))
                        _xslTransform.Load(reader);
                }
            }
        }

        /// <summary>
        /// Dummy for POST requests
        /// </summary>
        /// <param name="p"></param>
        /// <param name="inputData"></param>
        public override void HandlePOSTRequest(HttpProcessor processor, StreamReader inputData)
        {
            Log.WriteLine(LogLevel.Warning, "HTTP POST request from {0}: {1}  : NOT IMPLEMENTED", ((System.Net.IPEndPoint)processor.Socket.Client.RemoteEndPoint).Address, processor.HttpUrl);
        }

        /// <summary>
        /// POST requests handler
        /// </summary>
        /// <param name="p"></param>
        public override void HandleGETRequest(HttpProcessor processor)
        {
            Log.WriteLine("HTTP GET request from {0}: {1}", ((System.Net.IPEndPoint)processor.Socket.Client.RemoteEndPoint).Address, processor.HttpUrl);
            try
            {
                // Parse request
                string xml = string.Empty;
                string request = processor.HttpUrl;

                // Check for www request
                bool isWWWRequest = request.StartsWith("/" + TinyOPDS.Properties.Settings.Default.HttpPrefix) && !request.StartsWith("/" + TinyOPDS.Properties.Settings.Default.RootPrefix) ? true : false;

                // Remove prefix if any
                if (!request.Contains("opds-opensearch.xml") && !string.IsNullOrEmpty(TinyOPDS.Properties.Settings.Default.RootPrefix))
                {
                    request = request.Replace(TinyOPDS.Properties.Settings.Default.RootPrefix, "/");
                }
                if (!string.IsNullOrEmpty(TinyOPDS.Properties.Settings.Default.HttpPrefix))
                {
                    request = request.Replace(TinyOPDS.Properties.Settings.Default.HttpPrefix, "/");
                }

                while (request.IndexOf("//") >= 0) request = request.Replace("//", "/");

                // Remove any parameters from request except TinyOPDS params
                int paramPos = request.IndexOf('?');
                if (paramPos >= 0)
                {
                    int ourParamPos = request.IndexOf("pageNumber") + request.IndexOf("searchTerm");
                    if (ourParamPos >= 0)
                    {
                        ourParamPos = request.IndexOf('&', ourParamPos + 10);
                        if (ourParamPos >= 0) request = request.Substring(0, ourParamPos);
                    }
                    else request = request.Substring(0, paramPos);
                }

                string ext = Path.GetExtension(request).ToLower();
                if (!_extensions.Contains(ext)) ext = string.Empty;

                string[] http_params = request.Split(new Char[] { '?', '=', '&' });

                // User-agent check: some e-book readers can handle fb2 files (no conversion is  needed)
                string userAgent = processor.HttpHeaders["User-Agent"] as string;
                bool acceptFB2 = Utils.DetectFB2Reader(userAgent) || isWWWRequest;
                int threshold = (int) (isWWWRequest ? TinyOPDS.Properties.Settings.Default.ItemsPerWebPage : TinyOPDS.Properties.Settings.Default.ItemsPerOPDSPage);

                // Is it OPDS request?
                if (string.IsNullOrEmpty(ext))
                {
                    try
                    {
                        // Is it root node requested?
                        if (request.Equals("/"))
                        {
                            xml = new RootCatalog().GetCatalog().ToStringWithDeclaration();
                        }
                        else if (request.StartsWith("/newdate"))
                        {
                            xml = new NewBooksCatalog().GetCatalog(request.Substring(8), true, acceptFB2, threshold).ToStringWithDeclaration();
                        }
                        else if (request.StartsWith("/newtitle"))
                        {
                            xml = new NewBooksCatalog().GetCatalog(request.Substring(9), false, acceptFB2, threshold).ToStringWithDeclaration();
                        }
                        else if (request.StartsWith("/authorsindex"))
                        {
                            int numChars = request.StartsWith("/authorsindex/") ? 14 : 13;
                            xml = new AuthorsCatalog().GetCatalog(request.Substring(numChars), false, threshold).ToStringWithDeclaration();
                        }
                        else if (request.StartsWith("/author/"))
                        {
                            xml = new BooksCatalog().GetCatalogByAuthor(request.Substring(8), acceptFB2, threshold).ToStringWithDeclaration();
                        }
                        else if (request.StartsWith("/sequencesindex"))
                        {
                            int numChars = request.StartsWith("/sequencesindex/") ? 16 : 15;
                            xml = new SequencesCatalog().GetCatalog(request.Substring(numChars), threshold).ToStringWithDeclaration();
                        }
                        else if (request.Contains("/sequence/"))
                        {
                            xml = new BooksCatalog().GetCatalogBySequence(request.Substring(10), acceptFB2, threshold).ToStringWithDeclaration();
                        }
                        else if (request.StartsWith("/genres"))
                        {
                            int numChars = request.Contains("/genres/") ? 8 : 7;
                            xml = new GenresCatalog().GetCatalog(request.Substring(numChars)).ToStringWithDeclaration();
                        }
                        else if (request.StartsWith("/genre/"))
                        {
                            xml = new BooksCatalog().GetCatalogByGenre(request.Substring(7), acceptFB2, threshold).ToStringWithDeclaration();
                        }
                        else if (request.StartsWith("/search"))
                        {
                            if (http_params.Length > 1 && http_params[1].Equals("searchTerm"))
                            {
                                xml = new OpenSearch().Search(http_params[2], "", acceptFB2).ToStringWithDeclaration();
                            }
                            else if (http_params[1].Equals("searchType"))
                            {
                                int pageNumber = 0;
                                if (http_params.Length > 6 && http_params[5].Equals("pageNumber"))
                                {
                                    int.TryParse(http_params[6], out pageNumber);
                                }
                                xml = new OpenSearch().Search(http_params[4], http_params[2], acceptFB2, pageNumber).ToStringWithDeclaration();
                            }
                        }

                        if (string.IsNullOrEmpty(xml))
                        {
                            processor.WriteFailure();
                            return;
                        }

                        // Fix for the root namespace
                        // TODO: fix with standard way (how?)
                        xml = xml.Insert(xml.IndexOf("<feed ")+5, " xmlns=\"http://www.w3.org/2005/Atom\"");

                        if (TinyOPDS.Properties.Settings.Default.UseAbsoluteUri)
                        {
                            try
                            {
                                string host = processor.HttpHeaders["Host"].ToString();
                                xml = xml.Replace("href=\"", "href=\"http://" + (isWWWRequest ? host.UrlCombine(TinyOPDS.Properties.Settings.Default.HttpPrefix) : host.UrlCombine(TinyOPDS.Properties.Settings.Default.RootPrefix)));
                            }
                            catch { }
                        }
                        else
                        {
                            string prefix = isWWWRequest ? TinyOPDS.Properties.Settings.Default.HttpPrefix : TinyOPDS.Properties.Settings.Default.RootPrefix;
                            if (!string.IsNullOrEmpty(prefix)) prefix = "/" + prefix;
                            xml = xml.Replace("href=\"", "href=\"" + prefix);
                            // Fix open search link
                            xml = xml.Replace(prefix + "/opds-opensearch.xml", "/opds-opensearch.xml");
                        }

                        // Apply xsl transform
                        if (isWWWRequest)
                        {
                            string html = string.Empty;

                            MemoryStream htmlStream = new MemoryStream();
                            using (StringReader stream = new StringReader(xml))
                            {
                                XPathDocument myXPathDoc = new XPathDocument(stream);

// for easy debug of xsl transform, we'll reload external file in DEBUG build
#if DEBUG 
                                string xslFileName = Path.Combine(Utils.ServiceFilesLocation, "xml2html.xsl");
                                _xslTransform = new XslCompiledTransform();
                                if (File.Exists(xslFileName))
                                {
                                    _xslTransform.Load(xslFileName);
                                }
                                else 
                                {
                                    using (Stream resStream = Assembly.GetExecutingAssembly().GetManifestResourceStream(Assembly.GetExecutingAssembly().GetName().Name + ".xml2html.xsl"))
                                    {
                                        using (XmlReader reader = XmlReader.Create(resStream)) 
                                            _xslTransform.Load(reader);
                                    }
                                }
#endif
                                XmlTextWriter myWriter = new XmlTextWriter(htmlStream, null);
                                _xslTransform.Transform(myXPathDoc, null, myWriter);
                                htmlStream.Position = 0;
                                using (StreamReader sr = new StreamReader(htmlStream)) html = sr.ReadToEnd();
                            }

                            processor.WriteSuccess("text/html");
                            processor.OutputStream.Write(html);
                        }
                        else
                        {
                            processor.WriteSuccess("application/atom+xml;charset=utf-8");
                            processor.OutputStream.Write(xml);
                        }
                    }
                    catch (Exception e)
                    {
                        Log.WriteLine(LogLevel.Error, "OPDS catalog exception {0}", e.Message);
                    }
                    return;
                }
                else if (request.Contains("opds-opensearch.xml"))
                {
                    xml = new OpenSearch().OpenSearchDescription().ToStringWithDeclaration();
                    xml = xml.Insert(xml.IndexOf("<OpenSearchDescription")+22, " xmlns=\"http://a9.com/-/spec/opensearch/1.1/\"");

                    if (TinyOPDS.Properties.Settings.Default.UseAbsoluteUri)
                    {
                        try
                        {
                            string host = processor.HttpHeaders["Host"].ToString();
                            xml = xml.Replace("href=\"", "href=\"http://" + host.UrlCombine(TinyOPDS.Properties.Settings.Default.RootPrefix));
                        }
                        catch { }
                    }

                    processor.WriteSuccess("application/atom+xml;charset=utf-8");
                    processor.OutputStream.Write(xml);
                    return;
                }

                // fb2.zip book request
                else if ((request.Contains(".fb2.zip") && ext.Equals(".zip")) || ext.Equals(".epub"))
                {
                    string bookID = request.Substring(1, request.IndexOf('/', 1) - 1).Replace("%7B", "{").Replace("%7D", "}");
                    Book book = Library.GetBook(bookID);

                    if (book != null)
                    {
                        MemoryStream memStream = null;
                        memStream = new MemoryStream();

                        if (request.Contains(".fb2.zip"))
                        {
                            try
                            {
                                if (book.FilePath.ToLower().Contains(".zip@"))
                                {
                                    string[] pathParts = book.FilePath.Split('@');
                                    using (ZipFile zipFile = new ZipFile(pathParts[0]))
                                    {
                                        ZipEntry entry = zipFile.Entries.First(e => e.FileName.Contains(pathParts[1]));
                                        if (entry != null) entry.Extract(memStream);
                                    }
                                }
                                else
                                {
                                    using (FileStream stream = new FileStream(book.FilePath, FileMode.Open, FileAccess.Read, FileShare.Read))
                                        stream.CopyTo(memStream);
                                }
                                memStream.Position = 0;

                                // Compress fb2 document to zip
                                using (ZipFile zip = new ZipFile())
                                {
                                    zip.AddEntry(Transliteration.Front(string.Format("{0}_{1}.fb2", book.Authors.First(), book.Title)), memStream);
                                    using (MemoryStream outputStream = new MemoryStream())
                                    {
                                        zip.Save(outputStream);
                                        outputStream.Position = 0;
                                        processor.WriteSuccess("application/fb2+zip");
                                        outputStream.CopyTo(processor.OutputStream.BaseStream);
                                    }
                                }
                                HttpServer.ServerStatistics.BooksSent++;
                            }
                            catch (Exception e)
                            {
                                Log.WriteLine(LogLevel.Error, "FB2 file exception {0}", e.Message);
                            }
                        }
                        else if (ext.Equals(".epub"))
                        {
                            try
                            {
                                if (book.FilePath.ToLower().Contains(".zip@"))
                                {
                                    string[] pathParts = book.FilePath.Split('@');
                                    using (ZipFile zipFile = new ZipFile(pathParts[0]))
                                    {
                                        ZipEntry entry = zipFile.Entries.First(e => e.FileName.Contains(pathParts[1]));
                                        if (entry != null) entry.Extract(memStream);
                                        entry = null;
                                    }
                                }
                                else
                                {
                                    using (FileStream stream = new FileStream(book.FilePath, FileMode.Open, FileAccess.Read, FileShare.Read))
                                        stream.CopyTo(memStream);
                                }
                                memStream.Position = 0;
                                // At this moment, memStream has a copy of requested book
                                // For fb2, we need convert book to epub
                                if (book.BookType == BookType.FB2)
                                {
                                    // No convertor found, return an error
                                    if (string.IsNullOrEmpty(TinyOPDS.Properties.Settings.Default.ConvertorPath))
                                    {
                                        Log.WriteLine(LogLevel.Error, "No FB2 to EPUB convertor found, file request can not be completed!");
                                        processor.WriteFailure();
                                        return;
                                    }

                                    // Save fb2 book to the temp folder
                                    string inFileName = Path.Combine(Path.GetTempPath(), book.ID + ".fb2");
                                    using (FileStream stream = new FileStream(inFileName, FileMode.Create, FileAccess.ReadWrite, FileShare.ReadWrite))
                                        memStream.CopyTo(stream);

                                    // Run converter 
                                    string outFileName = Path.Combine(Path.GetTempPath(), book.ID + ".epub");
                                    string command = Path.Combine(TinyOPDS.Properties.Settings.Default.ConvertorPath, Utils.IsLinux ? "fb2toepub" : "Fb2ePub.exe");
                                    string arguments = string.Format(Utils.IsLinux ? "{0} {1}" : "\"{0}\" \"{1}\"", inFileName, outFileName);

                                    using (ProcessHelper converter = new ProcessHelper(command, arguments))
                                    {
                                        converter.Run();

                                        if (File.Exists(outFileName))
                                        {
                                            memStream = new MemoryStream();
                                            using (FileStream fileStream = new FileStream(outFileName, FileMode.Open, FileAccess.Read, FileShare.Read))
                                                fileStream.CopyTo(memStream);

                                            // Cleanup temp folder
                                            try { File.Delete(inFileName); }
                                            catch { }
                                            try { File.Delete(outFileName); }
                                            catch { }
                                        }
                                        else
                                        {
                                            string converterError = string.Empty;
                                            foreach (string s in converter.ProcessOutput) converterError += s + " ";
                                            Log.WriteLine(LogLevel.Error, "EPUB conversion error on file {0}. Error description: {1}", inFileName, converterError);
                                            processor.WriteFailure();
                                            return;
                                        }
                                    }
                                }

                                // At this moment, memStream has a copy of epub
                                processor.WriteSuccess("application/epub+zip");
                                memStream.Position = 0;
                                memStream.CopyTo(processor.OutputStream.BaseStream);
                                HttpServer.ServerStatistics.BooksSent++;
                            }

                            catch (Exception e)
                            {
                                Log.WriteLine(LogLevel.Error, "EPUB file exception {0}", e.Message);
                            }
                        }

                        processor.OutputStream.BaseStream.Flush();
                        if (memStream != null) memStream.Dispose();
                    }
                    else
                    {
                        Log.WriteLine(LogLevel.Error, "Book {0} not found in library.", bookID);
                    }
                }
                // Cover image or thumbnail request
                else if (ext.Contains(".jpeg"))
                {
                    bool getCover = true;
                    string bookID = string.Empty;
                    if (request.Contains("/cover/"))
                    {
                        bookID = Path.GetFileNameWithoutExtension(request.Substring(request.IndexOf("/cover/") + 7));
                    }
                    else if (request.Contains("/thumbnail/"))
                    {
                        bookID = Path.GetFileNameWithoutExtension(request.Substring(request.IndexOf("/thumbnail/") + 11));
                        getCover = false;
                    }

                    bookID = bookID.Replace("%7B", "{").Replace("%7D", "}");

                    if (!string.IsNullOrEmpty(bookID))
                    {
                        CoverImage image = null;
                        Book book = Library.GetBook(bookID);

                        if (book != null)
                        {
                            if (ImagesCache.HasImage(bookID)) image = ImagesCache.GetImage(bookID);
                            else
                            {
                                image = new CoverImage(book);
                                if (image != null && image.HasImages) ImagesCache.Add(image);
                            }

                            if (image != null && image.HasImages)
                            {
                                processor.WriteSuccess("image/jpeg");
                                (getCover ? image.CoverImageStream : image.ThumbnailImageStream).CopyTo(processor.OutputStream.BaseStream);
                                processor.OutputStream.BaseStream.Flush();
                                HttpServer.ServerStatistics.ImagesSent++;
                                return;
                            }
                        }
                    }
                }
                // favicon.ico request
                else if (ext.Contains(".ico"))
                {
                    string icon = Path.GetFileName(request);
                    Stream stream = System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream(Assembly.GetExecutingAssembly().GetName().Name + ".Icons." + icon);
                    if (stream != null && stream.Length > 0)
                    {
                        processor.WriteSuccess("image/x-icon");
                        stream.CopyTo(processor.OutputStream.BaseStream);
                        processor.OutputStream.BaseStream.Flush();
                        return;
                    }
                }
                processor.WriteFailure();
            }
            catch (Exception e)
            {
                Log.WriteLine(LogLevel.Error, ".HandleGETRequest() exception {0}", e.Message);
                processor.WriteFailure();
            }
        }
    }
}
