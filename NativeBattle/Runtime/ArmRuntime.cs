using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.IO.Compression;
using System.Text.Json;

namespace NativeBattle.Runtime;

// One isolated ARM address space per worker. This class is not thread safe.
public sealed class ArmRuntime : IDisposable
{
    public const uint Base = 0xC0342000;
    public const string ImageHash = "FD1A97BB94700507C7EC115ED00F0E24A6C28C5A852408D725304B44BA83F6C9";
    private const uint Heap = 0x10000000, HeapSize = 64 * 1024 * 1024;
    private const uint Stack = 0x20000000, Imports = 0x30000000, Return = 0x30010000;
    private readonly byte[] image;
    private readonly Unicorn.CodeHook importHook;
    private readonly Dictionary<uint, string> imports = new();
    private readonly Dictionary<uint, uint> allocations = new();
    private readonly HashSet<uint> mutexes = new();
    private nint engine;
    private uint cursor = Heap;
    private string? fault;
    public bool Poisoned { get; private set; }
    public ulong ElapsedNanoseconds { get; set; }

    public ArmRuntime(string path)
    {
        Unicorn.ValidateHost();
        image = File.ReadAllBytes(path);
        if (Convert.ToHexString(SHA256.HashData(image)) != ImageHash)
            throw new NativeBattleException("Unsupported libg image; this profile requires the verified 3.830.6 dump.");
        importHook = OnImport;
        Unicorn.Check(Unicorn.uc_open(1, 0, out engine));
        try
        {
            Map(Base, ((uint)image.Length + 4095) & ~4095u);
            Write(Base, image);
            Write(Base + 0x7AA8CC, new byte[0x7B7BA0 - 0x7AA8CC]);
            Map(Heap, HeapSize);
            Map(Stack, 0x100000);
            Map(Imports, 0x20000);
            SetRegister(111, 0xF00000);
            SetRegister(4, 0x40000000);
            Unicorn.Check(Unicorn.uc_hook_add(engine, out _, 4, importHook, 0, Imports, Imports + 0xFFFF));
            BindImports();
        }
        catch { Dispose(); throw; }
    }

    private void Map(uint address, uint size) => Unicorn.Check(Unicorn.uc_mem_map(engine, address, size, 7));
    private uint Register(int id) { Unicorn.Check(Unicorn.uc_reg_read(engine, id, out uint value)); return value; }
    private void SetRegister(int id, uint value) => Unicorn.Check(Unicorn.uc_reg_write(engine, id, ref value));
    public byte[] Read(uint address, uint size)
    {
        ObjectDisposedException.ThrowIf(engine == 0, this);
        if (size > HeapSize) throw new NativeBattleException("Read exceeds memory budget.");
        byte[] data = new byte[size];
        if (size != 0) Unicorn.Check(Unicorn.uc_mem_read(engine, address, data, size));
        return data;
    }
    public void Write(uint address, byte[] data)
    {
        ObjectDisposedException.ThrowIf(engine == 0, this);
        if (data.Length != 0) Unicorn.Check(Unicorn.uc_mem_write(engine, address, data, (nuint)data.Length));
    }
    public uint U32(uint address) => BinaryPrimitives.ReadUInt32LittleEndian(Read(address, 4));
    public void Put32(uint address, uint value) { byte[] data = new byte[4]; BinaryPrimitives.WriteUInt32LittleEndian(data, value); Write(address, data); }
    public uint Allocate(uint size)
    {
        if (size > HeapSize) throw new NativeBattleException("Invalid native allocation.");
        size = Math.Max(size, 1);
        uint next = checked((cursor + size + 15) & ~15u);
        if (next > Heap + HeapSize) throw new NativeBattleException("Native heap exhausted.");
        uint result = cursor; cursor = next; allocations.Add(result, size); return result;
    }
    public uint Buffer(byte[] data) { uint address = Allocate((uint)data.Length); Write(address, data); return address; }
    private byte[] CString(uint address, uint limit = 1024 * 1024, bool terminated = true)
    {
        if (limit > HeapSize) throw new NativeBattleException("Invalid string limit.");
        using var bytes = new MemoryStream();
        while (bytes.Length < limit)
        {
            uint current = checked(address + (uint)bytes.Length);
            byte[] block = Read(current, Math.Min(4096 - (current & 4095), limit - (uint)bytes.Length));
            int end = Array.IndexOf(block, (byte)0);
            bytes.Write(block, 0, end < 0 ? block.Length : end);
            if (end >= 0) return bytes.ToArray();
        }
        if (terminated) throw new NativeBattleException("Unterminated native string.");
        return bytes.ToArray();
    }
    private uint ImageU32(uint offset) => BinaryPrimitives.ReadUInt32LittleEndian(image.AsSpan((int)offset, 4));
    private void BindImports()
    {
        var tags = new Dictionary<uint, uint>();
        for (uint p = 0x5037F4; p < 0x50392C; p += 8) tags[ImageU32(p)] = ImageU32(p + 4);
        uint guard = Allocate(4); Put32(guard, 0x61AC37E9);
        foreach (var (start, length) in new[] { (tags[17], tags[18]), (tags[23], tags[2]) })
        for (uint p = start; p < start + length; p += 8)
        {
            uint target = ImageU32(p), info = ImageU32(p + 4), index = info >> 8;
            if (index == 0 || (info & 255) is not (21 or 22)) continue;
            uint symbol = tags[6] + index * 16;
            if (BinaryPrimitives.ReadUInt16LittleEndian(image.AsSpan((int)symbol + 14, 2)) != 0) continue;
            int nameAt = checked((int)(tags[5] + ImageU32(symbol)));
            string name = Encoding.ASCII.GetString(image, nameAt, Array.IndexOf(image, (byte)0, nameAt) - nameAt);
            uint pointer;
            if (name == "__stack_chk_guard") pointer = guard;
            else
            {
                pointer = Imports + (uint)imports.Count * 4;
                if (pointer >= Return) throw new NativeBattleException("Import table exceeds reserved memory.");
                imports.Add(pointer, name);
                Write(pointer, [0x1e, 0xff, 0x2f, 0xe1]);
            }
            Put32(Base + target, pointer);
        }
    }
    private void OnImport(nint ignored, ulong address, uint size, nint context)
    {
        // Exceptions must never cross an unmanaged callback boundary.
        try
        {
            string name = imports[(uint)address];
            uint result = InvokeImport(name, Register(66), Register(67), Register(68));
            SetRegister(66, result);
            SetRegister(11, Register(10));
        }
        catch (Exception error)
        {
            fault = $"Import {imports.GetValueOrDefault((uint)address)}: {error.Message}";
            Unicorn.uc_emu_stop(engine);
        }
    }
    private uint InvokeImport(string name, uint a, uint b, uint c)
    {
        switch (name)
        {
            case "__errno": return Imports + 0x1F000;
            case "strtoull":
                ulong integer = ParseUnsigned(a, b, c);
                SetRegister(67, (uint)(integer >> 32));
                return (uint)integer;
            case "__cxa_atexit": return 0; // Destruction releases the entire isolated address space.
            case "pthread_cond_signal": case "pthread_cond_broadcast": Read(a, 4); return 0;
            case "pthread_mutex_lock":
                Read(a, 4); if (!mutexes.Add(a)) throw new NativeBattleException("Native mutex would deadlock."); return 0;
            case "pthread_mutex_unlock":
                if (!mutexes.Remove(a)) throw new NativeBattleException("Unlock of an unlocked mutex."); return 0;
            case "pthread_mutex_init": case "pthread_mutex_destroy":
                if (mutexes.Contains(a) || (name == "pthread_mutex_init" && b != 0)) throw new NativeBattleException("Unsupported mutex attributes/state.");
                Put32(a, 0); return 0;
            case "clock_gettime":
                if (a is not (0 or 1 or 4 or 7)) throw new NativeBattleException("Unsupported clock.");
                Put32(b, (uint)(ElapsedNanoseconds / 1000000000)); Put32(b + 4, (uint)(ElapsedNanoseconds % 1000000000)); return 0;
            case "time": uint seconds = (uint)(ElapsedNanoseconds / 1000000000); if (a != 0) Put32(a, seconds); return seconds;
            case "malloc": return Allocate(a);
            case "calloc": return Allocate(checked(a * b));
            case "free": if (a != 0 && !allocations.ContainsKey(a)) throw new NativeBattleException("Invalid free."); return 0;
            case "realloc":
                if (a != 0 && !allocations.ContainsKey(a)) throw new NativeBattleException("Invalid realloc.");
                uint replacement = Allocate(b); if (a != 0) Write(replacement, Read(a, Math.Min(allocations[a], b))); return replacement;
            case "strlen": return (uint)CString(a).Length;
            case "strnlen": return (uint)CString(a, b, false).Length;
            case "strcpy": Write(a, [.. CString(b), 0]); return a;
            case "strncpy":
                byte[] text = CString(b, c, false); byte[] padded = new byte[c]; text.CopyTo(padded, 0); Write(a, padded); return a;
            case "strcmp": case "strncmp": case "strcasecmp":
                byte[] left = name == "strncmp" ? CString(a, c, false) : CString(a);
                byte[] right = name == "strncmp" ? CString(b, c, false) : CString(b);
                if (name == "strcasecmp") { LowerAscii(left); LowerAscii(right); }
                return unchecked((uint)left.AsSpan().SequenceCompareTo(right));
            case "memcmp": return unchecked((uint)Read(a, c).AsSpan().SequenceCompareTo(Read(b, c)));
            case "atoi":
                var match = Regex.Match(Encoding.ASCII.GetString(CString(a)), @"^\s*([+-]?\d+)");
                return match.Success ? unchecked((uint)int.Parse(match.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture)) : 0;
            case "memcpy": case "memmove": case "__aeabi_memcpy": case "__aeabi_memcpy4": case "__aeabi_memcpy8": case "__aeabi_memmove": Write(a, Read(b, c)); return a;
            case "memset": case "__aeabi_memset": case "__aeabi_memset4": case "__aeabi_memset8":
                uint count = name == "memset" ? c : b;
                if (count > HeapSize) throw new NativeBattleException("Invalid memset size.");
                byte[] fill = new byte[count]; Array.Fill(fill, (byte)(name == "memset" ? b : c)); Write(a, fill); return a;
            case "__aeabi_memclr": case "__aeabi_memclr4": case "__aeabi_memclr8":
                if (b > HeapSize) throw new NativeBattleException("Invalid memclr size."); Write(a, new byte[b]); return a;
            default: throw new NativeBattleException($"Unsupported Android import {name}, caller +0x{unchecked(Register(10) - Base):x}.");
        }
    }
    private static void LowerAscii(byte[] bytes) { for (int i = 0; i < bytes.Length; i++) if (bytes[i] is >= 65 and <= 90) bytes[i] += 32; }
    private ulong ParseUnsigned(uint address, uint endPointer, uint numberBase)
    {
        byte[] text = CString(address);
        int pos = 0;
        if (numberBase != 0 && (numberBase < 2 || numberBase > 36))
        {
            Put32(Imports + 0x1F000, 22);
            if (endPointer != 0) Put32(endPointer, address);
            return 0;
        }
        while (pos < text.Length && (text[pos] == 32 || text[pos] is >= 9 and <= 13)) pos++;
        bool negative = pos < text.Length && text[pos] == '-';
        if (pos < text.Length && text[pos] is (byte)'+' or (byte)'-') pos++;
        static int Digit(byte ch) => ch is >= 48 and <= 57 ? ch - 48 : ch is >= 65 and <= 90 ? ch - 65 + 10 : ch is >= 97 and <= 122 ? ch - 97 + 10 : 99;
        if ((numberBase == 0 || numberBase == 16) && pos + 2 < text.Length && text[pos] == '0' && text[pos + 1] is (byte)'x' or (byte)'X' && Digit(text[pos + 2]) < 16)
        { numberBase = 16; pos += 2; }
        if (numberBase == 0) numberBase = pos < text.Length && text[pos] == '0' ? 8u : 10u;
        int start = pos;
        ulong value = 0;
        bool overflow = false;
        while (pos < text.Length && Digit(text[pos]) < numberBase)
        {
            uint digit = (uint)Digit(text[pos++]);
            if (value > (ulong.MaxValue - digit) / numberBase) overflow = true;
            value = unchecked(value * numberBase + digit);
        }
        if (endPointer != 0) Put32(endPointer, pos == start ? address : address + (uint)pos);
        if (overflow) { Put32(Imports + 0x1F000, 34); return ulong.MaxValue; }
        return negative ? unchecked(0ul - value) : value;
    }
    public uint Call(uint offset, params uint[] args) => CallBounded(offset, 2_000_000, 2_000_000, args);
    public uint CallBounded(uint offset, nuint instructions, ulong timeoutMicroseconds, params uint[] args)
    {
        ObjectDisposedException.ThrowIf(engine == 0, this);
        if (Poisoned) throw new NativeBattleException("A failed call invalidated this worker.");
        if (offset >= 0x438040 || args.Length > 12 || instructions == 0 || timeoutMicroseconds == 0) throw new NativeBattleException("Invalid native call/budget.");
        fault = null;
        try
        {
            SetRegister(3, 0x10);
            for (int i = 0; i < 4; i++) SetRegister(66 + i, i < args.Length ? args[i] : 0);
            uint stack = Stack + 0x100000 - 64;
            SetRegister(12, stack);
            for (int i = 4; i < args.Length; i++) Put32(stack + (uint)(i - 4) * 4, args[i]);
            SetRegister(10, Return);
            int error = Unicorn.uc_emu_start(engine, Base + (offset | 1), Return, timeoutMicroseconds, instructions);
            if (fault != null) throw new NativeBattleException(fault);
            Unicorn.Check(error);
            if (Register(11) != Return) throw new NativeBattleException("Native call exceeded its time/instruction budget.");
            return Register(66);
        }
        catch (Exception error)
        {
            Poisoned = true;
            throw new NativeBattleException($"{error.Message} (libg PC +0x{unchecked(Register(11) - Base):x}, LR +0x{unchecked(Register(10) - Base):x})");
        }
    }
    public uint ByteStream(byte[] payload)
    {
        uint stream = Allocate(0x28); Put32(stream, Base + 0x4F5830); Put32(stream + 0x1C, Buffer(payload)); Put32(stream + 0x20, (uint)payload.Length); return stream;
    }
    public uint ConstructBattle() { uint game = Allocate(0x138); Call(0x1CE054, game, 1); return game; }
    public void InitializeStatics()
    {
        for (uint p = 0x5035A4; p < 0x5037F4; p += 4)
        {
            uint pointer = ImageU32(p); if (pointer == 0) continue;
            uint offset = pointer - Base;
            if (offset is < 0x3F000 or >= 0x4C000) throw new NativeBattleException("Unexpected static initializer.");
            Call(offset);
        }
    }
    private sealed record DataSnapshot(int Schema, string Image, uint Cursor, Dictionary<uint, uint> Allocations, Dictionary<string, string> Assets);

    public void SaveDataSnapshot(string path, Dictionary<string, string> assets)
    {
        if (Poisoned || mutexes.Count != 0) throw new NativeBattleException("Cannot cache a failed or locked runtime.");
        string absolute = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(absolute)!);
        string temporary = absolute + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var archive = ZipFile.Open(temporary, ZipArchiveMode.Create))
            {
                void Add(string name, byte[] data) { using var stream = archive.CreateEntry(name, CompressionLevel.Fastest).Open(); stream.Write(data); }
                Add("state.json", JsonSerializer.SerializeToUtf8Bytes(new DataSnapshot(1, ImageHash, cursor, allocations, assets)));
                Add("module.bin", Read(Base, (uint)image.Length));
                Add("heap.bin", Read(Heap, cursor - Heap));
            }
            File.Move(temporary, absolute, true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }

    public bool RestoreDataSnapshot(string path, string gamefiles)
    {
        if (Poisoned || cursor != Heap + 16 || mutexes.Count != 0) throw new NativeBattleException("Data cache requires a fresh worker.");
        if (!File.Exists(path)) return false;
        using var archive = ZipFile.OpenRead(path);
        byte[] Entry(string name, long maximum)
        {
            var entry = archive.GetEntry(name) ?? throw new InvalidDataException("Missing cache entry.");
            if (entry.Length < 0 || entry.Length > maximum) throw new InvalidDataException("Invalid cache entry size.");
            using var stream = entry.Open();
            byte[] bytes = new byte[(int)entry.Length]; stream.ReadExactly(bytes); return bytes;
        }
        var state = JsonSerializer.Deserialize<DataSnapshot>(Entry("state.json", 16 * 1024 * 1024)) ?? throw new InvalidDataException("Missing cache state.");
        if (state.Schema != 1 || state.Image != ImageHash) return false;
        if (state.Cursor <= Heap || state.Cursor > Heap + HeapSize) throw new InvalidDataException("Invalid cache heap.");
        string root = Path.GetFullPath(gamefiles).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        foreach (var asset in state.Assets)
        {
            string source = Path.GetFullPath(Path.Combine(root, asset.Key));
            if (!source.StartsWith(root, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal)) throw new InvalidDataException("Cache asset path leaves Gamefiles.");
            if (!File.Exists(source) || Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(source))) != asset.Value) return false;
        }
        byte[] module = Entry("module.bin", image.Length), heap = Entry("heap.bin", HeapSize);
        if (module.Length != image.Length || heap.Length != state.Cursor - Heap) throw new InvalidDataException("Cache memory sizes disagree.");
        foreach (var allocation in state.Allocations)
            if (allocation.Key < Heap || allocation.Value == 0 || (ulong)allocation.Key + allocation.Value > state.Cursor) throw new InvalidDataException("Invalid cached allocation.");
        Write(Base, module); Write(Heap, heap);
        cursor = state.Cursor;
        allocations.Clear();
        foreach (var allocation in state.Allocations) allocations.Add(allocation.Key, allocation.Value);
        return true;
    }
    public void Dispose()
    {
        if (engine != 0) { Unicorn.uc_close(engine); engine = 0; }
        GC.KeepAlive(importHook);
        GC.SuppressFinalize(this);
    }
    ~ArmRuntime() { if (engine != 0) Unicorn.uc_close(engine); }
}
