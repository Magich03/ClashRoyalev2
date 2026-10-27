namespace ClashRoyale.Logic.Alliance
{
    using System;
    using System.Linq;
    using System.Runtime.CompilerServices;
    using System.Threading;
    using System.Threading.Tasks;
    using ClashRoyale.Database.Models;
    using ClashRoyale.Enums;
    using ClashRoyale.Files.Csv;
    using ClashRoyale.Logic.Alliance.Stream;
    using ClashRoyale.Logic.Battle;
    using ClashRoyale.Logic.Home.Spells;
    using ClashRoyale.Logic.Player;
    using ClashRoyale.Logic.Player.Enums;
    using ClashRoyale.Messages;
    using ClashRoyale.Messages.Server.Alliance;

    // Commands and replies never enter Clan.Messages or any shared broadcast path.
    public static class PrivateClanCommands
    {
        private static int ReplySequence;
        private static readonly ConditionalWeakTable<Device, SemaphoreSlim> Gates = new();
        public static bool IsCommand(string text) => text?.TrimStart().StartsWith(MessageFactory.Delimiter) == true;

        public static bool CheatsAllowed(Player player, string setting)
        {
            // Self-service cheats are enabled by default for this private server.
            if (setting == null || setting.Trim() == "*") return true;
            return setting.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Contains($"{player.HighId}:{player.LowId}", StringComparer.Ordinal);
        }

        public static async Task<bool> TryHandle(Device device, string text, CancellationToken cancellation)
        {
            if (!IsCommand(text)) return false;
            var gate = Gates.GetValue(device, _ => new SemaphoreSlim(1, 1));
            await gate.WaitAsync(cancellation);
            try
            {
                var player = device.GameMode.Player;
                bool ready = device.GameMode.State == HomeState.Home && !MatchBattleSession.Contains(device)
                    && !TrainingBattleSession.Contains(device) && !LadderMatchmaking.Contains(device);
                return await HandleAsync(player, text,
                    CheatsAllowed(player, Environment.GetEnvironmentVariable("CLASH_CHAT_CHEATS")), ready,
                    device.NetworkManager.SendMessage, async p => await PlayerDb.Save(p) != null);
            }
            finally { gate.Release(); }
        }

        // Per-connection delivery and storage are supplied explicitly, so replies cannot fan out to other members.
        public static async Task<bool> HandleAsync(Player player, string text, bool cheatsAllowed, bool homeReady,
            Action<Message> send, Func<Player, Task<bool>> save)
        {
            if (!IsCommand(text)) return false;
            text = text.Trim();
            if (text.Length > 256) { Reply(send, "Command too long. Maximum: 256 characters."); return true; }
            string[] parts = text.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
            var cheat = MessageFactory.CreateCheat(parts[0].Substring(1));
            if (cheat == null) { Reply(send, "Unknown command. Type /help."); return true; }
            var result = cheat.Prepare(player, parts.Skip(1).ToArray());
            var apply = result.Apply;
            if (apply != null)
            {
                if (!cheatsAllowed) { Reply(send, "Account cheats are disabled for your player ID."); return true; }
                if (!homeReady) { Reply(send, "Return home and leave matchmaking before changing your account."); return true; }
                var restore = Capture(player);
                try
                {
                    apply();
                    if (!await save(player)) throw new InvalidOperationException("Save failed");
                }
                catch
                {
                    restore();
                    Reply(send, "Could not save the change. Your account was left unchanged.");
                    return true;
                }
                // The native home cannot be replaced while its UI is active.
                // Display the saved result in the client's reload popup instead of clan chat.
                send(new ClashRoyale.Messages.Server.Account.LoginFailedMessage(Reason.Default)
                {
                    ErrorMessage = result.Response + "\n\nSaved successfully. Tap Try Again to refresh your account."
                });
                return true;
            }
            Reply(send, result.Response);
            return true;
        }

        private static Action Capture(Player player)
        {
            int level = player.ExpLevel, xp = player.ExpPoints, popup = player.Home.LastLevelUpPopup;
            int score = player.Score;
            var arena = player.Arena;
            var commodities = new ClashRoyale.Logic.Player.Slots.CommoditySlots();
            commodities.Load(player.CommoditySlots.Save());
            int gems = player.Diamonds, freeGems = player.FreeDiamonds;
            int selectedDeck = player.Home.SelectedDeck;
            var savedDecks = player.Home.SavedDecks.Select(d => (int[])d.Clone()).ToArray();
            var deck = new SpellDeck();
            for (int i = 0; i < 8; i++) deck.SetSpell(i, Copy(player.Home.SpellDeck[i]));
            var collection = new SpellCollection();
            foreach (var spell in player.Home.SpellCollection.GetSpells()) collection.AddSpell(Copy(spell));
            return () =>
            {
                player.ExpLevel = level; player.ExpPoints = xp; player.Home.LastLevelUpPopup = popup;
                player.Score = score; player.Arena = arena;
                player.CommoditySlots = commodities;
                player.Diamonds = gems; player.FreeDiamonds = freeGems;
                player.Home.SelectedDeck = selectedDeck; player.Home.SavedDecks = savedDecks;
                player.Home.SpellDeck = deck; player.Home.SpellCollection = collection;
            };
        }
        private static Spell Copy(Spell spell)
        {
            if (spell == null) return null;
            var clone = spell.Clone(); clone.ClientValue6 = spell.ClientValue6; return clone;
        }

        private static void Reply(Action<Message> send, string text)
        {
            foreach (string line in text.Split('\n'))
                send(new AllianceStreamEntryMessage(new ChatStreamEntry
                {
                    // Separate ID namespace from saved clan entries (ServerId, increasing low ID).
                    HighId = int.MaxValue, LowId = Interlocked.Increment(ref ReplySequence),
                    SenderHighId = 0, SenderLowId = 0, SenderName = "Server", SenderExpLevel = 1,
                    SenderRole = 1, Message = line
                }));
        }
    }
}
