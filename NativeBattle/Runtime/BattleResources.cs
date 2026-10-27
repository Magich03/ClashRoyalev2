using System.Security.Cryptography;
using System.Text;

namespace NativeBattle.Runtime;

public sealed class BattleResources(ArmRuntime runtime, string gamefilesDirectory)
{
    private readonly string root = Path.GetFullPath(gamefilesDirectory).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
    private readonly Dictionary<string, uint> nodes = new(StringComparer.Ordinal);
    public Dictionary<string, string> Hashes { get; } = new(StringComparer.Ordinal);

    public string NativeString(uint pointer)
    {
        uint length = runtime.U32(pointer + 4);
        return Encoding.UTF8.GetString(runtime.Read(length >= 8 ? runtime.U32(pointer + 8) : pointer + 8, length));
    }
    public uint Csv(string name)
    {
        if (nodes.TryGetValue(name, out uint existing)) return existing;
        string path = Path.GetFullPath(Path.Combine(root, name));
        if (!path.StartsWith(root, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
            throw new NativeBattleException("Resource path leaves the server Gamefiles directory.");
        byte[] raw = File.ReadAllBytes(path);
        Hashes[name] = Convert.ToHexString(SHA256.HashData(raw));
        byte[] delimiter = raw.AsSpan().IndexOf("\r\n"u8) >= 0 ? "\r\n"u8.ToArray() : "\n"u8.ToArray();
        uint lines = runtime.CallBounded(0x137CEC, 2_000_000_000, 60_000_000,
            runtime.Buffer(raw), runtime.Buffer([.. delimiter, 0]), (uint)raw.Length, (uint)delimiter.Length);
        uint filename = runtime.Allocate(20);
        runtime.Call(0x119CB8, filename, runtime.Buffer([.. Encoding.UTF8.GetBytes(name), 0]));
        uint node = runtime.Allocate(0x60);
        runtime.CallBounded(0x50244, 2_000_000_000, 60_000_000, node, lines, filename);
        // Location files contain named child tables; their root table is empty.
        if (runtime.U32(node) == 0 && (name.StartsWith("csv_logic/", StringComparison.Ordinal) || name.StartsWith("csv_client/", StringComparison.Ordinal)))
            throw new NativeBattleException($"Native parser produced no table for {name}.");
        nodes.Add(name, node);
        return node;
    }
    public void Load(Action<string>? progress = null)
    {
        runtime.InitializeStatics();
        uint resources = runtime.Call(0xD0584);
        uint entries = runtime.U32(resources), count = runtime.U32(resources + 8);
        if (count != 70) throw new NativeBattleException($"Unexpected resource count {count}.");
        for (uint index = 0; index < count; index++)
        {
            uint resource = runtime.U32(entries + index * 4), kind = runtime.U32(resource + 0x20);
            if (kind > 1) throw new NativeBattleException($"Unknown resource kind {kind}.");
            string name = NativeString(resource + 8);
            progress?.Invoke($"Loading {name}");
            uint node = Csv(name);
            if (kind == 0) runtime.CallBounded(0x11A84C, 100_000_000, 30_000_000, resources, index, node, 0, 0);
            else if (runtime.U32(resource + 0x1C) != 0)
            {
                uint table = runtime.Call(0x2920CC);
                if (table == 0) throw new NativeBattleException("Text patch has no base string table.");
                runtime.CallBounded(0x131CC8, 100_000_000, 30_000_000, table, runtime.U32(node));
            }
            else
            {
                runtime.Call(0x32022C);
                runtime.CallBounded(0x241C80, 100_000_000, 30_000_000, runtime.U32(node));
            }
        }
        progress?.Invoke("Finalizing native game tables");
        runtime.CallBounded(0x378764, 200_000_000, 30_000_000);
    }

    public void LoadLocations(Action<string>? progress = null)
    {
        using var parser = new Microsoft.VisualBasic.FileIO.TextFieldParser(Path.Combine(root, "csv_logic", "locations.csv"));
        parser.SetDelimiters(",");
        parser.HasFieldsEnclosedInQuotes = true;
        string[] headers = parser.ReadFields() ?? throw new NativeBattleException("Missing location headers.");
        parser.ReadFields(); // Column types.
        int nameColumn = Array.IndexOf(headers, "Name"), locationColumn = Array.IndexOf(headers, "FileName"), tilesColumn = Array.IndexOf(headers, "TileDataFileName");
        if (nameColumn < 0 || locationColumn < 0 || tilesColumn < 0) throw new NativeBattleException("Missing location columns.");
        uint index = 0;
        while (!parser.EndOfData)
        {
            string[] row = parser.ReadFields()!;
            if (string.IsNullOrEmpty(row[nameColumn])) continue;
            progress?.Invoke($"Loading location {row[nameColumn]}");
            runtime.CallBounded(0x18A678, 100_000_000, 30_000_000, Csv(row[locationColumn]), Csv(row[tilesColumn]), index++);
        }
    }
}
