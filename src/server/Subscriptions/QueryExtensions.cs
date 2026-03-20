using Microsoft.EntityFrameworkCore;

using NMAC.Core;

namespace NMAC.Subscriptions;

public static class QueryExtensions
{
    public static Task ExecuteUpsertAsync(this AppDbContext db, Subscription entity, CancellationToken cancellationToken = default)
    {
        var topicUri = entity.TopicUri.ToString();
        var callbackUri = entity.CallbackUri?.ToString();

        // Atomic upsert at the database level to avoid check-then-act races.
        return db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO subscriptions (topic_uri, slug, secret, mode, expiration, callback_uri, enabled)
            VALUES ({topicUri}, {entity.Slug}, {entity.Secret}, {entity.Mode}, {entity.Expiration}, {callbackUri}, {entity.Enabled})
            ON CONFLICT (topic_uri) DO UPDATE
            SET slug = EXCLUDED.slug,
                secret = EXCLUDED.secret,
                mode = EXCLUDED.mode,
                expiration = EXCLUDED.expiration,
                callback_uri = EXCLUDED.callback_uri,
                enabled = EXCLUDED.enabled
            WHERE subscriptions.slug IS DISTINCT FROM EXCLUDED.slug
               OR subscriptions.secret IS DISTINCT FROM EXCLUDED.secret
               OR subscriptions.mode IS DISTINCT FROM EXCLUDED.mode
               OR subscriptions.expiration IS DISTINCT FROM EXCLUDED.expiration
               OR subscriptions.callback_uri IS DISTINCT FROM EXCLUDED.callback_uri
               OR subscriptions.enabled IS DISTINCT FROM EXCLUDED.enabled
            """, cancellationToken);
    }

    public static Task ExecuteUpsertAsync(this AppDbContext db, ContentDistribution entity, CancellationToken cancellationToken = default)
    {
        var topicUri = entity.TopicUri.ToString();

        // Atomic upsert at the database level to avoid check-then-act races.
        return db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO content_distributions (topic_uri, content, headers, metadata, last_received_at)
            VALUES ({topicUri}, {entity.Content}, {entity.Headers}, {entity.Metadata}, {entity.LastReceivedAt})
            ON CONFLICT (topic_uri) DO UPDATE
            SET content = EXCLUDED.content,
                headers = EXCLUDED.headers,
                metadata = EXCLUDED.metadata,
                last_received_at = EXCLUDED.last_received_at
            WHERE content_distributions.content IS DISTINCT FROM EXCLUDED.content
               OR content_distributions.headers IS DISTINCT FROM EXCLUDED.headers
               OR content_distributions.metadata IS DISTINCT FROM EXCLUDED.metadata
               OR content_distributions.last_received_at IS DISTINCT FROM EXCLUDED.last_received_at
            """, cancellationToken);
    }
}
