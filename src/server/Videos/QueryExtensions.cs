using System.Data;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

using NMAC.Core;

namespace NMAC.Videos;

public enum YTVideoUpsertOutcome
{
    Unchanged = 0,
    Inserted = 1,
    Updated = 2
}

public static class EFCoreExtensions
{
    public static async Task<YTVideoUpsertOutcome> ExecuteUpsertAsync(this AppDbContext db, YTVideo entity, CancellationToken cancellationToken = default)
    {
        var topicUri = entity.TopicUri.ToString();
        var watchUrl = entity.WatchUrl?.ToString();
        var thumbnailUrl = entity.ThumbnailUrl?.ToString();

        // Keep only the newest known state for each YouTube video and detect insert/update/no-op.
        var strategy = db.Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync(async () =>
        {
            var connection = db.Database.GetDbConnection();
            var openedHere = false;

            if (connection.State != ConnectionState.Open)
            {
                await db.Database.OpenConnectionAsync(cancellationToken);
                openedHere = true;
            }

            try
            {
                await using var command = connection.CreateCommand();
                command.CommandType = CommandType.Text;
                command.CommandText = """
                    WITH upserted AS (
                        INSERT INTO yt_videos (
                            video_id,
                            channel_id,
                            topic_uri,
                            title,
                            published_at,
                            updated_at,
                            watch_url,
                            description,
                            thumbnail_url,
                            last_seen_at)
                        VALUES (
                            @video_id,
                            @channel_id,
                            @topic_uri,
                            @title,
                            @published_at,
                            @updated_at,
                            @watch_url,
                            @description,
                            @thumbnail_url,
                            @last_seen_at)
                        ON CONFLICT (video_id) DO UPDATE
                        SET channel_id = EXCLUDED.channel_id,
                            topic_uri = EXCLUDED.topic_uri,
                            title = EXCLUDED.title,
                            published_at = EXCLUDED.published_at,
                            updated_at = EXCLUDED.updated_at,
                            watch_url = EXCLUDED.watch_url,
                            description = EXCLUDED.description,
                            thumbnail_url = EXCLUDED.thumbnail_url,
                            last_seen_at = EXCLUDED.last_seen_at
                        WHERE COALESCE(EXCLUDED.updated_at, EXCLUDED.published_at, '-infinity'::timestamptz)
                            > COALESCE(yt_videos.updated_at, yt_videos.published_at, '-infinity'::timestamptz)
                        RETURNING (xmax = 0) AS is_insert
                    )
                    SELECT COALESCE(
                        (SELECT CASE WHEN is_insert THEN 1 ELSE 2 END FROM upserted),
                        0);
                    """;

                if (db.Database.CurrentTransaction is not null)
                {
                    command.Transaction = db.Database.CurrentTransaction.GetDbTransaction();
                }

                AddParameter(command, "video_id", entity.VideoId);
                AddParameter(command, "channel_id", entity.ChannelId);
                AddParameter(command, "topic_uri", topicUri);
                AddParameter(command, "title", entity.Title);
                AddParameter(command, "published_at", entity.PublishedAt);
                AddParameter(command, "updated_at", entity.UpdatedAt);
                AddParameter(command, "watch_url", watchUrl);
                AddParameter(command, "description", entity.Description);
                AddParameter(command, "thumbnail_url", thumbnailUrl);
                AddParameter(command, "last_seen_at", entity.LastSeenAt);

                var scalar = await command.ExecuteScalarAsync(cancellationToken);
                var code = scalar is null or DBNull ? 0 : Convert.ToInt32(scalar);

                return code switch
                {
                    1 => YTVideoUpsertOutcome.Inserted,
                    2 => YTVideoUpsertOutcome.Updated,
                    _ => YTVideoUpsertOutcome.Unchanged
                };
            }
            finally
            {
                if (openedHere)
                {
                    await db.Database.CloseConnectionAsync();
                }
            }
        });
    }

    private static void AddParameter(IDbCommand command, string name, object? value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value ?? DBNull.Value;
        command.Parameters.Add(parameter);
    }
}
