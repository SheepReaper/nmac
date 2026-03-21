using System.Data;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

using NMAC.Core;

using Npgsql;

using NpgsqlTypes;

namespace NMAC.LiveStreams;

internal static class QueryExtensions
{
    internal static async Task<int> ExecuteBatchInsertAsync(
        this AppDbContext db,
        IReadOnlyList<LiveSuperChat> entities,
        CancellationToken ct = default)
    {
        if (entities.Count == 0) return 0;

        // UNNEST arrays provide high-throughput bulk inserts with one command while
        // preserving ON CONFLICT deduplication semantics.

        var connection = (NpgsqlConnection)db.Database.GetDbConnection();
        var openedHere = connection.State != ConnectionState.Open;
        if (openedHere)
            await db.Database.OpenConnectionAsync(ct);

        try
        {
            await using var cmd = (NpgsqlCommand)connection.CreateCommand();

            cmd.CommandText = """
                INSERT INTO live_super_chats (
                    message_id, video_id, live_chat_id, author_channel_id,
                    author_display_name, author_profile_image_url, amount_micros,
                    currency, amount_display_string, is_super_sticker, sticker_id,
                    sticker_alt_text, sticker_alt_text_language, message_content, published_at)
                SELECT * FROM UNNEST(
                    @message_ids, @video_ids, @live_chat_ids, @author_channel_ids,
                    @author_display_names, @author_profile_image_urls, @amount_micros_arr,
                    @currencies, @amount_display_strings, @is_super_stickers, @sticker_ids,
                    @sticker_alt_texts, @sticker_alt_text_languages, @message_contents, @published_ats)
                ON CONFLICT (message_id) DO NOTHING
                """;

            cmd.Parameters.Add(TextArray("message_ids", entities.Select(e => e.MessageId)));
            cmd.Parameters.Add(TextArray("video_ids", entities.Select(e => e.VideoId)));
            cmd.Parameters.Add(TextArray("live_chat_ids", entities.Select(e => e.LiveChatId)));
            cmd.Parameters.Add(TextArray("author_channel_ids", entities.Select(e => e.AuthorChannelId)));
            cmd.Parameters.Add(TextArray("author_display_names", entities.Select(e => e.AuthorDisplayName)));
            cmd.Parameters.Add(TextArray("author_profile_image_urls", entities.Select(e => e.AuthorProfileImageUrl)));
            cmd.Parameters.Add(BigintArray("amount_micros_arr", entities.Select(e => e.AmountMicros)));
            cmd.Parameters.Add(TextArray("currencies", entities.Select(e => e.Currency)));
            cmd.Parameters.Add(TextArray("amount_display_strings", entities.Select(e => e.AmountDisplayString)));
            cmd.Parameters.Add(BoolArray("is_super_stickers", entities.Select(e => e.IsSuperSticker)));
            cmd.Parameters.Add(TextArray("sticker_ids", entities.Select(e => e.StickerId)));
            cmd.Parameters.Add(TextArray("sticker_alt_texts", entities.Select(e => e.StickerAltText)));
            cmd.Parameters.Add(TextArray("sticker_alt_text_languages", entities.Select(e => e.StickerAltTextLanguage)));
            cmd.Parameters.Add(TextArray("message_contents", entities.Select(e => e.MessageContent)));
            cmd.Parameters.Add(TimestampTzArray("published_ats", entities.Select(e => e.PublishedAt)));

            if (db.Database.CurrentTransaction is not null)
                cmd.Transaction = (NpgsqlTransaction)db.Database.CurrentTransaction.GetDbTransaction();

            return await cmd.ExecuteNonQueryAsync(ct);
        }
        finally
        {
            if (openedHere)
                await db.Database.CloseConnectionAsync();
        }
    }

    internal static async Task<int> ExecuteBatchInsertAsync(
        this AppDbContext db,
        IReadOnlyList<LiveFundingDonation> entities,
        CancellationToken ct = default)
    {
        if (entities.Count == 0) return 0;

        var connection = (NpgsqlConnection)db.Database.GetDbConnection();
        var openedHere = connection.State != ConnectionState.Open;
        if (openedHere)
            await db.Database.OpenConnectionAsync(ct);

        try
        {
            await using var cmd = (NpgsqlCommand)connection.CreateCommand();

            cmd.CommandText = """
                INSERT INTO live_funding_donations (
                    message_id, video_id, live_chat_id, author_channel_id,
                    author_display_name, amount_micros, currency,
                    amount_display_string, user_comment, published_at)
                SELECT * FROM UNNEST(
                    @message_ids, @video_ids, @live_chat_ids, @author_channel_ids,
                    @author_display_names, @amount_micros_arr, @currencies,
                    @amount_display_strings, @user_comments, @published_ats)
                ON CONFLICT (message_id) DO NOTHING
                """;

            cmd.Parameters.Add(TextArray("message_ids", entities.Select(e => e.MessageId)));
            cmd.Parameters.Add(TextArray("video_ids", entities.Select(e => e.VideoId)));
            cmd.Parameters.Add(TextArray("live_chat_ids", entities.Select(e => e.LiveChatId)));
            cmd.Parameters.Add(TextArray("author_channel_ids", entities.Select(e => e.AuthorChannelId)));
            cmd.Parameters.Add(TextArray("author_display_names", entities.Select(e => e.AuthorDisplayName)));
            cmd.Parameters.Add(BigintArray("amount_micros_arr", entities.Select(e => e.AmountMicros)));
            cmd.Parameters.Add(TextArray("currencies", entities.Select(e => e.Currency)));
            cmd.Parameters.Add(TextArray("amount_display_strings", entities.Select(e => e.AmountDisplayString)));
            cmd.Parameters.Add(TextArray("user_comments", entities.Select(e => e.UserComment)));
            cmd.Parameters.Add(TimestampTzArray("published_ats", entities.Select(e => e.PublishedAt)));

            if (db.Database.CurrentTransaction is not null)
                cmd.Transaction = (NpgsqlTransaction)db.Database.CurrentTransaction.GetDbTransaction();

            return await cmd.ExecuteNonQueryAsync(ct);
        }
        finally
        {
            if (openedHere)
                await db.Database.CloseConnectionAsync();
        }
    }

    private static NpgsqlParameter TextArray(string name, IEnumerable<string?> values) =>
        new(name, NpgsqlDbType.Array | NpgsqlDbType.Text) { Value = values.ToArray() };

    private static NpgsqlParameter BigintArray(string name, IEnumerable<long> values) =>
        new(name, NpgsqlDbType.Array | NpgsqlDbType.Bigint) { Value = values.ToArray() };

    private static NpgsqlParameter BoolArray(string name, IEnumerable<bool> values) =>
        new(name, NpgsqlDbType.Array | NpgsqlDbType.Boolean) { Value = values.ToArray() };

    private static NpgsqlParameter TimestampTzArray(string name, IEnumerable<DateTimeOffset?> values) =>
        new(name, NpgsqlDbType.Array | NpgsqlDbType.TimestampTz) { Value = values.ToArray() };
}
