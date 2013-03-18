using System;
using System.IO;
using System.IO.Compression;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Net;

using Ionic.Zip;
using TinyOPDS.OPDS;
using TinyOPDS.Data;

namespace TinyOPDS.Server
{
    public class OPDSServer : HttpServer
    {
        private static string[] fb2Clients = new string[] { "FBREADER", "MOON+ READER" };

        public OPDSServer(int port, int timeout = 5000) : base(port, timeout) { }

        /// <summary>
        /// Dummy for POST requests
        /// </summary>
        /// <param name="p"></param>
        /// <param name="inputData"></param>
        public override void HandlePOSTRequest(HttpProcessor processor, StreamReader inputData)
        {
            Log.WriteLine("HTTP POST request from {0}: {1}  : NOT IMPLEMENTED", ((System.Net.IPEndPoint)processor.Socket.Client.RemoteEndPoint).Address, processor.HttpUrl);
        }

        /// <summary>
        /// POST requests handler
        /// </summary>
        /// <param name="p"></param>
        public override void HandleGETRequest(HttpProcessor processor)
        {
            Log.WriteLine("HTTP GET request from {0}: {1}", ((System.Net.IPEndPoint) processor.Socket.Client.RemoteEndPoint).Address, processor.HttpUrl);
#if DEBUG
            DateTime startTime = DateTime.Now;
#endif

            try
            {
                // Parse request
                string xml = string.Empty;
                string request = processor.HttpUrl.Replace(Properties.Settings.Default.RootPrefix, "").Replace("//", "/").ToLower();
                string ext = Path.GetExtension(request);
                string[] http_params = request.Split(new Char[] { '?', '=', '&' });

                // User-agent check: some e-book readers can handle fb2 files (no conversion is  needed)
                bool fb2Only = false;
                if (!string.IsNullOrEmpty(processor.HttpHeaders["User-Agent"] as string))
                {
                    foreach (string userAgent in fb2Clients)
                    {
                        fb2Only |= (processor.HttpHeaders["User-Agent"] as string).ToUpper().Contains(userAgent);
                        if (fb2Only) break;
                    }
                }

                // Is it OPDS request?
                if (string.IsNullOrEmpty(ext))
                {
                    try
                    {
                        // Is it root node requested?
                        if (request.Equals("/"))
                        {
                            xml = new RootCatalog().Catalog.ToString();
                        }
                        else if (request.StartsWith("/authorsindex"))
                        {
                            int numChars = request.StartsWith("/authorsindex/") ? 14 : 13;
                            xml = new AuthorsCatalog().GetCatalog(request.Substring(numChars)).ToString();
                        }
                        else if (request.StartsWith("/author/"))
                        {
                            xml = new BooksCatalog().GetCatalogByAuthor(request.Substring(8), fb2Only).ToString();
                        }
                        else if (request.StartsWith("/sequencesindex"))
                        {
                            int numChars = request.StartsWith("/sequencesindex/") ? 16 : 15;
                            xml = new SequencesCatalog().GetCatalog(request.Substring(numChars)).ToString();
                        }
                        else if (request.Contains("/sequence/"))
                        {
                            xml = new BooksCatalog().GetCatalogBySequence(request.Substring(10), fb2Only).ToString();
                        }
                        else if (request.StartsWith("/genres"))
                        {
                            int numChars = request.Contains("/genres/") ? 8 : 7;
                            xml = new GenresCatalog().GetCatalog(request.Substring(numChars)).ToString();
                        }
                        else if (request.StartsWith("/genre/"))
                        {
                            xml = new BooksCatalog().GetCatalogByGenre(request.Substring(7), fb2Only).ToString();
                        }
                        else if (request.StartsWith("/search"))
                        {
                            if (http_params[1].Equals("searchTerm"))
                            {
                                xml = new OpenSearch().Search(http_params[2], "", fb2Only).ToString();
                            }
                            else if (http_params[1].Equals("searchType"))
                            {
                                xml = new OpenSearch().Search(http_params[4], http_params[2], fb2Only).ToString();
                            }
                        }

                        // Modify and send xml back to the client app
                        xml = "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n" + xml.Insert(5, " xmlns=\"http://www.w3.org/2005/Atom\"");
                        string host = string.Empty;
                        try { host = processor.HttpHeaders["Host"].ToString(); } catch { }
                        xml = xml.Replace("{$HOST}", host.UrlCombine(Properties.Settings.Default.RootPrefix));

                        /// Unfortunately, current OPDS-enabled apps don't support this feature, even those that pretend to (like FBReader for Android)
#if USE_GZIP_ENCODING
                // Compress xml if compression supported
                if (!processor.HttpHeaders.ContainsValue("gzip"))
                {

                    byte[] temp = Encoding.UTF8.GetBytes(xml);
                    using (MemoryStream inStream = new MemoryStream(temp))
                    using (MemoryStream outStream = new MemoryStream())
                    using (GZipStream gzipStream = new GZipStream(outStream, CompressionMode.Compress))
                    {
                        inStream.CopyTo(gzipStream);
                        outStream.Position = 0;
                        processor.WriteSuccess("application/atom+xml;charset=utf=8",true);
                        outStream.CopyTo(processor.OutputStream.BaseStream);
                    }
                }
                else
#endif
                        {
                            processor.WriteSuccess("application/atom+xml;charset=utf=8");
                            processor.OutputStream.Write(xml);
                        }
                    }
                    catch (Exception e)
                    {
                        Log.WriteLine(LogLevel.Error, "OPDS catalog exception {0}", e.Message);
                    }
                    return;
                }
                // fb2.zip book request
                else if (request.Contains(".fb2.zip"))
                {
                    MemoryStream memStream = null;
                    try
                    {
                        memStream = new MemoryStream();
                        Book book = Library.GetBook(request.Substring(1, request.IndexOf('/', 1) - 1));

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
                            ZipEntry e = zip.AddEntry(Transliteration.Front(string.Format("{0}_{1}.fb2", book.Authors.First(), book.Title)), memStream);
                            using (MemoryStream outputStream = new MemoryStream())
                            {
                                zip.Save(outputStream);
                                outputStream.Position = 0;
                                processor.WriteSuccess("application/fb2+zip");
                                outputStream.CopyTo(processor.OutputStream.BaseStream);
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        Log.WriteLine(LogLevel.Error, "FB2 file exception {0}", e.Message);
                    }
                    finally
                    {
                        if (memStream != null) memStream.Dispose();
                    }
                    return;
                }
                // epub book request
                else if (ext.Contains(".epub"))
                {
                    MemoryStream memStream = null;
                    try
                    {
                        memStream = new MemoryStream();
                        Book book = Library.GetBook(request.Substring(1, request.IndexOf('/', 1) - 1));

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
                        if (book.BookType == BookType.FB2 && !string.IsNullOrEmpty(Properties.Settings.Default.ConvertorPath))
                        {
                            // Save fb2 book to the temp folder
                            string inFileName = Path.Combine(Path.GetTempPath(), book.ID + ".fb2");
                            using (FileStream stream = new FileStream(inFileName, FileMode.Create, FileAccess.ReadWrite, FileShare.ReadWrite))
                                memStream.CopyTo(stream);

                            // Run converter 
#if DEBUG
                            DateTime convertStartTime = DateTime.Now;
#endif
                            string converterPath = Path.Combine(Properties.Settings.Default.ConvertorPath, "Fb2ePub.exe");
                            using (ProcessHelper converter = new ProcessHelper(converterPath, inFileName))
                            {
                                using (AutoResetEvent waitEvent = new AutoResetEvent(false))
                                {
                                    converter.RunAsync(waitEvent);
                                    waitEvent.WaitOne(10000);
                                }
#if DEBUG
                                Log.WriteLine("EPUB conversion time = {0}", DateTime.Now.Subtract(convertStartTime));
#endif
                            }

                            string outFileName = Path.Combine(Path.GetTempPath(), book.ID + ".epub");
                            if (File.Exists(outFileName))
                            {
                                memStream = new MemoryStream();
                                using (FileStream fileStream = new FileStream(outFileName, FileMode.Open, FileAccess.Read, FileShare.Read))
                                    fileStream.CopyTo(memStream);

                                // Cleanup temp folder
                                try { File.Delete(inFileName); } catch { }
                                try { File.Delete(outFileName); } catch { }
                            }
                        }

                        // At this moment, memStream has a copy of epub
                        processor.WriteSuccess("application/epub+zip");
                        memStream.Position = 0;
                        memStream.CopyTo(processor.OutputStream.BaseStream);
                    }
                    catch (Exception e)
                    {
                        Log.WriteLine(LogLevel.Error, "EPUB file exception {0}", e.Message);
                    }
                    finally
                    {
                        if (memStream != null) memStream.Dispose();
                    }
                    return;
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
                                if (image != null) ImagesCache.Add(image);
                            }

                            if (image != null && image.HasImages)
                            {
                                processor.WriteSuccess("image/jpeg");
                                (getCover ? image.CoverImageStream : image.ThumbnailImageStream).CopyTo(processor.OutputStream.BaseStream);
                                return;
                            }
                        }
                    }
                }
                // favicon.ico request
                else if (ext.Contains(".ico"))
                {
                    string icon = Path.GetFileName(request);
                    Stream stream = System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream("TinyOPDS.Icons." + icon);
                    if (stream != null && stream.Length > 0)
                    {
                        processor.WriteSuccess("image/x-icon");
                        stream.CopyTo(processor.OutputStream.BaseStream);
                        return;
                    }
                }
                processor.WriteFailure();
            }
            catch
            {
                processor.WriteFailure();
            }
            finally
            {
#if DEBUG
                //Log.WriteLine("HTTP request handling time = {0}", DateTime.Now.Subtract(startTime));
#endif
            }
        }
    }
}
