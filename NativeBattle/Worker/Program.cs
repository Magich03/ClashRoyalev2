using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using NativeBattle.Runtime;

// Local process protocol only. The C# server owns authentication and networking.
if (args.Length is < 3 or > 4)
{
    Console.Error.WriteLine("Usage: NativeBattle.Worker <unicorn library> <verified libg dump> <server Gamefiles directory> [validated data cache]");
    return 2;
}
try
{
    nint library = NativeLibrary.Load(Path.GetFullPath(args[0]));
    NativeLibrary.SetDllImportResolver(typeof(ArmRuntime).Assembly, (name, _, _) => name == "unicorn" ? library : 0);
    using var machine = new BattleMachine(args[1], args[2], dataCache: args.Length == 4 ? args[3] : null);
    Send(new { type = "ready", protocol = 1, image = ArmRuntime.ImageHash });
    while (ReadBoundedLine(Console.In) is { } line)
    {
        using var request = JsonDocument.Parse(line, new JsonDocumentOptions { MaxDepth = 8 });
        var root = request.RootElement;
        switch (root.GetProperty("type").GetString())
        {
            case "start":
                machine.Load(root.GetProperty("state").GetBytesFromBase64());
                Send(new { type = "started", status = machine.Status() });
                break;
            case "card":
                machine.QueuePlaceCard(root.GetProperty("payload").GetBytesFromBase64());
                Send(new { type = "queued" });
                break;
            case "advance":
                var executed = machine.Advance(root.GetProperty("tick").GetUInt32());
                Send(new { type = "advanced", executed, status = machine.Status() });
                break;
            case "step":
                var before = machine.Status();
                uint target = root.GetProperty("tick").GetUInt32();
                if (target < before.Tick || target - before.Tick > 10) throw new ArgumentException("A server step must contain at most ten ticks.");
                var commands = root.GetProperty("commands");
                if (commands.GetArrayLength() > 64) throw new ArgumentException("Too many commands in a server step.");
                foreach (var command in commands.EnumerateArray()) machine.QueuePlaceCard(command.GetBytesFromBase64());
                var checksums = new[] { new { tick = before.Tick, checksum = machine.Checksum() } };
                var results = machine.Advance(target);
                Send(new { type = "stepped", executed = results, checksums, status = machine.Status() });
                break;
            case "status": Send(new { type = "status", status = machine.Status() }); break;
            case "snapshot": Send(new { type = "snapshot", status = machine.Status(), checksum = machine.Checksum(), state = Convert.ToBase64String(machine.ExportState()) }); break;
            case "stop": Send(new { type = "stopped" }); return 0;
            default: throw new ArgumentException("Unsupported worker request.");
        }
    }
    return 0;
}
catch (Exception error)
{
    Send(new { type = "error", message = error.Message });
    // A failed native operation invalidates the entire battle. Never continue it.
    return 1;
}

static void Send(object response)
{
    Console.WriteLine(JsonSerializer.Serialize(response));
    Console.Out.Flush();
}

static string? ReadBoundedLine(TextReader input)
{
    const int maxLength = 1500000;
    var line = new StringBuilder();
    for (int character; (character = input.Read()) >= 0;)
    {
        if (character == '\n') return line.ToString();
        if (line.Length >= maxLength) throw new ArgumentException("Worker request exceeds maximum size.");
        line.Append((char)character);
    }
    return line.Length == 0 ? null : line.ToString();
}
