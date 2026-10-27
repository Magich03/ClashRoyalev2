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
Game assets, the patcher, reference dumps, and existing account data are retained.

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

## Private clan commands


Type `/help` in clan chat to list commands. Slash messages are intercepted before clan storage or broadcast. Replies from `Server` go only to the requesting connection and are not saved in clan history. Successful account changes are saved and shown in the Login failed popup instead of clan chat. Tap Try Again to reload the updated account.

| Command | Effect on your account |
| --- | --- |
| `/max` | Unlock all cards, set valid maximum card/king levels, fill gold, set 1,000,000 gems and raise trophies to at least the highest enabled league threshold (6,400). |
| `/min` | Reset to Arena 1, zero trophies/best score, king level 1 and only the starting eight cards at base levels; clear spare card counts, gold and gems. |
| `/gold <amount>` | Set gold from 0 through the game's gold cap. |
| `/arena <number>` | Set current trophies to that arena or league threshold, preserving higher best trophies. Use `/arena` to list valid numbers. |
| `/gems <amount>` | Set gems from 0 through 1,000,000. |
| `/status` | Show server uptime, online players, queues, battles, memory/runtime and account status. |
| `/info` | Show server information and preset behavior. |
| `/help` | List registered commands. |

Both presets retain clan and battle history. `/min` resets all saved decks to the starter deck and removes other cards. `/max` retains your deck and updates trophies, best score and arena without lowering an existing higher score. Account-changing commands require the player to be home and outside matchmaking/battles.

Self-service account cheats are enabled for everyone by default. Set the server environment variable `CLASH_CHAT_CHEATS` to `off` to disable them, or to comma-separated player IDs such as `0:1,0:2` to restrict them. `*` enables all accounts. Help, info and status remain available.

The existing `/` delimiter and command registry are in `ClashRoyale/Messages/MessageFactory.cs`. Each cheat has its own class under `ClashRoyale/Logic/Cheats/Commands/`; add new commands there and register them in the factory. `PrivateClanCommands` handles private delivery, validation, saving and refreshes. No client patch is required.
The league introduction acknowledgement and other home flags are saved per account. No client patch is required.

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

## Android client releases

Signed APK downloads are published under GitHub Releases, outside Git history.
Two variants use the same package name (`com.greedycell.astralroyale`):

- Pepper: native Pepper encryption enabled, with the server public key.
- RC4: Pepper disabled, using the legacy RC4 connection.

Both LAN builds connect to `192.168.1.7:9339`. They replace one another when
installed; they do not install as two separate apps. Neither bundled script
contains diagnostic logging or memory-dump routines. `Dumps/` retains the
original protocol reference files for server development.

Keep the server private key outside Git and outside release downloads.
