using System.Xml.Serialization;

using static NMAC.Subscriptions.WebSub.Namespaces;

namespace NMAC.Subscriptions.WebSub.Media;

[XmlRoot("statistics", Namespace = NS_MEDIA)]
public class Statistics
{
    [XmlAttribute("views")]
    public int Views { get; set; }
}
