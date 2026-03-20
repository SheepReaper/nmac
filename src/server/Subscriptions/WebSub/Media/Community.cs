using System.Xml.Serialization;

using static NMAC.Subscriptions.WebSub.Namespaces;

namespace NMAC.Subscriptions.WebSub.Media;

[XmlRoot("community", Namespace = NS_MEDIA)]
public class Community
{

    [XmlElement("starrating", Namespace = NS_MEDIA)]
    public StarRating? StarRating { get; set; }

    [XmlElement("statistics", Namespace = NS_MEDIA)]
    public Statistics? Statistics { get; set; }
}
