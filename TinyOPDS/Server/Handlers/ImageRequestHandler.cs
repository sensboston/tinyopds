/*
 * This file is part of TinyOPDS server project
 * https://github.com/sensboston/tinyopds
 *
 * Copyright (c) 2013-2025 SeNSSoFT
 * SPDX-License-Identifier: MIT
 *
 * This module handles images requests to
 * the server
 * 
 */

using System;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using TinyOPDS.Data;

namespace TinyOPDS.Server
{
    /// <summary>
    /// Handles image requests (covers and thumbnails) with cancellation support
    /// </summary>
    public class ImageRequestHandler
    {
        /// <summary>
        /// Main entry point for handling image requests with cancellation support
        /// </summary>
        public void HandleImageRequestWithCancellation(HttpProcessor processor, string request, string clientHash)
        {
            CancellationToken cancellationToken = default(CancellationToken);
            bool hasToken = false;
            string bookID = null;

            try
            {
                // Get cancellation token for this client
                if (!string.IsNullOrEmpty(clientHash))
                {
                    cancellationToken = RequestCancellationManager.GetImageRequestToken(clientHash);
                    hasToken = true;
                }

                // Check if already cancelled
                if (hasToken && cancellationToken.IsCancellationRequested)
                {
                    Log.WriteLine(LogLevel.Info, "Image request cancelled before processing: {0}", request);
                    processor.WriteFailure();
                    return;
                }

                bool getCover = request.Contains("/cover/");
                bookID = ExtractBookIdFromImageRequest(request, getCover);

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

                // Check cancellation before expensive operations
                if (hasToken && cancellationToken.IsCancellationRequested)
                {
                    Log.WriteLine(LogLevel.Info, "Image request cancelled before extraction for book {0}", bookID);
                    return;
                }

                var imageObject = GetOrCreateCoverImageWithCancellation(bookID, book, cancellationToken);

                if (imageObject != null)
                {
                    SendImageToClientWithCancellation(processor, imageObject, getCover, bookID, cancellationToken);
                }
                else
                {
                    Log.WriteLine(LogLevel.Warning, "No image available for book {0}", bookID);
                    processor.WriteFailure();
                }
            }
            catch (OperationCanceledException)
            {
                Log.WriteLine(LogLevel.Info, "Image request cancelled for book {0}", bookID ?? "unknown");
            }
            catch (Exception ex)
            {
                Log.WriteLine(LogLevel.Error, "Image request error for book {0}: {1}", bookID ?? "unknown", ex.Message);
                try
                {
                    processor.WriteFailure();
                }
                catch { }
            }
            finally
            {
                if (hasToken && !string.IsNullOrEmpty(clientHash))
                {
                    RequestCancellationManager.CompleteImageRequest(clientHash);
                }
            }
        }

        /// <summary>
        /// Handles image request without cancellation support (legacy)
        /// </summary>
        public void HandleImageRequest(HttpProcessor processor, string request)
        {
            string bookID = null;
            try
            {
                bool getCover = request.Contains("/cover/");
                bookID = ExtractBookIdFromImageRequest(request, getCover);

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

                var imageObject = GetOrCreateCoverImage(bookID, book);

                if (imageObject != null)
                {
                    SendImageToClient(processor, imageObject, getCover, bookID);
                }
                else
                {
                    Log.WriteLine(LogLevel.Warning, "No image available for book {0}", bookID);
                    processor.WriteFailure();
                }
            }
            catch (Exception ex)
            {
                Log.WriteLine(LogLevel.Error, "Image request error for book {0}: {1}", bookID ?? "unknown", ex.Message);
                try
                {
                    processor.WriteFailure();
                }
                catch { }
            }
        }

        /// <summary>
        /// Extracts book ID from image request URL
        /// </summary>
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

        /// <summary>
        /// Gets or creates cover image with cancellation support
        /// </summary>
        private object GetOrCreateCoverImageWithCancellation(string bookID, Book book, CancellationToken cancellationToken)
        {
            try
            {
                // Check cache first
                if (ImagesCache.HasImage(bookID))
                {
                    return ImagesCache.GetImage(bookID);
                }

                // Check cancellation before extraction
                cancellationToken.ThrowIfCancellationRequested();

                // For now, use existing CoverImage class
                // In production, you'd want to modify CoverImage to accept cancellation token
                var image = new CoverImage(book);

                // Simulate cancellation check points during image extraction
                // In real implementation, pass cancellation token to CoverImage constructor
                cancellationToken.ThrowIfCancellationRequested();

                if (image != null && image.HasImages)
                {
                    ImagesCache.Add(image);
                }

                return image;
            }
            catch (OperationCanceledException)
            {
                throw; // Re-throw to handle at higher level
            }
            catch (Exception ex)
            {
                Log.WriteLine(LogLevel.Error, "Error creating cover image for book {0}: {1}", bookID, ex.Message);
                return null;
            }
        }

        /// <summary>
        /// Gets or creates cover image without cancellation support (legacy)
        /// </summary>
        private object GetOrCreateCoverImage(string bookID, Book book)
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

        /// <summary>
        /// Sends image to client with cancellation support
        /// </summary>
        private void SendImageToClientWithCancellation(HttpProcessor processor, object imageObject, bool getCover, string bookID, CancellationToken cancellationToken)
        {
            Stream imageStream = null;
            bool hasImages = false;

            try
            {
                // Handle both CoverImage and CachedCoverImage types
                if (imageObject is CachedCoverImage cachedImage)
                {
                    hasImages = cachedImage.HasImages;
                    if (hasImages)
                    {
                        imageStream = getCover ? cachedImage.CoverImageStream : cachedImage.ThumbnailImageStream;
                    }
                }
                else if (imageObject is CoverImage regularImage)
                {
                    hasImages = regularImage.HasImages;
                    if (hasImages)
                    {
                        imageStream = getCover ? regularImage.CoverImageStream : regularImage.ThumbnailImageStream;
                    }
                }

                if (hasImages && imageStream != null && imageStream.Length > 0)
                {
                    // Check cancellation before sending
                    cancellationToken.ThrowIfCancellationRequested();

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
                        // Check cancellation during transfer
                        cancellationToken.ThrowIfCancellationRequested();

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
                        HttpServer.ServerStatistics.IncrementImagesSent();
                        Log.WriteLine(LogLevel.Info, "Successfully sent {0} image for book {1} ({2} bytes)",
                            getCover ? "cover" : "thumbnail", bookID, totalBytesSent);
                    }
                }
                else
                {
                    Log.WriteLine(LogLevel.Warning, "No image stream available for book {0}", bookID);
                    processor.WriteFailure();
                }
            }
            catch (OperationCanceledException)
            {
                Log.WriteLine(LogLevel.Info, "Image sending cancelled for book {0}", bookID);
                throw;
            }
            finally
            {
                imageStream?.Dispose();
            }
        }

        /// <summary>
        /// Sends image to client without cancellation support (legacy)
        /// </summary>
        private void SendImageToClient(HttpProcessor processor, object imageObject, bool getCover, string bookID)
        {
            Stream imageStream = null;
            bool hasImages = false;

            try
            {
                // Handle both CoverImage and CachedCoverImage types
                if (imageObject is CachedCoverImage cachedImage)
                {
                    hasImages = cachedImage.HasImages;
                    if (hasImages)
                    {
                        imageStream = getCover ? cachedImage.CoverImageStream : cachedImage.ThumbnailImageStream;
                    }
                }
                else if (imageObject is CoverImage regularImage)
                {
                    hasImages = regularImage.HasImages;
                    if (hasImages)
                    {
                        imageStream = getCover ? regularImage.CoverImageStream : regularImage.ThumbnailImageStream;
                    }
                }

                if (hasImages && imageStream != null && imageStream.Length > 0)
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
                        HttpServer.ServerStatistics.IncrementImagesSent();
                        Log.WriteLine(LogLevel.Info, "Successfully sent {0} image for book {1} ({2} bytes)",
                            getCover ? "cover" : "thumbnail", bookID, totalBytesSent);
                    }
                }
                else
                {
                    Log.WriteLine(LogLevel.Warning, "No image available for book {0}", bookID);
                    processor.WriteFailure();
                }
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
                catch { }
            }
            finally
            {
                imageStream?.Dispose();
            }
        }
    }
}