using System.Xml.Serialization;

using static NMAC.Subscriptions.WebSub.Namespaces;

namespace NMAC.Subscriptions.WebSub.Media;

[XmlRoot("starRating", Namespace = NS_MEDIA)]
public class StarRating
{
    [XmlAttribute("count")]
    public int Count { get; set; }

    [XmlAttribute("average")]
    public double Average { get; set; }

    [XmlAttribute("min")]
    public int Min { get; set; }

    [XmlAttribute("max")]
    public int Max { get; set; }
}
