# Asset downloader

Downloads the files listed in an old Clash Royale `fingerprint.json`. The default CDN is `https://game-assets.clashroyaleapp.com/`; requests use `<host>/<version-sha>/<relative-file>`.

You can also double-click `Downloader.exe` in `bin/Release/net8.0`. The build includes `fingerprint.json` beside it. The downloader finds that manifest automatically, creates `Downloads/Gamefiles` under its working directory, and keeps the window open until you press Enter after completion or an error.

From the repository root:

```powershell
dotnet run --project Downloader -c Release -- --fingerprint fingerprint.json --output Downloads/Gamefiles
```

The output folder and all selected asset subfolders (including nested folders and `level/`) are created automatically before downloads begin. You do not need to create them manually. The default output is a separate download folder. It does not replace the running server's game files or player data.

Download only the official starting-home file:

```powershell
dotnet run --project Downloader -c Release -- --only level/starting_home.json --overwrite
```

`level/starting_home.json` is downloaded from the same official CDN and fingerprint SHA as the other assets, even though it is absent from the manifest's file list. There is no GitHub fallback. The original bytes and schema are preserved; this is not automatically converted into the C# server's custom home format. Existing home files are compared with the official response; differing copies require `--overwrite`.

Verified for SHA `67bbb9df2eabf5c5fa4a8fa480df2a46bc861d87`: the official home is 447 bytes with six starter cards. The official fingerprint labels this asset set `3.830.1`; the local fingerprint labels it `3.830.6`, but the SHA and all 992 asset hashes match. `level/starting_home_max.json` returned HTTP 403 and is not included as an official download.


## Behavior

- Verifies manifest-listed CDN files against their SHA-1 before writing them. The extra starting-home file has no hash in that manifest; its source is the same SHA-scoped HTTPS URL and its JSON structure is validated.
- Decompresses the old Supercell LZMA CSV/JSON format for use by the C# server. Use `--raw` to retain CDN bytes for a client content patch. Manifest hashes describe the original compressed bytes, not the decoded CSVs.
- Retries temporary connection/server failures, supports Ctrl+C and rerunning to resume, and writes files atomically.
- Records each source and outcome in `download-report.json`; returns a nonzero exit code for failures. It never fabricates missing files.
- Preserves valid existing server CSV/JSON files, including intentional customizations. Invalid existing placeholders are reported; use `--overwrite` to replace them with verified downloads.
- Rejects paths that escape the selected directory. A filtered download does not write a misleading complete fingerprint.

Options: `--fingerprint PATH|URL`, `--output DIRECTORY`, `--host URL`, `--only RELATIVE_PATH` (repeatable), `--extra RELATIVE_PATH` (additional CDN paths), `--jobs 1..16`, `--overwrite`, `--raw`, `--no-home`, `--help`.

Old files can disappear from the upstream CDN. Such failures remain visible in the report; an alternate archive can be selected with `--host`.

Build the four-project solution with `dotnet build ClashRoyale.sln -c Release`. The downloader executable is generated in `Downloader/bin/Release/net8.0/` and runs on .NET 8 or a newer installed major runtime.
