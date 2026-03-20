using System.Xml.Serialization;

using static NMAC.Subscriptions.WebSub.Namespaces;

namespace NMAC.Subscriptions.WebSub.Media;

[XmlRoot("thumbnail", Namespace = NS_MEDIA)]
public class Thumbnail
{
    [XmlAttribute("url")]
    public string? UrlString { get; set; }

    public Uri? Url => Uri.TryCreate(UrlString, UriKind.Absolute, out var uri) ? uri : null;

    [XmlAttribute("width")]
    public string? WidthString { get; set; }

    public int? Width => int.TryParse(WidthString, out var width) ? width : null;

    [XmlAttribute("height")]
    public string? HeightString { get; set; }

    public int? Height => int.TryParse(HeightString, out var height) ? height : null;
}