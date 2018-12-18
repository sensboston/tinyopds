/***********************************************************
 * This file is a part of TinyOPDS server project
 * 
 * Copyright (c) 2013 SeNSSoFT
 *
 * This code is licensed under the Microsoft Public License, 
 * see http://tinyopds.codeplex.com/license for the details.
 *
 * Simple implementation of UPnP controller. Works fine with 
 * some D-Link and NetGear router models (need more tests)
 * 
 * Based on the Harold Aptroot article & code 
 * http://www.codeproject.com/Articles/27992/
 * 
 * TODO: check compatibility with other routers
 * 
 ************************************************************/

using System;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using System.Net.Sockets;
using System.Net;
using System.Xml;
using System.IO;
using System.Net.NetworkInformation;
using System.ComponentModel;

namespace UPnP
{
    public class UPnPController : IDisposable
    {
        private bool _disposed = false;
        private string _serviceUrl;
        private BackgroundWorker _worker;
        private WebClient _webClient;

        public bool Discovered { get; private set; }
        public event EventHandler DiscoverCompleted;

        public bool UPnPReady { get { return !string.IsNullOrEmpty(_serviceUrl); } }

        public UPnPController ()
        {
            Discovered = false;
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!this._disposed && disposing)
            {
                if (_webClient != null && _webClient.IsBusy)
                {
                    _webClient.CancelAsync();
                    _webClient.Dispose();
                }
                if (_worker != null)
                {
                    _worker.Dispose();
                }
                DiscoverCompleted = null;
                _disposed = true;
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        public void DiscoverAsync(bool useUPnP)
        {
            _worker = new BackgroundWorker();
            _worker.DoWork += new DoWorkEventHandler(_worker_DoWork);
            _worker.RunWorkerAsync(useUPnP);
        }

        void _worker_DoWork(object sender, DoWorkEventArgs e)
        {
            bool detectUPnP = (bool) e.Argument;
            if (detectUPnP)
            {
                NetworkInterface[] adapters = NetworkInterface.GetAllNetworkInterfaces();
                foreach (NetworkInterface adapter in adapters)
                {
                    IPInterfaceProperties properties = adapter.GetIPProperties();
                    if (properties.GatewayAddresses != null && properties.GatewayAddresses.Count > 0)
                    {
                        foreach (IPAddressInformation uniAddr in properties.UnicastAddresses)
                        {
                            if (uniAddr.Address.AddressFamily == AddressFamily.InterNetwork)
                            {
                                using (Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp))
                                {
                                    socket.Bind(new IPEndPoint(uniAddr.Address, 0));
                                    socket.ReceiveTimeout = 2000;
                                    socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.Broadcast, 1);
                                    string req = "M-SEARCH * HTTP/1.1\r\n" +
                                                 "HOST: 239.255.255.250:1900\r\n" +
                                                 "ST:upnp:rootdevice\r\n" +
                                                 "MAN:\"ssdp:discover\"\r\n" +
                                                 "MX:3\r\n\r\n";
                                    byte[] data = Encoding.ASCII.GetBytes(req);
                                    IPEndPoint ipe = new IPEndPoint(IPAddress.Broadcast, 1900);
                                    byte[] buffer = new byte[0x1000];

                                    for (int i = 0; i < 3; i++) socket.SendTo(data, ipe);

                                    int length = 0;
                                    do
                                    {
                                        try { length = socket.Receive(buffer); }
                                        catch { break; }
                                        string resp = Encoding.ASCII.GetString(buffer, 0, length).ToLower();
                                        if (resp.Contains("upnp:rootdevice"))
                                        {
                                            resp = resp.Substring(resp.ToLower().IndexOf("location:") + 9);
                                            resp = resp.Substring(0, resp.IndexOf("\r")).Trim();
                                            if (!string.IsNullOrEmpty(_serviceUrl = GetServiceUrl(resp)))
                                            {
                                                break;
                                            }
                                        }
                                    } while (length > 0);
                                }
                                if (UPnPReady)
                                {
                                    string ip = "127.0.0.0";
                                    XmlDocument xdoc = SOAPRequest(_serviceUrl,
                                        "<u:GetExternalIPAddress xmlns:u=\"urn:schemas-upnp-org:service:WANIPConnection:1\">" +
                                        "</u:GetExternalIPAddress>", "GetExternalIPAddress");
                                    if (xdoc.OuterXml.Contains("NewExternalIPAddress"))
                                    {
                                        XmlNamespaceManager nsMgr = new XmlNamespaceManager(xdoc.NameTable);
                                        nsMgr.AddNamespace("tns", "urn:schemas-upnp-org:device-1-0");
                                        ip = xdoc.SelectSingleNode("//NewExternalIPAddress/text()", nsMgr).Value;
                                    }
                                    ExternalIP = IPAddress.Parse(ip);
                                }
                                Discovered = true;
                                if (UPnPReady && DiscoverCompleted != null) DiscoverCompleted(this, new EventArgs());
                            }
                        }
                    }
                }
            }
            // Just detect external IP address
            else
            {
                _webClient = new WebClient();
                _webClient.DownloadStringCompleted += (object o, DownloadStringCompletedEventArgs ea) =>
                    {
                        if (!_disposed && ea.Error == null && ea.Result != null)
                        {
                            Regex ip = new Regex(@"\b\d{1,3}\.\d{1,3}\.\d{1,3}\.\d{1,3}\b");
                            MatchCollection result = ip.Matches(ea.Result);
                            try { ExternalIP = IPAddress.Parse(result[0].Value); }
                            catch { ExternalIP = IPAddress.Parse("0.0.0.0"); }
                            if (DiscoverCompleted != null) DiscoverCompleted(this, new EventArgs());
                        }
                    };
                _webClient.DownloadStringAsync(new Uri("http://myip.dnsdynamic.org")); //new Uri("http://checkip.dyndns.org"));
            }
        }

        private string GetServiceUrl(string resp)
        {
#if false
            // UPDATE: registry fix eliminate the IOException but completely ruins UPnP detection (after reboot)
            // Prevent IOException 
            // See https://connect.microsoft.com/VisualStudio/feedback/details/773666/webrequest-create-eats-an-ioexception-on-the-first-call#details
            RegistryKey registryKey = null;
            registryKey = Registry.LocalMachine.OpenSubKey("SOFTWARE\\Wow6432Node\\Microsoft\\.NETFramework", true);
            if (registryKey == null) registryKey = Registry.LocalMachine.OpenSubKey("SOFTWARE\\Microsoft\\.NETFramework", true);
            if (registryKey.GetValue("LegacyWPADSupport") == null) registryKey.SetValue("LegacyWPADSupport", 0);
#endif
            try
            {
                XmlDocument desc = new XmlDocument();
                desc.Load(WebRequest.Create(resp).GetResponse().GetResponseStream());
                XmlNamespaceManager nsMgr = new XmlNamespaceManager(desc.NameTable);
                nsMgr.AddNamespace("tns", "urn:schemas-upnp-org:device-1-0");
                XmlNode typen = desc.SelectSingleNode("//tns:device/tns:deviceType/text()", nsMgr);
                if (!typen.Value.Contains("InternetGatewayDevice")) return null;
                XmlNode node = desc.SelectSingleNode("//tns:service[tns:serviceType=\"urn:schemas-upnp-org:service:WANIPConnection:1\"]/tns:controlURL/text()", nsMgr);
                if (node == null) return null;
                return CombineUrls(resp, node.Value);
            }
            catch 
            { 
                return null; 
            }
        }

        private string CombineUrls(string resp, string p)
        {
            int n = resp.IndexOf("://");
            n = resp.IndexOf('/', n + 3);
            return resp.Substring(0, n) + p;
        }

        public void ForwardPort(int port, ProtocolType protocol, string description)
        {
            if (UPnPReady)
            {
                SOAPRequest(_serviceUrl, 
                    "<u:AddPortMapping xmlns:u=\"urn:schemas-upnp-org:service:WANIPConnection:1\">" +
                    "<NewRemoteHost></NewRemoteHost><NewExternalPort>" + port.ToString() + "</NewExternalPort><NewProtocol>" + protocol.ToString().ToUpper() + "</NewProtocol>" +
                    "<NewInternalPort>" + port.ToString() + "</NewInternalPort><NewInternalClient>" + LocalIP.ToString() +
                    "</NewInternalClient><NewEnabled>1</NewEnabled><NewPortMappingDescription>" + description +
                    "</NewPortMappingDescription><NewLeaseDuration>0</NewLeaseDuration></u:AddPortMapping>", "AddPortMapping");
            }
        }

        public void DeleteForwardingRule(int port, ProtocolType protocol)
        {
            if (UPnPReady)
            {
                SOAPRequest(_serviceUrl,
                    "<u:DeletePortMapping xmlns:u=\"urn:schemas-upnp-org:service:WANIPConnection:1\">" +
                    "<NewRemoteHost>" +
                    "</NewRemoteHost>" +
                    "<NewExternalPort>" + port + "</NewExternalPort>" +
                    "<NewProtocol>" + protocol.ToString().ToUpper() + "</NewProtocol>" +
                    "</u:DeletePortMapping>", "DeletePortMapping");
            }
        }

        /// <summary>
        /// Local network interface index
        /// </summary>
        private int _interfaceIndex = 0;
        public int InterfaceIndex
        {
            get { return _interfaceIndex; }
            set { if (value >= 0 && value < LocalInterfaces.Count) _interfaceIndex = value; }
        }

        /// <summary>
        /// Local IP address
        /// </summary>
        /// <returns></returns>
        public IPAddress LocalIP { get { return LocalInterfaces[InterfaceIndex]; } }

        /// <summary>
        /// List of all local network interfaces with gateways
        /// </summary>
        private static List<IPAddress> _localInterfaces = null;
        public static List<IPAddress> LocalInterfaces
        {
            get
            {
                if (_localInterfaces == null)
                {
                    _localInterfaces = new List<IPAddress>();
                    foreach (NetworkInterface netif in NetworkInterface.GetAllNetworkInterfaces())
                    {
                        IPInterfaceProperties properties = netif.GetIPProperties();
                        foreach (GatewayIPAddressInformation gw in properties.GatewayAddresses)
                        {
                            if (!gw.Address.ToString().Equals("0.0.0.0"))
                            {
                                foreach (IPAddressInformation unicast in properties.UnicastAddresses)
                                {
                                    // Lets skip "link local" addresses (RFC 3927), probably this address is disabled
                                    if (unicast.Address.ToString().StartsWith("169.254")) break;
                                    if (unicast.Address.AddressFamily == AddressFamily.InterNetwork)
                                    {
                                        _localInterfaces.Add(unicast.Address);
                                    }
                                }
                            }
                        }
                    }
                    // If no network interface detected, add at least a local loopback (127.0.0.1)
                    if (_localInterfaces.Count == 0) _localInterfaces.Add(IPAddress.Loopback);
                }
                return _localInterfaces;
            }
        }

        public IPAddress ExternalIP { get; private set;}

        private static XmlDocument SOAPRequest(string url, string soap, string function)
        {
            XmlDocument resp = new XmlDocument();
            try
            {
                string req = "<?xml version=\"1.0\"?>" +
                "<s:Envelope xmlns:s=\"http://schemas.xmlsoap.org/soap/envelope/\" s:encodingStyle=\"http://schemas.xmlsoap.org/soap/encoding/\">" +
                "<s:Body>" +
                soap +
                "</s:Body>" +
                "</s:Envelope>";
                WebRequest r = HttpWebRequest.Create(url);
                r.Method = "POST";
                byte[] b = Encoding.UTF8.GetBytes(req);
                r.Headers.Add("SOAPACTION", "\"urn:schemas-upnp-org:service:WANIPConnection:1#" + function + "\"");
                r.ContentType = "text/xml; charset=\"utf-8\"";
                r.ContentLength = b.Length;
                r.GetRequestStream().Write(b, 0, b.Length);
                WebResponse wres = r.GetResponse();
                Stream ress = wres.GetResponseStream();
                resp.Load(ress);
            }
            catch { }
            return resp;
        }
    }
}
