using Microsoft.JSInterop;

namespace NMAC.Ui.LiveStreams;

public sealed class BrowserTimeInterop(IJSRuntime jsRuntime) : IAsyncDisposable
{
    private readonly Lazy<Task<IJSObjectReference>> _moduleTask = new(() => jsRuntime
        .InvokeAsync<IJSObjectReference>("import", "./_content/NMAC.Ui/browserTimeInterop.js")
        .AsTask());

    public async ValueTask<int> GetUtcOffsetMinutesAsync(DateTime localDateTime)
    {
        var module = await _moduleTask.Value;
        return await module.InvokeAsync<int>(
            "getUtcOffsetMinutesForLocalDateTime",
            localDateTime.Year,
            localDateTime.Month,
            localDateTime.Day,
            localDateTime.Hour,
            localDateTime.Minute);
    }

    public async ValueTask<BrowserLocalDateTimeParts> GetLocalDateTimePartsAsync(DateTimeOffset utcDateTime)
    {
        var module = await _moduleTask.Value;
        return await module.InvokeAsync<BrowserLocalDateTimeParts>(
            "getLocalDateTimePartsFromUtc",
            utcDateTime.UtcDateTime.ToString("O"));
    }

    public async ValueTask DisposeAsync()
    {
        if (_moduleTask.IsValueCreated)
        {
            var module = await _moduleTask.Value;
            await module.DisposeAsync();
        }
    }
}

public sealed class BrowserLocalDateTimeParts
{
    public int Year { get; set; }

    public int Month { get; set; }

    public int Day { get; set; }

    public int Hour { get; set; }

    public int Minute { get; set; }
}
