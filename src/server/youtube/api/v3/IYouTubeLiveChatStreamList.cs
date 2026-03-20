using ProtoBuf.Grpc;
using ProtoBuf.Grpc.Configuration;

namespace youtube.api.v3;

[Service("youtube.api.v3.V3DataLiveChatMessageService")]
public interface IYouTubeLiveChatStreamList
{
    IAsyncEnumerable<LiveChatMessageListResponse> StreamList(
        LiveChatMessageListRequest request,
        CallContext context = default
    );
}
