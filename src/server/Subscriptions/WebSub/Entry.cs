using System.Xml;
using System.Xml.Serialization;

using NMAC.Subscriptions.WebSub.Media;

using static NMAC.Subscriptions.WebSub.Namespaces;

namespace NMAC.Subscriptions.WebSub;

[XmlRoot("entry")]
public class Entry
{
    [XmlElement("id")] // format: yt:video:VIDEO_ID
    public string? Id { get; set; }


    [XmlElement("videoId", Namespace = NS_YOUTUBE)]
    public string? VideoId { get; set; }

    [XmlElement("channelId", Namespace = NS_YOUTUBE)]
    public string? ChannelId { get; set; }

    [XmlElement("title")]
    public string? Title { get; set; }

    [XmlElement("link")]
    public List<Link> Links { get; set; } = [];

    public Link? WatchLink => Links.FirstOrDefault(l => l.Rel == "alternate");

    public Uri? WatchUrl => WatchLink?.Href;

    [XmlElement("author")]
    public Author? Author { get; set; }

    [XmlElement("published")]
    public DateTimeOffset? Published { get; set; }

    [XmlElement("updated")]
    public DateTimeOffset? Updated { get; set; }

    [XmlElement("group", Namespace = NS_MEDIA)]
    public Group? MediaGroup { get; set; }
}
