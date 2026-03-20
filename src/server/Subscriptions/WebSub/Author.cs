using System.Xml;
using System.Xml.Serialization;

namespace NMAC.Subscriptions.WebSub;

[XmlRoot("author")]
public class Author
{
    [XmlElement("name")]
    public string? Name { get; set; }

    [XmlElement("uri")]
    public string? UriString { get; set; }

    public Uri? Uri => Uri.TryCreate(UriString, UriKind.Absolute, out var uri) ? uri : null;
}
