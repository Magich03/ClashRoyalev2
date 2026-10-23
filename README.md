# ClashRoyale - [![License](https://i.creativecommons.org/l/by-nc-nd/4.0/80x15.png)](https://creativecommons.org/licenses/by-nc-nd/4.0/)

## ClashRoyale - Project
***ClashRoyale*** is a _lightweight_ Clash Royale game server.
It has been **written by Berkan** from **GameDef**.  
The goal of this emulator is to ensure a clean, easy-to-understand code environment.
ClashRoyale uses dedicated threads and async operators. 
This fork uses JSON file storage for the current server.


## Current solution

The .NET 8 solution contains the server, its shared game/protocol library, `ClashRoyale.Patcher`, and `Downloader`.
The obsolete C# clients, proxy, CSV editor, and conversion utilities have been removed.
Desktop texture rendering and local sound playback are also removed from server startup.
Game assets, the patcher, Frida/APK tools, native dumps, and existing account data are retained.

Build from the repository root:

```powershell
dotnet build ClashRoyale.sln -c Release
```

Build and publish all runnable tools with `build.cmd` (or `./build.ps1`).
The default is Release; use `build.cmd -Configuration Debug` for Debug.
Outputs are separated under `artifacts/Release/`:

- `server/ClashRoyale.Server.exe`: server, runnable Patcher, shared library, dependencies, Gamefiles, Patchs/VERSION and Secrets/pepper.key.
- `patcher/ClashRoyale.Patcher.exe`: runnable asset patcher with its Gamefiles and version file.
- `downloader/Downloader.exe`: standalone downloader with the official fingerprint.

Ordinary `dotnet build` outputs use each project's `bin/Release/net8.0/`.
Generated folders and old `Builds` trees are excluded from source/content discovery.
The shared library's assets are copied once per application that needs them; the
server output also includes the runnable Patcher and its runtime files, sharing the same Gamefiles and dependencies.
Server and Patcher resolve their data relative to their executable, including
when launched from another working directory. .NET 8 or a compatible newer runtime is required.

Stop a deployment before publishing over its running files. Preserve its `Data`
directory and `Secrets/pepper.key` when upgrading. Do not deploy another server's
key over an existing deployment. Old `Builds` deployments are not automatically deleted.

2v2, challenges and normal-map special events choose one of twelve standard
arenas per match, shared by all players. Touchdown retains its required map.
Ladder/training arenas, saved player arenas and reward eligibility are unchanged.

## About us
**GameDef** has been around since 2017, offering the latest discussion on games from Supercell.  
We are working hard to not only bring you the best servers but become one of the best communities.

## Disclaimer
**GameDef**'s resources and programs are protected under the CC license and our policies available on our website.
**GameDef**'s resources and programs are created by the (**GameDef**) team.  
**GameDef** is **NOT** affiliated with '_Supercell, Oy_'.  
**GameDef** does **NOT** own '_Clash of Clans_', '_Boom Beach_', '_Clash Royale_', and/or '_Hay Day_'.

## Licence
This work is licensed under the Creative Commons Attribution-NonCommercial-NoDerivs 4.0 Unported License.  
To view a copy of this license, visit https://creativecommons.org/licenses/by-nc-nd/4.0/ or send a letter to Creative Commons, PO Box 1866, Mountain View, CA 94042, USA.
