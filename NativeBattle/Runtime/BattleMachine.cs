namespace NativeBattle.Runtime;

public sealed record CardExecution(uint Tick, uint Result);
public sealed record BattleStatus(uint Tick, bool Finished, uint Side0Crowns, uint Side1Crowns);
public sealed record BattleObject(uint Id, uint Type, int X, int Y);

// Owns a single battle; invoke from one worker thread in an isolated process.
public sealed class BattleMachine : IDisposable
{
    private readonly ArmRuntime runtime;
    private readonly SortedDictionary<uint, List<uint>> pending = new();
    private uint game;
    private uint outputStream;
    private int pendingCount;
    public BattleMachine(string image, string gamefiles, Action<string>? progress = null, string? dataCache = null)
    {
        runtime = new ArmRuntime(image);
        try
        {
            if (dataCache != null && runtime.RestoreDataSnapshot(dataCache, gamefiles))
            { progress?.Invoke("Loaded validated native data cache"); return; }
            var resources = new BattleResources(runtime, gamefiles);
            resources.Load(progress);
            resources.LoadLocations(progress);
            if (dataCache != null) runtime.SaveDataSnapshot(dataCache, resources.Hashes);
        }
        catch { runtime.Dispose(); throw; }
    }

    public void Load(byte[] state)
    {
        if (game != 0) throw new InvalidOperationException("A worker owns one battle.");
        if (state.Length is < 1 or > 1024 * 1024) throw new ArgumentException("Invalid battle-state size.");
        game = runtime.ConstructBattle();
        // A headless authority permits logic mutation between ticks (command execution).
        // decode/updateOneTick save and restore this flag rather than owning its lifetime.
        runtime.Write(game + 0x130, [1]);
        uint stream = runtime.ByteStream(state);
        if (runtime.Call(0x10CF28, stream) == 0) throw new NativeBattleException("Initial battle must include decodeOnce data.");
        runtime.CallBounded(0x249EC4, 100_000_000, 30_000_000, game, stream);
        runtime.CallBounded(0x1B316C, 100_000_000, 30_000_000, game, stream, 1, 0);
        if (runtime.U32(stream + 0x10) != state.Length) throw new NativeBattleException("Battle packet was not consumed exactly.");
    }

    public void QueuePlaceCard(byte[] payload)
    {
        EnsureLoaded();
        if (payload.Length is < 1 or > 256 || pendingCount >= 256) throw new ArgumentException("Card-command limit exceeded.");
        uint command = runtime.Allocate(0x30);
        runtime.Put32(command, ArmRuntime.Base + 0x4FBA9C);
        runtime.Call(0x35AEDC, command);
        uint stream = runtime.ByteStream(payload);
        runtime.Call(0x231176, command, stream);
        if (runtime.U32(stream + 0x10) != payload.Length) throw new NativeBattleException("Card packet was not consumed exactly.");
        uint tick = runtime.U32(command + 8), current = runtime.U32(game + 0x34);
        if (tick < current || tick - current > 1200) throw new ArgumentException("Card tick is outside the scheduling window.");
        if (!pending.TryGetValue(tick, out var commands)) pending.Add(tick, commands = new());
        commands.Add(command);
        pendingCount++;
    }

    public IReadOnlyList<CardExecution> Advance(uint targetTick)
    {
        EnsureLoaded();
        uint tick = runtime.U32(game + 0x34);
        if (targetTick < tick || targetTick - tick > 2000) throw new ArgumentException("Advance must be between zero and 2000 ticks.");
        var executed = new List<CardExecution>();
        while (tick < targetTick)
        {
            if (runtime.Call(0x11D4F4, runtime.U32(game + 0x78)) != 0) break;
            if (pending.Remove(tick, out var commands))
            {
                pendingCount -= commands.Count;
                foreach (uint command in commands)
                    // Bit 1 applies the spell to simulation; zero only reserves client-preview elixir.
                    executed.Add(new CardExecution(tick, runtime.CallBounded(0x169FBC, 100_000_000, 10_000_000, command, game, 2, 0)));
            }
            runtime.ElapsedNanoseconds = (ulong)tick * 50_000_000;
            runtime.CallBounded(0x3166D8, 50_000_000, 5_000_000, game);
            uint next = runtime.U32(game + 0x34);
            if (next == tick)
            {
                if (runtime.Call(0x11D4F4, runtime.U32(game + 0x78)) != 0) break;
                throw new NativeBattleException("Native simulation did not advance.");
            }
            if (next != tick + 1) throw new NativeBattleException("Native simulation advanced by an unexpected tick count.");
            tick = next;
        }
        return executed;
    }

    public BattleStatus Status()
    {
        EnsureLoaded();
        uint battle = runtime.U32(game + 0x78);
        uint first = runtime.U32(battle + 0x50), second = runtime.U32(battle + 0x54);
        if (first == 0 || second == 0) throw new NativeBattleException("Missing battle summoners.");
        return new BattleStatus(runtime.U32(game + 0x34), runtime.Call(0x11D4F4, battle) != 0,
            runtime.Call(0x32E004, first, second), runtime.Call(0x32E004, second, first));
    }

    public uint Checksum()
    {
        EnsureLoaded();
        return runtime.CallBounded(0x265910, 50_000_000, 5_000_000, game);
    }

    // Native encodeOnce + full encode, including the SectorState presence bit.
    // The output stream is reused so periodic snapshots do not consume the battle heap.
    public byte[] ExportState()
    {
        EnsureLoaded();
        if (outputStream == 0) outputStream = runtime.ByteStream(new byte[1024 * 1024]);
        runtime.Call(0x245110, outputStream);
        runtime.Put32(outputStream + 0x10, 0);
        runtime.Put32(outputStream + 0x18, 0);
        runtime.Call(0x15DD04, outputStream, 1);
        runtime.CallBounded(0x159318, 100_000_000, 10_000_000, game, outputStream, 1, 0);
        runtime.CallBounded(0x2DAAD8, 100_000_000, 10_000_000, game, outputStream, 1, 0, 0);
        uint length = runtime.U32(outputStream + 0x10);
        if (length > 1024 * 1024) throw new NativeBattleException("Encoded battle state exceeds its limit.");
        return runtime.Read(runtime.U32(outputStream + 0x1C), length);
    }

    public IReadOnlyList<BattleObject> Objects()
    {
        EnsureLoaded();
        uint manager = runtime.U32(game + 0x24), array = runtime.U32(manager), count = runtime.U32(manager + 8);
        if (count > 8192) throw new NativeBattleException("Native object count exceeds its limit.");
        var objects = new List<BattleObject>();
        for (uint i = 0; i < count; i++)
        {
            uint obj = runtime.U32(array + i * 4);
            uint getType = runtime.U32(runtime.U32(obj) + 0x2C) - ArmRuntime.Base;
            objects.Add(new BattleObject(runtime.Call(0x17A0F0, obj), runtime.Call(getType, obj),
                unchecked((int)runtime.Call(0x2A31DA, obj)), unchecked((int)runtime.Call(0x21ECF0, obj))));
        }
        return objects;
    }

    private void EnsureLoaded() { if (game == 0) throw new InvalidOperationException("No battle loaded."); }
    public void Dispose() => runtime.Dispose();
}
