/*
 * This file is part of TinyOPDS server project
 * https://github.com/sensboston/tinyopds
 *
 * Copyright (c) 2013-2025 SeNSSoFT
 * SPDX-License-Identifier: MIT
 *
 * Simple implementation of UPnP controller. Works fine with 
 * some D-Link and NetGear router models (need more tests)
 * 
 * Based on the Harold Aptroot article & code 
 * http://www.codeproject.com/Articles/27992/
 *
 */

using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using System.Net.Sockets;
using System.Net;
using System.Xml;
using System.IO;
using System.Net.NetworkInformation;
using System.ComponentModel;
using System.Threading;

namespace UPnP
{
    public class UPnPController : IDisposable
    {
        private bool _disposed = false;
        private string _serviceUrl;
        private BackgroundWorker _worker;
        private WebClient _webClient;
        private readonly object _lockObject = new object();

        private const int DISCOVERY_TIMEOUT_MS = 5000;
        private const int SOCKET_TIMEOUT_MS = 2000;
        private const int MAX_RECEIVE_ATTEMPTS = 5;
        private const int RECEIVE_SLEEP_MS = 100;
        private const string MULTICAST_ADDRESS = "239.255.255.250";
        private const int MULTICAST_PORT = 1900;

        public bool Discovered { get; private set; }
        public event EventHandler DiscoverCompleted;
        public bool UPnPReady { get { return !string.IsNullOrEmpty(_serviceUrl); } }
        public IPAddress ExternalIP { get; private set; }

        private int _interfaceIndex = 0;
        public int InterfaceIndex
        {
            get { return _interfaceIndex; }
            set
            {
                if (value >= 0 && value < LocalInterfaces.Count)
                    _interfaceIndex = value;
            }
        }

        public IPAddress LocalIP { get { return LocalInterfaces[InterfaceIndex]; } }

        private static List<IPAddress> _localInterfaces = null;
        public static List<IPAddress> LocalInterfaces
        {
            get
            {
                if (_localInterfaces == null)
                {
                    _localInterfaces = BuildLocalInterfacesList();
                }
                return _localInterfaces;
            }
        }

        public UPnPController()
        {
            Discovered = false;
            ExternalIP = IPAddress.Loopback;
        }

        public void DiscoverAsync(bool useUPnP)
        {
            if (_disposed) return;

            lock (_lockObject)
            {
                if (_worker != null && _worker.IsBusy)
                {
                    return; // Already discovering
                }

                _worker = new BackgroundWorker();
                _worker.DoWork += Worker_DoWork;
                _worker.RunWorkerCompleted += Worker_RunWorkerCompleted;
                _worker.RunWorkerAsync(useUPnP);
            }
        }

        private void Worker_DoWork(object sender, DoWorkEventArgs e)
        {
            if (_disposed) return;

            bool detectUPnP = (bool)e.Argument;

            if (detectUPnP)
            {
                DiscoverUPnPDevices();
            }
            else
            {
                DetectExternalIPAddress();
            }
        }

        private void Worker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            if (_disposed) return;

            Discovered = true;
            DiscoverCompleted?.Invoke(this, EventArgs.Empty);
        }

        private void DiscoverUPnPDevices()
        {
            NetworkInterface[] adapters = NetworkInterface.GetAllNetworkInterfaces();

            foreach (NetworkInterface adapter in adapters)
            {
                if (_disposed) return;

                if (!IsValidNetworkInterface(adapter)) continue;

                IPInterfaceProperties properties = adapter.GetIPProperties();

                foreach (IPAddressInformation uniAddr in properties.UnicastAddresses)
                {
                    if (_disposed) return;

                    if (uniAddr.Address.AddressFamily == AddressFamily.InterNetwork &&
                        !IsLinkLocalAddress(uniAddr.Address))
                    {
                        if (TryDiscoverOnInterface(uniAddr.Address))
                        {
                            return; // Successfully discovered, exit
                        }
                    }
                }
            }
        }

        private bool IsValidNetworkInterface(NetworkInterface adapter)
        {
            if (adapter.OperationalStatus != OperationalStatus.Up) return false;

            IPInterfaceProperties properties = adapter.GetIPProperties();
            return properties.GatewayAddresses != null && properties.GatewayAddresses.Count > 0;
        }

        private bool IsLinkLocalAddress(IPAddress address)
        {
            return address.ToString().StartsWith("169.254");
        }

        private bool TryDiscoverOnInterface(IPAddress localAddress)
        {
            Socket socket = null;
            try
            {
                socket = CreateAndConfigureSocket(localAddress);
                SendDiscoveryRequests(socket);
                return ProcessDiscoveryResponses(socket);
            }
            catch
            {
                return false;
            }
            finally
            {
                socket?.Close();
                socket?.Dispose();
            }
        }

        private Socket CreateAndConfigureSocket(IPAddress localAddress)
        {
            var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            socket.Bind(new IPEndPoint(localAddress, 0));
            socket.ReceiveTimeout = SOCKET_TIMEOUT_MS;
            socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.Broadcast, 1);
            return socket;
        }

        private void SendDiscoveryRequests(Socket socket)
        {
            string request = BuildDiscoveryRequest();
            byte[] data = Encoding.ASCII.GetBytes(request);
            IPEndPoint endpoint = new IPEndPoint(IPAddress.Parse(MULTICAST_ADDRESS), MULTICAST_PORT);

            // Send multiple discovery requests for better reliability
            for (int i = 0; i < 3; i++)
            {
                if (_disposed) break;
                socket.SendTo(data, endpoint);
                Thread.Sleep(50); // Small delay between requests
            }
        }

        private string BuildDiscoveryRequest()
        {
            return "M-SEARCH * HTTP/1.1\r\n" +
                   "HOST: 239.255.255.250:1900\r\n" +
                   "ST:upnp:rootdevice\r\n" +
                   "MAN:\"ssdp:discover\"\r\n" +
                   "MX:3\r\n\r\n";
        }

        private bool ProcessDiscoveryResponses(Socket socket)
        {
            byte[] buffer = new byte[4096];
            DateTime startTime = DateTime.Now;
            TimeSpan maxWaitTime = TimeSpan.FromMilliseconds(DISCOVERY_TIMEOUT_MS);
            int receiveAttempts = 0;

            while (DateTime.Now - startTime < maxWaitTime && !_disposed)
            {
                try
                {
                    if (socket.Available > 0)
                    {
                        int length = socket.Receive(buffer, SocketFlags.None);
                        receiveAttempts = 0;

                        if (length > 0)
                        {
                            string response = Encoding.ASCII.GetString(buffer, 0, length);
                            if (ProcessSingleResponse(response))
                            {
                                return TryGetExternalIP();
                            }
                        }
                    }
                    else
                    {
                        Thread.Sleep(RECEIVE_SLEEP_MS);
                        receiveAttempts++;

                        if (receiveAttempts >= MAX_RECEIVE_ATTEMPTS)
                        {
                            break;
                        }
                    }
                }
                catch (SocketException ex) when (IsExpectedSocketError(ex))
                {
                    break; // Expected timeout or connection issues
                }
                catch
                {
                    break; // Unexpected error
                }
            }

            return false;
        }

        private bool IsExpectedSocketError(SocketException ex)
        {
            return ex.SocketErrorCode == SocketError.TimedOut ||
                   ex.SocketErrorCode == SocketError.ConnectionReset ||
                   ex.SocketErrorCode == SocketError.ConnectionAborted;
        }

        private bool ProcessSingleResponse(string response)
        {
            if (!response.ToLower().Contains("upnp:rootdevice")) return false;

            try
            {
                string locationHeader = ExtractLocationFromResponse(response);
                if (string.IsNullOrEmpty(locationHeader)) return false;

                _serviceUrl = GetServiceUrl(locationHeader);
                return !string.IsNullOrEmpty(_serviceUrl);
            }
            catch
            {
                return false;
            }
        }

        private string ExtractLocationFromResponse(string response)
        {
            string lowerResponse = response.ToLower();
            int locationIndex = lowerResponse.IndexOf("location:");

            if (locationIndex == -1) return null;

            string locationPart = response.Substring(locationIndex + 9);
            int endIndex = locationPart.IndexOf("\r");

            if (endIndex == -1) endIndex = locationPart.IndexOf("\n");
            if (endIndex == -1) return locationPart.Trim();

            return locationPart.Substring(0, endIndex).Trim();
        }

        private bool TryGetExternalIP()
        {
            try
            {
                XmlDocument response = SOAPRequest(_serviceUrl,
                    "<u:GetExternalIPAddress xmlns:u=\"urn:schemas-upnp-org:service:WANIPConnection:1\">" +
                    "</u:GetExternalIPAddress>", "GetExternalIPAddress");

                if (response?.OuterXml.Contains("NewExternalIPAddress") == true)
                {
                    XmlNamespaceManager nsMgr = new XmlNamespaceManager(response.NameTable);
                    nsMgr.AddNamespace("tns", "urn:schemas-upnp-org:device-1-0");
                    XmlNode ipNode = response.SelectSingleNode("//NewExternalIPAddress/text()", nsMgr);

                    if (ipNode?.Value != null && IPAddress.TryParse(ipNode.Value, out IPAddress externalIP))
                    {
                        ExternalIP = externalIP;
                        return true;
                    }
                }
            }
            catch
            {
                // Fallback to localhost if external IP detection fails
            }

            ExternalIP = IPAddress.Loopback;
            return true;
        }

        private void DetectExternalIPAddress()
        {
            if (_disposed) return;

            try
            {
                _webClient = new WebClient();
                _webClient.DownloadStringCompleted += WebClient_DownloadStringCompleted;
                _webClient.DownloadStringAsync(new Uri("http://myip.dnsdynamic.org"));
            }
            catch
            {
                ExternalIP = IPAddress.Loopback;
            }
        }

        private void WebClient_DownloadStringCompleted(object sender, DownloadStringCompletedEventArgs e)
        {
            if (_disposed) return;

            try
            {
                if (e.Error == null && !string.IsNullOrEmpty(e.Result))
                {
                    Regex ipRegex = new Regex(@"\b\d{1,3}\.\d{1,3}\.\d{1,3}\.\d{1,3}\b");
                    MatchCollection matches = ipRegex.Matches(e.Result);

                    if (matches.Count > 0 && IPAddress.TryParse(matches[0].Value, out IPAddress externalIP))
                    {
                        ExternalIP = externalIP;
                        return;
                    }
                }
            }
            catch
            {
                // Fallback on any error
            }

            ExternalIP = IPAddress.Loopback;
        }

        private string GetServiceUrl(string deviceUrl)
        {
            try
            {
                XmlDocument deviceDescription = new XmlDocument();
                using (WebResponse response = WebRequest.Create(deviceUrl).GetResponse())
                using (Stream stream = response.GetResponseStream())
                {
                    deviceDescription.Load(stream);
                }

                XmlNamespaceManager nsMgr = new XmlNamespaceManager(deviceDescription.NameTable);
                nsMgr.AddNamespace("tns", "urn:schemas-upnp-org:device-1-0");

                // Check if this is an Internet Gateway Device
                XmlNode deviceTypeNode = deviceDescription.SelectSingleNode("//tns:device/tns:deviceType/text()", nsMgr);
                if (deviceTypeNode?.Value?.Contains("InternetGatewayDevice") != true)
                    return null;

                // Find WANIPConnection service
                XmlNode controlUrlNode = deviceDescription.SelectSingleNode(
                    "//tns:service[tns:serviceType=\"urn:schemas-upnp-org:service:WANIPConnection:1\"]/tns:controlURL/text()", nsMgr);

                if (controlUrlNode?.Value == null) return null;

                return CombineUrls(deviceUrl, controlUrlNode.Value);
            }
            catch
            {
                return null;
            }
        }

        private string CombineUrls(string baseUrl, string relativePath)
        {
            Uri baseUri = new Uri(baseUrl);
            Uri combinedUri = new Uri(baseUri, relativePath);
            return combinedUri.ToString();
        }

        private static List<IPAddress> BuildLocalInterfacesList()
        {
            var interfaces = new List<IPAddress>();

            try
            {
                foreach (NetworkInterface netif in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (netif.OperationalStatus != OperationalStatus.Up) continue;

                    IPInterfaceProperties properties = netif.GetIPProperties();
                    bool hasGateway = false;

                    // Check if interface has a valid gateway
                    foreach (GatewayIPAddressInformation gw in properties.GatewayAddresses)
                    {
                        if (!gw.Address.ToString().Equals("0.0.0.0"))
                        {
                            hasGateway = true;
                            break;
                        }
                    }

                    if (hasGateway)
                    {
                        foreach (IPAddressInformation unicast in properties.UnicastAddresses)
                        {
                            if (unicast.Address.AddressFamily == AddressFamily.InterNetwork &&
                                !unicast.Address.ToString().StartsWith("169.254")) // Skip link-local
                            {
                                interfaces.Add(unicast.Address);
                            }
                        }
                    }
                }
            }
            catch
            {
                // If enumeration fails, add at least loopback
            }

            // Ensure we have at least one interface
            if (interfaces.Count == 0)
            {
                interfaces.Add(IPAddress.Loopback);
            }

            return interfaces;
        }

        public void ForwardPort(int port, ProtocolType protocol, string description)
        {
            if (!UPnPReady || _disposed) return;

            try
            {
                string soapAction = "<u:AddPortMapping xmlns:u=\"urn:schemas-upnp-org:service:WANIPConnection:1\">" +
                    "<NewRemoteHost></NewRemoteHost>" +
                    "<NewExternalPort>" + port + "</NewExternalPort>" +
                    "<NewProtocol>" + protocol.ToString().ToUpper() + "</NewProtocol>" +
                    "<NewInternalPort>" + port + "</NewInternalPort>" +
                    "<NewInternalClient>" + LocalIP + "</NewInternalClient>" +
                    "<NewEnabled>1</NewEnabled>" +
                    "<NewPortMappingDescription>" + description + "</NewPortMappingDescription>" +
                    "<NewLeaseDuration>0</NewLeaseDuration>" +
                    "</u:AddPortMapping>";

                SOAPRequest(_serviceUrl, soapAction, "AddPortMapping");
            }
            catch
            {
                // Port forwarding failed - silently ignore
            }
        }

        public void DeleteForwardingRule(int port, ProtocolType protocol)
        {
            if (!UPnPReady || _disposed) return;

            try
            {
                string soapAction = "<u:DeletePortMapping xmlns:u=\"urn:schemas-upnp-org:service:WANIPConnection:1\">" +
                    "<NewRemoteHost></NewRemoteHost>" +
                    "<NewExternalPort>" + port + "</NewExternalPort>" +
                    "<NewProtocol>" + protocol.ToString().ToUpper() + "</NewProtocol>" +
                    "</u:DeletePortMapping>";

                SOAPRequest(_serviceUrl, soapAction, "DeletePortMapping");
            }
            catch
            {
                // Port deletion failed - silently ignore
            }
        }

        private static XmlDocument SOAPRequest(string url, string soapBody, string soapAction)
        {
            var response = new XmlDocument();

            try
            {
                string envelope = "<?xml version=\"1.0\"?>" +
                    "<s:Envelope xmlns:s=\"http://schemas.xmlsoap.org/soap/envelope/\" " +
                    "s:encodingStyle=\"http://schemas.xmlsoap.org/soap/encoding/\">" +
                    "<s:Body>" + soapBody + "</s:Body></s:Envelope>";

                WebRequest request = WebRequest.Create(url);
                request.Method = "POST";
                request.ContentType = "text/xml; charset=\"utf-8\"";
                request.Headers.Add("SOAPACTION", "\"urn:schemas-upnp-org:service:WANIPConnection:1#" + soapAction + "\"");

                byte[] data = Encoding.UTF8.GetBytes(envelope);
                request.ContentLength = data.Length;

                using (Stream requestStream = request.GetRequestStream())
                {
                    requestStream.Write(data, 0, data.Length);
                }

                using (WebResponse webResponse = request.GetResponse())
                using (Stream responseStream = webResponse.GetResponseStream())
                {
                    response.Load(responseStream);
                }
            }
            catch
            {
                // Return empty document on error
            }

            return response;
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed && disposing)
            {
                lock (_lockObject)
                {
                    _disposed = true;

                    if (_webClient != null)
                    {
                        if (_webClient.IsBusy)
                        {
                            _webClient.CancelAsync();
                        }
                        _webClient.Dispose();
                        _webClient = null;
                    }

                    if (_worker != null)
                    {
                        _worker.Dispose();
                        _worker = null;
                    }

                    DiscoverCompleted = null;
                }
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}