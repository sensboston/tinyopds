using System;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using System.Net.Sockets;
using System.Net;
using System.Xml;
using System.IO;
using System.Net.NetworkInformation;

namespace UPnP
{
    public class NAT
    {
        public static bool IsReady { get { return !string.IsNullOrEmpty(_serviceUrl); } }

        private static string _descUrl, _serviceUrl, _eventUrl;

        public static bool Discover()
        {
            using (Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp))
            {
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
                    catch { return false; }
                    string resp = Encoding.ASCII.GetString(buffer, 0, length).ToLower();
                    if (resp.Contains("upnp:rootdevice"))
                    {
                        resp = resp.Substring(resp.ToLower().IndexOf("location:") + 9);
                        resp = resp.Substring(0, resp.IndexOf("\r")).Trim();
                        if (!string.IsNullOrEmpty(_serviceUrl = GetServiceUrl(resp)))
                        {
                            _descUrl = resp;
                            return true;
                        }
                    }
                } while (length > 0);
            }
            return false;
        }

        private static string GetServiceUrl(string resp)
        {
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
                XmlNode eventnode = desc.SelectSingleNode("//tns:service[tns:serviceType=\"urn:schemas-upnp-org:service:WANIPConnection:1\"]/tns:eventSubURL/text()", nsMgr);
                _eventUrl = CombineUrls(resp, eventnode.Value);
                return CombineUrls(resp, node.Value);
            }
            catch 
            { 
                return null; 
            }
        }

        private static string CombineUrls(string resp, string p)
        {
            int n = resp.IndexOf("://");
            n = resp.IndexOf('/', n + 3);
            return resp.Substring(0, n) + p;
        }

        public static void ForwardPort(int port, ProtocolType protocol, string description)
        {
            if (IsReady)
            {
                XmlDocument xdoc = SOAPRequest(_serviceUrl, 
                    "<u:AddPortMapping xmlns:u=\"urn:schemas-upnp-org:service:WANIPConnection:1\">" +
                    "<NewRemoteHost></NewRemoteHost><NewExternalPort>" + port.ToString() + "</NewExternalPort><NewProtocol>" + protocol.ToString().ToUpper() + "</NewProtocol>" +
                    "<NewInternalPort>" + port.ToString() + "</NewInternalPort><NewInternalClient>" + LocalIP.ToString() +
                    "</NewInternalClient><NewEnabled>1</NewEnabled><NewPortMappingDescription>" + description +
                    "</NewPortMappingDescription><NewLeaseDuration>0</NewLeaseDuration></u:AddPortMapping>", "AddPortMapping");
            }
        }

        public static void DeleteForwardingRule(int port, ProtocolType protocol)
        {
            if (IsReady)
            {
                XmlDocument xdoc = SOAPRequest(_serviceUrl,
                    "<u:DeletePortMapping xmlns:u=\"urn:schemas-upnp-org:service:WANIPConnection:1\">" +
                    "<NewRemoteHost>" +
                    "</NewRemoteHost>" +
                    "<NewExternalPort>" + port + "</NewExternalPort>" +
                    "<NewProtocol>" + protocol.ToString().ToUpper() + "</NewProtocol>" +
                    "</u:DeletePortMapping>", "DeletePortMapping");
            }
        }

        /// <summary>
        /// Detect local IP address
        /// </summary>
        /// <returns></returns>
        public static IPAddress LocalIP
        {
            get
            {
                IPAddress address = IPAddress.Any;
                try
                {
                    string ip = "127.0.0.0";
                    foreach (NetworkInterface networkCard in NetworkInterface.GetAllNetworkInterfaces())
                    {
                        foreach (GatewayIPAddressInformation gatewayAddr in networkCard.GetIPProperties().GatewayAddresses)
                        {
                            if (gatewayAddr.Address.ToString() != "0.0.0.0")
                            {
                                ip = gatewayAddr.Address.ToString();
                                ip = ip.Substring(0, ip.LastIndexOf('.'));
                                break;
                            }
                        }
                    }

                    IPAddress[] addresses = Dns.GetHostAddresses(Dns.GetHostName());
                    foreach (IPAddress addr in addresses)
                    {
                        if (addr.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                        {
                            if (addr.ToString().Contains(ip))
                            {
                                address = addr;
                                break;
                            }
                        }
                    }
                }
                catch { }
                return address;
            }
        }

        public static IPAddress ExternalIP
        {
            get
            {
                string ip = "127.0.0.0";
                if (IsReady)
                {
                    XmlDocument xdoc = SOAPRequest(_serviceUrl,
                        "<u:GetExternalIPAddress xmlns:u=\"urn:schemas-upnp-org:service:WANIPConnection:1\">" +
                        "</u:GetExternalIPAddress>", "GetExternalIPAddress");
                    XmlNamespaceManager nsMgr = new XmlNamespaceManager(xdoc.NameTable);
                    nsMgr.AddNamespace("tns", "urn:schemas-upnp-org:device-1-0");
                    ip = xdoc.SelectSingleNode("//NewExternalIPAddress/text()", nsMgr).Value;
                }
                // Let's use non-UPnP method
                else
                {
                    using (WebClient client = new WebClient())
                    {
                        try { ip = client.DownloadString("http://myip.dnsdynamic.org"); } catch { }
                    }
                }
                return IPAddress.Parse(ip);
            }
        }

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
