using System.Runtime.InteropServices;

namespace NativeBattle.Runtime;

internal static class Unicorn
{
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetProcessMitigationPolicy(nint process, int policy, out uint flags, nuint length);

    internal static void ValidateHost()
    {
        if (OperatingSystem.IsWindows())
        {
            if (!GetProcessMitigationPolicy(-1, 7, out uint flags, 4))
                throw new NativeBattleException("Cannot determine Windows emulator host compatibility.");
            if ((flags & 1) != 0)
                throw new NativeBattleException("Unicorn requires the dedicated worker executable. Do not load it inside the server or launch through dotnet.exe (CFG incompatibility).");
        }
    }
    private const string Library = "unicorn";
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal delegate void CodeHook(nint engine, ulong address, uint size, nint context);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] internal static extern int uc_open(int architecture, int mode, out nint engine);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] internal static extern int uc_close(nint engine);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] internal static extern int uc_mem_map(nint engine, ulong address, nuint size, uint permissions);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] internal static extern int uc_mem_write(nint engine, ulong address, byte[] data, nuint size);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] internal static extern int uc_mem_read(nint engine, ulong address, byte[] data, nuint size);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] internal static extern int uc_reg_write(nint engine, int register, ref uint value);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] internal static extern int uc_reg_read(nint engine, int register, out uint value);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] internal static extern int uc_emu_start(nint engine, ulong begin, ulong until, ulong timeout, nuint count);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] internal static extern int uc_emu_stop(nint engine);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] internal static extern int uc_hook_add(nint engine, out nuint hook, int type, CodeHook callback, nint context, ulong begin, ulong end);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] private static extern nint uc_strerror(int error);

    internal static void Check(int error)
    {
        if (error != 0) throw new NativeBattleException(Marshal.PtrToStringAnsi(uc_strerror(error)) ?? $"Unicorn error {error}");
    }
}

public sealed class NativeBattleException(string message) : Exception(message);
