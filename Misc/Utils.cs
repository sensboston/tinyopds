/***********************************************************
 * This file is a part of TinyOPDS server project
 * 
 * Copyright (c) 2013 SeNSSoFT
 *
 * This code is licensed under the Microsoft Public License, 
 * see http://tinyopds.codeplex.com/license for the details.
 *
 * This module contains string extensions and some helpers
 * 
 ************************************************************/

using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Linq;
using System.Text;

namespace TinyOPDS
{
    public static class StringExtension
    {
        /// <summary>
        /// Capitalize words in the string (for example, author's name: first, middle last), by converting first char of every word to uppercase
        /// </summary>
        /// <param name="str">source string</param>
        /// <param name="onlyFirstWord">capitalize first word only</param>
        /// <returns></returns>
        public static string Capitalize(this string str, bool onlyFirstWord = false)
        {
            string[] words = str.Split(' ');
            str = string.Empty;
            for (int i = 0; i < words.Length; i++)
            {
                if (!onlyFirstWord || (onlyFirstWord && i == 0))
                {
                    if (words[i].Length > 1)
                    {
                        if (words[i].IsUpper()) words[i] = words[i].ToLower();
                        words[i] = char.ToUpper(words[i][0]) + words[i].Substring(1);
                    }
                    else
                    {
                        words[i] = words[i].ToUpper();
                    }
                }
                str += words[i] + " ";
            }
            return str.Trim();
        }

        public static bool IsUpper(this string str)
        {
            bool isUpper = true;
            foreach (char c in str) isUpper &= char.IsUpper(c);
            return isUpper;
        }
    }

    public class Utils
    {
        private static string[] fb2Clients = new string[] { "fbreader", "moon+ reader" };
        /// <summary>
        /// Detect eBook readers with fb2 support
        /// </summary>
        /// <param name="userAgent"></param>
        /// <returns>true if reader supports fb2 format</returns>
        public static bool DetectFB2Reader(string userAgent)
        {
            if (!string.IsNullOrEmpty(userAgent))
            {
                foreach (string s in fb2Clients)
                {
                    if (userAgent.IndexOf(s, StringComparison.OrdinalIgnoreCase) >= 0) return true;
                }
            }
            return false;
        }

        private static string[] browsers = new string[] { "opera", "aol", "msie", "firefox", "chrome", "mozilla", "safari", "netscape", "navigator", "mosaic", "lynx", 
                                                          "amaya", "omniweb", "avant", "camino", "flock", "seamonkey", "konqueror", "gecko", "yandex.browser" };
        /// <summary>
        /// Detect browsers by User-Agent
        /// </summary>
        /// <param name="userAgent"></param>
        /// <returns>true if it's browser request</returns>
        public static bool DetectBrowser(string userAgent)
        {
            if (!string.IsNullOrEmpty(userAgent))
            {
                foreach (string s in browsers)
                {
                    if (userAgent.IndexOf(s, StringComparison.OrdinalIgnoreCase) >= 0) return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Helper for project Mono
        /// </summary>
        public static bool IsLinux
        {
            get
            {
                int p = (int)Environment.OSVersion.Platform;
                return (p == 4) || (p == 6) || (p == 128);
            }
        }

        // Default path to service files: databases, log, setting
        public static string ServiceFilesLocation
        {
            get
            {
                //return Properties.Settings.Default.ServiceFilesPath;
                return Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
            }
        }

        // Assembly version
        public static Version Version
        {
            get
            {
                return System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
            }
        }

        public static string ServerVersionName
        {
            get
            {
                return string.Format("running on TinyOPDS server version {0}.{1}", Version.Major, Version.Minor); 
            }
        }

		/// <summary>
		/// Creates a name-based UUID using the algorithm from RFC 4122 §4.3.
		/// </summary>
		/// <param name="namespaceId">The ID of the namespace.</param>
		/// <param name="name">The name (within that namespace).</param>
		/// <param name="version">The version number of the UUID to create; this value must be either
		/// <returns>A UUID derived from the namespace and name.</returns>
		public static Guid CreateGuid(Guid namespaceId, string name)
		{
			if (name == null) throw new ArgumentNullException("name");
			// convert the name to a sequence of octets (as defined by the standard or conventions of its namespace) (step 3)
			// ASSUME: UTF-8 encoding is always appropriate
			byte[] nameBytes = Encoding.UTF8.GetBytes(name);

			// convert the namespace UUID to network order (step 3)
			byte[] namespaceBytes = namespaceId.ToByteArray();
			SwapByteOrder(namespaceBytes);

			// compute the hash of the name space ID concatenated with the name (step 4)
            byte[] hash = namespaceId.ToByteArray();
			using (SHA256 algorithm = new SHA256Managed())
			{
				algorithm.TransformBlock(namespaceBytes, 0, namespaceBytes.Length, hash, 0);
				algorithm.TransformFinalBlock(nameBytes, 0, nameBytes.Length);
				hash = algorithm.Hash;
			}

			// most bytes from the hash are copied straight to the bytes of the new GUID (steps 5-7, 9, 11-12)
			byte[] newGuid = new byte[16];
			Array.Copy(hash, 0, newGuid, 0, 16);

			// set the four most significant bits (bits 12 through 15) of the time_hi_and_version field to the appropriate 4-bit version number from Section 4.1.3 (step 8)
			newGuid[6] = (byte) ((newGuid[6] & 0x0F) | (5 << 4));

			// set the two most significant bits (bits 6 and 7) of the clock_seq_hi_and_reserved to zero and one, respectively (step 10)
			newGuid[8] = (byte) ((newGuid[8] & 0x3F) | 0x80);

			// convert the resulting UUID to local byte order (step 13)
			SwapByteOrder(newGuid);
			return new Guid(newGuid);
		}

		/// <summary>
		/// The namespace for fully-qualified domain names (from RFC 4122, Appendix C).
		/// </summary>
		public static readonly Guid DnsNamespace = new Guid("6ba7b810-9dad-11d1-80b4-00c04fd430c8");

		/// <summary>
		/// The namespace for URLs (from RFC 4122, Appendix C).
		/// </summary>
		public static readonly Guid UrlNamespace = new Guid("6ba7b811-9dad-11d1-80b4-00c04fd430c8");

		/// <summary>
		/// The namespace for ISO OIDs (from RFC 4122, Appendix C).
		/// </summary>
		public static readonly Guid IsoOidNamespace = new Guid("6ba7b812-9dad-11d1-80b4-00c04fd430c8");

		// Converts a GUID (expressed as a byte array) to/from network order (MSB-first).
		internal static void SwapByteOrder(byte[] guid)
		{
			SwapBytes(guid, 0, 3);
			SwapBytes(guid, 1, 2);
			SwapBytes(guid, 4, 5);
			SwapBytes(guid, 6, 7);
		}

		private static void SwapBytes(byte[] guid, int left, int right)
		{
			byte temp = guid[left];
			guid[left] = guid[right];
			guid[right] = temp;
		}
	}

    /// <summary>
    /// Gives us a handy way to modify a collection while we're iterating through it.
    /// 
    /// </summary>
    /// Example of usage:
    /// foreach (Book book in new IteratorIsolateCollection(Library.Books.Values))
    /// {
    ///     book.Title = book.Title.ToUpper();
    /// }
    public class IteratorIsolateCollection : IEnumerable
    {
        IEnumerable _enumerable;

        public IteratorIsolateCollection(IEnumerable enumerable)
        {
            _enumerable = enumerable;
        }

        public IEnumerator GetEnumerator()
        {
            return new IteratorIsolateEnumerator(_enumerable.GetEnumerator());
        }

        internal class IteratorIsolateEnumerator : IEnumerator
        {
            ArrayList items = new ArrayList();
            int currentItem;

            internal IteratorIsolateEnumerator(IEnumerator enumerator)
            {
                while (enumerator.MoveNext() != false)
                {
                    items.Add(enumerator.Current);
                }
                IDisposable disposable = enumerator as IDisposable;
                if (disposable != null)
                {
                    disposable.Dispose();
                }
                currentItem = -1;
            }

            public void Reset()
            {
                currentItem = -1;
            }

            public bool MoveNext()
            {
                currentItem++;
                if (currentItem == items.Count)
                    return false;

                return true;
            }

            public object Current
            {
                get
                {
                    return items[currentItem];
                }
            }
        }
    }
}
