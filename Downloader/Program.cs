using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using ClashRoyale.Compression.Lzma.Compress.LZMA;

namespace GL.Tools.Downloader;

internal static class Program
{
    private sealed record Asset(string File, string? Sha, Uri Url, bool Home);
    private sealed record Result(string File, string Status, string Source, string? Error = null);

    private static async Task<int> Main(string[] args)
    {
        using var stop = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) => { e.Cancel = true; stop.Cancel(); };
        try { return await Run(args, stop.Token); }
        catch (OperationCanceledException) { Console.Error.WriteLine("Cancelled. Completed files are kept; run again to resume."); return 130; }
        catch (Exception e) { Console.Error.WriteLine("Error: " + e.Message); return 1; }
        finally
        {
            if (args.Length == 0 && !Console.IsInputRedirected && !Console.IsOutputRedirected)
            {
                Console.WriteLine("Press Enter to close.");
                Console.ReadLine();
            }
        }
    }

    private static async Task<int> Run(string[] args, CancellationToken ct)
    {
        string fingerprint = "fingerprint.json", output = "Downloads/Gamefiles";
        string host = "https://game-assets.clashroyaleapp.com/";
        int jobs = 4;
        bool overwrite = false, raw = false, homes = true, explicitFingerprint = false;
        var only = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var extras = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < args.Length; i++)
        {
            string Value() => ++i < args.Length ? args[i] : throw new ArgumentException("Missing value for " + args[i - 1]);
            switch (args[i])
            {
                case "--help": case "-h":
                    Console.WriteLine("""
                    Downloads real version-matched assets and starting-home templates.
                    --fingerprint PATH|URL   Manifest (default: fingerprint.json)
                    --output DIRECTORY      Destination (default: Downloads/Gamefiles)
                    --host URL              CDN root; version SHA is appended
                    --only RELATIVE_PATH    Download selected file only; repeatable
                    --extra RELATIVE_PATH   Add a CDN file absent from the manifest
                    --jobs 1..16            Concurrent downloads (default: 4)
                    --overwrite             Replace existing files (default: preserve valid files)
                    --raw                   Keep compressed CSV/JSON bytes for client patches
                    --no-home               Omit the additional official starting_home.json
                    Server mode verifies CDN SHA-1, then decompresses CSV/JSON for the C# server.
                    Missing/corrupt downloads are reported, never replaced with fake files.
                    """); return 0;
                case "--fingerprint": fingerprint = Value(); explicitFingerprint = true; break;
                case "--output": output = Value(); break;
                case "--host": host = Value(); break;
                case "--only": only.Add(Value()); break;
                case "--extra": extras.Add(Value()); break;
                case "--jobs": jobs = int.Parse(Value()); break;
                case "--overwrite": overwrite = true; break;
                case "--raw": raw = true; break;
                case "--no-home": homes = false; break;
                default: throw new ArgumentException("Unknown option: " + args[i]);
            }
        }
        if (jobs < 1 || jobs > 16) throw new ArgumentException("--jobs must be between 1 and 16.");
        if (!explicitFingerprint)
        {
            var candidates = new[] {
                Path.GetFullPath("fingerprint.json"),
                Path.GetFullPath("Gamefiles/fingerprint.json"),
                Path.Combine(AppContext.BaseDirectory, "fingerprint.json")
            };
            fingerprint = candidates.FirstOrDefault(File.Exists)
                ?? throw new FileNotFoundException("No fingerprint.json found. Keep it beside Downloader.exe or use --fingerprint PATH.");
        }
        Console.WriteLine("Manifest: " + fingerprint);
        var cdn = RootUri(host);
        output = Path.GetFullPath(output);
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(60), MaxResponseContentBufferSize = 512 * 1024 * 1024 };
        http.DefaultRequestHeaders.UserAgent.ParseAdd("ClashRoyale-AssetDownloader/1.0");
        byte[] manifestBytes = Uri.TryCreate(fingerprint, UriKind.Absolute, out var manifestUri) && manifestUri.Scheme is "https" or "http"
            ? await Fetch(http, manifestUri, ct) : await File.ReadAllBytesAsync(fingerprint, ct);
        using var manifest = JsonDocument.Parse(manifestBytes);
        string sha = manifest.RootElement.GetProperty("sha").GetString() ?? "";
        if (!Regex.IsMatch(sha, "\\A[0-9a-fA-F]{40}\\z")) throw new InvalidDataException("Manifest version SHA must contain 40 hexadecimal digits.");
        var assets = new Dictionary<string, Asset>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in manifest.RootElement.GetProperty("files").EnumerateArray())
        {
            string file = SafeName(item.GetProperty("file").GetString() ?? "");
            string hash = item.GetProperty("sha").GetString() ?? "";
            if (!Regex.IsMatch(hash, "\\A[0-9a-fA-F]{40}\\z")) throw new InvalidDataException("Invalid file SHA: " + file);
            if (!assets.TryAdd(file, new Asset(file, hash, AssetUri(cdn, sha + "/" + file), false)))
                throw new InvalidDataException("Duplicate manifest path: " + file);
        }
        foreach (string extra in extras) { string file = SafeName(extra); assets.TryAdd(file, new Asset(file, null, AssetUri(cdn, sha + "/" + file), false)); }
        if (homes)
        {
            const string file = "level/starting_home.json";
            // The official CDN serves this versioned file even when it is absent from the fingerprint list.
            if (assets.TryGetValue(file, out var listed)) assets[file] = listed with { Home = true };
            else assets.Add(file, new Asset(file, null, AssetUri(cdn, sha + "/" + file), true));
        }
        foreach (string file in only)
            if (!assets.ContainsKey(SafeName(file))) throw new ArgumentException("Not in manifest or extra files: " + file);
        var selected = assets.Values.Where(a => only.Count == 0 || only.Contains(a.File)).ToArray();
        // Prepare the entire selected folder tree before starting network transfers.
        var folders = selected.Select(asset => Path.GetDirectoryName(Destination(output, asset.File))!)
            .Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        Directory.CreateDirectory(output);
        foreach (string folder in folders) Directory.CreateDirectory(folder);
        Console.WriteLine($"Prepared {folders.Length} asset folders in {output}");
        var results = new ConcurrentBag<Result>();
        await Parallel.ForEachAsync(selected, new ParallelOptions { MaxDegreeOfParallelism = jobs, CancellationToken = ct }, async (asset, token) =>
        {
            string destination = Destination(output, asset.File);
            try
            {
                if (File.Exists(destination) && !overwrite && !asset.Home)
                {
                    byte[] existing = await File.ReadAllBytesAsync(destination, token);
                    Validate(existing, asset.File, raw);
                    // Preserve customized server CSV/JSON; exact checksums apply to downloaded/raw assets.
                    if (asset.Sha != null && (raw || !IsText(asset.File))) CheckHash(existing, asset.Sha);
                    results.Add(new(asset.File, "kept", asset.Url.AbsoluteUri));
                    Console.WriteLine("KEEP " + asset.File); return;
                }
                byte[] bytes = await Fetch(http, asset.Url, token);
                if (asset.Sha != null) CheckHash(bytes, asset.Sha);
                if (!raw && IsText(asset.File)) bytes = DecodeText(bytes);
                Validate(bytes, asset.File, raw);
                if (asset.Home)
                {
                    using var home = JsonDocument.Parse(bytes);
                    if (home.RootElement.ValueKind != JsonValueKind.Object || !home.RootElement.TryGetProperty("decks", out _))
                        throw new InvalidDataException("Invalid starting-home template.");
                    if (File.Exists(destination) && !overwrite && !(await File.ReadAllBytesAsync(destination, token)).SequenceEqual(bytes))
                        throw new InvalidDataException("Existing home differs from the official file for this fingerprint. Use --overwrite to replace it.");
                }
                await AtomicWrite(destination, bytes, token);
                results.Add(new(asset.File, "downloaded", asset.Url.AbsoluteUri));
                Console.WriteLine("OK   " + asset.File);
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
            catch (Exception e)
            {
                results.Add(new(asset.File, "failed", asset.Url.AbsoluteUri, e.Message));
                Console.Error.WriteLine("FAIL " + asset.File + ": " + e.Message);
            }
        });
        // A filtered download must not masquerade as a complete client asset bundle.
        if (only.Count == 0 && results.All(r => r.Status != "failed") && (!File.Exists(Path.Combine(output, "fingerprint.json")) || overwrite))
            await AtomicWrite(Path.Combine(output, "fingerprint.json"), manifestBytes, ct);
        var report = new { versionSha = sha, format = raw ? "raw-client" : "server-decoded", files = results.OrderBy(r => r.File).ToArray() };
        await AtomicWrite(Path.Combine(output, "download-report.json"), JsonSerializer.SerializeToUtf8Bytes(report, new JsonSerializerOptions { WriteIndented = true }), ct);
        int failed = results.Count(r => r.Status == "failed");
        Console.WriteLine($"Finished: {results.Count(r => r.Status == "downloaded")} downloaded, {results.Count(r => r.Status == "kept")} kept, {failed} failed. Output: {output}");
        return failed == 0 ? 0 : 1;
    }

    private static Uri RootUri(string value)
    {
        if (!Uri.TryCreate(value.TrimEnd('/') + "/", UriKind.Absolute, out var uri) || uri.Scheme is not ("https" or "http") || uri.Query != "" || uri.Fragment != "")
            throw new ArgumentException("Source must be an HTTP(S) URL without a query or fragment.");
        return uri;
    }
    private static Uri AssetUri(Uri root, string file) => new(root, string.Join('/', file.Split('/').Select(Uri.EscapeDataString)));
    private static string SafeName(string name)
    {
        var parts = name.Split('/');
        if (parts.Any(p => p.Length == 0 || p is "." or ".." || p.EndsWith(' ') || p.EndsWith('.') || p.Any(c => c < 32 || "\\:*?\"<>|%".Contains(c)) || Regex.IsMatch(p, @"^(CON|PRN|AUX|NUL|COM[0-9]|LPT[0-9])(\.|$)", RegexOptions.IgnoreCase)))
            throw new InvalidDataException("Unsafe asset path: " + name);
        return name;
    }
    private static string Destination(string root, string name)
    {
        string destination = Path.GetFullPath(Path.Combine(root, SafeName(name)));
        if (!destination.StartsWith(root.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Asset escapes destination.");
        for (var dir = new DirectoryInfo(Path.GetDirectoryName(destination)!); dir != null; dir = dir.Parent)
            if (dir.Exists && dir.Attributes.HasFlag(FileAttributes.ReparsePoint)) throw new IOException("Destination contains a directory link: " + dir.FullName);
        if (File.Exists(destination) && File.GetAttributes(destination).HasFlag(FileAttributes.ReparsePoint)) throw new IOException("Destination is a file link.");
        return destination;
    }
    private static async Task<byte[]> Fetch(HttpClient http, Uri uri, CancellationToken ct)
    {
        for (int attempt = 0; ; attempt++)
        {
            try
            {
                using var response = await http.GetAsync(uri, ct);
                if ((int)response.StatusCode >= 500 || response.StatusCode == HttpStatusCode.TooManyRequests)
                { if (attempt < 2) { await Task.Delay(500 * (attempt + 1), ct); continue; } }
                response.EnsureSuccessStatusCode();
                return await response.Content.ReadAsByteArrayAsync(ct);
            }
            catch (HttpRequestException e) when (attempt < 2 && e.StatusCode == null) { await Task.Delay(500 * (attempt + 1), ct); }
            catch (TaskCanceledException) when (!ct.IsCancellationRequested && attempt < 2) { await Task.Delay(500 * (attempt + 1), ct); }
        }
    }
    private static bool IsText(string name) => name.EndsWith(".csv", StringComparison.OrdinalIgnoreCase) || name.EndsWith(".json", StringComparison.OrdinalIgnoreCase);
    private static void CheckHash(byte[] bytes, string expected)
    {
        if (!Convert.ToHexString(SHA1.HashData(bytes)).Equals(expected, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("SHA-1 mismatch; file was not written.");
    }
    private static byte[] DecodeText(byte[] bytes)
    {
        // Supercell's old LZMA header has five property bytes and a four-byte output size.
        if (bytes.Length < 9 || bytes[0] != 0x5d) return bytes;
        uint size = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(5, 4));
        uint dictionary = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(1, 4));
        if (size > 64 * 1024 * 1024 || dictionary > 64 * 1024 * 1024) throw new InvalidDataException("Oversized compressed text asset.");
        var decoder = new ClashRoyale.Compression.Lzma.Compress.LZMA.Decoder(); decoder.SetDecoderProperties(bytes[..5]);
        using var input = new MemoryStream(bytes, 9, bytes.Length - 9);
        using var output = new MemoryStream();
        decoder.Code(input, output, input.Length, size, null);
        if (output.Length != size) throw new InvalidDataException("Incomplete LZMA text asset.");
        return output.ToArray();
    }
    private static void Validate(byte[] bytes, string name, bool raw)
    {
        if (bytes.Length == 0) throw new InvalidDataException("Empty asset.");
        string prefix = Encoding.UTF8.GetString(bytes, 0, Math.Min(bytes.Length, 100)).TrimStart('\uFEFF', ' ', '\r', '\n');
        if (prefix.StartsWith("FAKE FILE", StringComparison.OrdinalIgnoreCase) || prefix.StartsWith("<!doctype html", StringComparison.OrdinalIgnoreCase) || prefix.StartsWith("<html", StringComparison.OrdinalIgnoreCase) || prefix.StartsWith("<?xml", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Placeholder/error document instead of an asset.");
        if (raw && IsText(name)) bytes = DecodeText(bytes);
        if (name.EndsWith(".json", StringComparison.OrdinalIgnoreCase)) { using var json = JsonDocument.Parse(bytes); }
        else if (name.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
        {
            string text = new UTF8Encoding(false, true).GetString(bytes);
            if (!text.Contains(',') || !text.Contains('\n') || text.Contains('\0')) throw new InvalidDataException("Invalid CSV asset.");
        }
    }
    private static async Task AtomicWrite(string path, byte[] bytes, CancellationToken ct)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        string temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try { await File.WriteAllBytesAsync(temp, bytes, ct); File.Move(temp, path, true); }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }
}
