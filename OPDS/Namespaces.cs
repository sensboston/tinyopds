using System.Xml.Linq;

namespace TinyOPDS.OPDS
{
    internal class Namespaces
    {
        internal static XNamespace xmlns = XNamespace.Get("http://www.w3.org/2005/Atom");
        internal static XNamespace dc = XNamespace.Get("http://purl.org/dc/terms/");
        internal static XNamespace os = XNamespace.Get("http://a9.com/-/spec/opensearch/1.1/");
        internal static XNamespace opds = XNamespace.Get("http://opds-spec.org/2010/catalog");
    }
}
