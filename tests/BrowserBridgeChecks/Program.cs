using System.Reflection;
using DinnerTray;

// Inject pending discovery results to exercise scheduling without a real browser or network.
const BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Instance;
void Set(BrowserBridge bridge, string name, object value) => typeof(BrowserBridge).GetField(name, flags)!.SetValue(bridge, value);
object? Get(BrowserBridge bridge, string name) => typeof(BrowserBridge).GetField(name, flags)!.GetValue(bridge);
Task Update(BrowserBridge bridge) => (Task)typeof(BrowserBridge).GetMethod("UpdateTargetAsync", flags)!.Invoke(bridge, null)!;
void Check(bool value, string message) { if (!value) throw new Exception(message); }

using (var bridge = new BrowserBridge())
{
    var active = new CdpClient();
    Set(bridge, "page", active);
    Set(bridge, "targetId", "playing");
    var slow = new TaskCompletionSource<(string Id, string Socket)?>();
    Set(bridge, "discovery", slow.Task);
    var update = Update(bridge);
    var first = await Task.WhenAny(update, Task.Delay(500));
    slow.SetResult(("playing", "unused"));
    await update;
    Check(first == update, "A slow unrelated tab must not block active capture");
    await Update(bridge);
    Check(ReferenceEquals(Get(bridge, "page"), active), "Rediscovery must preserve the active connection");

    Set(bridge, "discovery", Task.FromException<(string Id, string Socket)?>(new IOException("Discovery endpoint unavailable")));
    await Update(bridge);
    Check(ReferenceEquals(Get(bridge, "page"), active), "Discovery errors must not disconnect healthy playback");
    Check(Get(bridge, "discovery") == null, "Failed discovery must allow a later retry");
}

using (var bridge = new BrowserBridge())
{
    var pending = new TaskCompletionSource<(string Id, string Socket)?>();
    Set(bridge, "discovery", pending.Task);
    var update = Update(bridge);
    Check(!update.IsCompleted, "Initial connection must wait for discovery");
    pending.SetResult(null);
    await update;
    Check(Get(bridge, "page") == null, "No video targets must leave the bridge idle");
}
Console.WriteLine("PASS: discovery scheduling, connection preservation, failure recovery, and initial connection");
