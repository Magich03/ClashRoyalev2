namespace ClashRoyale.Logic.Battle;

using System.Diagnostics;
using System.Text.Json;
using ClashRoyale.Extensions;
using ClashRoyale.Logic.Commands;

// The network server never loads Unicorn. Each battle owns one bounded worker process.
internal sealed class NativeBattleHost : IDisposable
{
    private readonly Process process;
    private bool disposed;
    internal sealed record Status(int Tick, bool Finished, int Side0Crowns, int Side1Crowns);
    internal sealed record Snapshot(Status Status, uint Checksum, byte[] State);
    public Status Current { get; private set; }
    public Dictionary<int, uint> Checksums { get; } = new();
    // Disabled until live synchronization and background/resume behavior are verified.
    public static bool Enabled => false;

    public NativeBattleHost(byte[] state)
    {
        string directory = Path.GetFullPath(Environment.GetEnvironmentVariable("CR_NATIVE_DIRECTORY") ?? Path.Combine(AppContext.BaseDirectory, "NativeBattle"));
        string data = Path.GetFullPath(Environment.GetEnvironmentVariable("CR_NATIVE_GAMEFILES") ?? Path.Combine(AppContext.BaseDirectory, "Gamefiles"));
        var start = new ProcessStartInfo(Path.Combine(directory, "NativeBattle.Worker.exe"))
        {
            UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true
        };
        foreach (string argument in new[] { Path.Combine(directory, "unicorn.dll"), Path.Combine(directory, "libg-memory.so"), data, Path.Combine(directory, "native-data-cache.zip") })
            start.ArgumentList.Add(argument);
        process = new Process { StartInfo = start };
        try
        {
            if (!process.Start()) throw new IOException("Could not start native battle worker.");
            // Drain stderr so diagnostics can never block the worker's protocol pipe.
            process.ErrorDataReceived += (_, e) => { if (!string.IsNullOrEmpty(e.Data)) Console.WriteLine("[NATIVE] " + e.Data); };
            process.BeginErrorReadLine();
            using var ready = Read(120000);
            Require(ready, "ready");
            using var loaded = Exchange(new { type = "start", state = Convert.ToBase64String(state) }, "started");
            Current = ParseStatus(loaded.RootElement.GetProperty("status"));
            if (Current.Tick != 0) throw new IOException("Native battle started at an unexpected tick.");
        }
        catch { Dispose(); throw; }
    }

    public void Step(int targetTick, Command[] commands)
    {
        if (commands.Any(c => c is not PlaceCardCommand)) throw new InvalidOperationException("Unsupported native battle command.");
        var payloads = commands.Select(command =>
        {
            var bytes = new ByteStream();
            command.Encode(new ChecksumEncoder(bytes));
            return Convert.ToBase64String(bytes.ToArray(0, bytes.Offset));
        }).ToArray();
        using var response = Exchange(new { type = "step", tick = targetTick, commands = payloads }, "stepped");
        foreach (var item in response.RootElement.GetProperty("checksums").EnumerateArray())
            Checksums[item.GetProperty("tick").GetInt32()] = item.GetProperty("checksum").GetUInt32();
        foreach (int old in Checksums.Keys.Where(t => t < targetTick - 200).ToArray()) Checksums.Remove(old);
        Current = ParseStatus(response.RootElement.GetProperty("status"));
        if ((!Current.Finished && Current.Tick != targetTick) || Current.Tick > targetTick)
            throw new IOException("Native battle clock disagrees with the server.");
    }

    public Snapshot GetSnapshot()
    {
        using var response = Exchange(new { type = "snapshot" }, "snapshot");
        var root = response.RootElement;
        byte[] state = root.GetProperty("state").GetBytesFromBase64();
        if (state.Length > 1024 * 1024) throw new IOException("Native battle snapshot exceeds its limit.");
        return new Snapshot(ParseStatus(root.GetProperty("status")), root.GetProperty("checksum").GetUInt32(), state);
    }

    private static Status ParseStatus(JsonElement value) => new(value.GetProperty("Tick").GetInt32(), value.GetProperty("Finished").GetBoolean(),
        value.GetProperty("Side0Crowns").GetInt32(), value.GetProperty("Side1Crowns").GetInt32());
    private JsonDocument Exchange(object request, string expected)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        try
        {
            process.StandardInput.WriteLine(JsonSerializer.Serialize(request));
            var response = Read(15000);
            try { Require(response, expected); return response; }
            catch { response.Dispose(); throw; }
        }
        catch { Dispose(); throw; }
    }
    private JsonDocument Read(int timeout)
    {
        var line = process.StandardOutput.ReadLineAsync();
        if (!line.Wait(timeout)) throw new TimeoutException("Native battle worker timed out.");
        string text = line.Result ?? throw new IOException("Native battle worker exited.");
        if (text.Length > 1500000) throw new IOException("Native battle response exceeds its limit.");
        return JsonDocument.Parse(text, new JsonDocumentOptions { MaxDepth = 16 });
    }
    private static void Require(JsonDocument document, string expected)
    {
        string? type = document.RootElement.GetProperty("type").GetString();
        if (type == "error") throw new IOException("Native battle: " + document.RootElement.GetProperty("message").GetString());
        if (type != expected) throw new IOException("Unexpected native battle response: " + type);
    }
    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        try { if (!process.HasExited) { process.Kill(entireProcessTree: true); process.WaitForExit(5000); } }
        catch (InvalidOperationException) { }
        finally { process.Dispose(); }
    }
}
