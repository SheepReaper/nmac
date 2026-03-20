using System.Xml.Serialization;

using static NMAC.Subscriptions.WebSub.Namespaces;

namespace NMAC.Subscriptions.WebSub.Media;

[XmlRoot("group", Namespace = NS_MEDIA)]
public class Group
{
    [XmlElement("title", Namespace = NS_MEDIA)]
    public string? Title { get; set; }

    [XmlElement("content", Namespace = NS_MEDIA)]
    public Content? Content { get; set; }

    [XmlElement("thumbnail", Namespace = NS_MEDIA)]
    public Thumbnail? Thumbnail { get; set; }

    [XmlElement("description", Namespace = NS_MEDIA)]
    public string? Description { get; set; }

    [XmlElement("community", Namespace = NS_MEDIA)]
    public Community? Community { get; set; }
}
