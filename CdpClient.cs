using System.Net.WebSockets;
using System.Text.Json;

namespace DinnerTray;

// One in-flight command per connection. Consume unsolicited CDP events while waiting.
internal sealed class CdpClient : IDisposable
{
    private readonly ClientWebSocket socket = new();
    private readonly SemaphoreSlim gate = new(1);
    private int sequence;
    public async Task ConnectAsync(string url)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(4));
        await socket.ConnectAsync(new Uri(url), timeout.Token);
    }
    public async Task<JsonElement> CallAsync(string method, object? parameters = null)
    {
        await gate.WaitAsync();
        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(4));
            int id = ++sequence;
            var bytes = JsonSerializer.SerializeToUtf8Bytes(new { id, method, @params = parameters ?? new { } });
            await socket.SendAsync(bytes, WebSocketMessageType.Text, true, timeout.Token);
            var buffer = new byte[16384];
            while (true)
            {
                using var message = new MemoryStream();
                WebSocketReceiveResult part;
                do
                {
                    part = await socket.ReceiveAsync(buffer, timeout.Token);
                    if (part.MessageType == WebSocketMessageType.Close) throw new IOException("Browser disconnected.");
                    message.Write(buffer, 0, part.Count);
                    if (message.Length > 4_000_000) throw new IOException("Unexpected browser response size.");
                } while (!part.EndOfMessage);
                using var json = JsonDocument.Parse(message.ToArray());
                var root = json.RootElement;
                if (!root.TryGetProperty("id", out var received) || received.GetInt32() != id) continue;
                if (root.TryGetProperty("error", out var error)) throw new IOException(error.ToString());
                return root.GetProperty("result").Clone();
            }
        }
        finally { gate.Release(); }
    }
    public async Task<JsonElement> EvaluateAsync(string expression, bool userGesture = false)
    {
        var result = await CallAsync("Runtime.evaluate", new { expression, returnByValue = true, awaitPromise = true, userGesture });
        if (result.TryGetProperty("exceptionDetails", out var error)) throw new IOException(error.ToString());
        return result.GetProperty("result").GetProperty("value").Clone();
    }
    public void Dispose() => socket.Dispose();
}
