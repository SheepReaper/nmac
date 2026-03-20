using System.Xml;
using System.Xml.Serialization;

namespace NMAC.Subscriptions.WebSub;

[XmlRoot("link")]
public class Link
{
    [XmlAttribute("rel")]
    public string? Rel { get; set; }

    [XmlAttribute("href")]
    public string? HrefString { get; set; }

    public Uri? Href => Uri.TryCreate(HrefString, UriKind.Absolute, out var uri) ? uri : null;
}
