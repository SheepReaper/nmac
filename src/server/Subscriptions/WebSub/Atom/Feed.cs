using System.Xml;
using System.Xml.Serialization;
using static NMAC.Subscriptions.WebSub.Namespaces;

namespace NMAC.Subscriptions.WebSub.Atom;

[XmlRoot("feed", Namespace = NS_ATOM)]
public class Feed
{
    [XmlElement("link")]
    public List<Link> Links { get; set; } = [];

    [XmlElement("id")] // format: yt:channel:CHANNEL_ID
    public string? Id { get; set; }

    [XmlElement("channelId", Namespace = NS_YOUTUBE)]
    public string? ChannelId { get; set; }

    [XmlElement("title")]
    public string? Title { get; set; }

    [XmlElement("author")]
    public Author? Author { get; set; }

    [XmlElement("published")]
    public DateTimeOffset? Published { get; set; }

    [XmlElement("entry")]
    public List<Entry> Entries { get; set; } = [];

    public Link? SelfLink => Links.FirstOrDefault(l => l.Rel == "self");
    public Link? HubLink => Links.FirstOrDefault(l => l.Rel == "hub");
    public IEnumerable<Link> AlternateLinks => Links.Where(l => l.Rel == "alternate");
    public Uri? HubUri => HubLink?.Href;
    public Uri? SelfUri => SelfLink?.Href;
}
