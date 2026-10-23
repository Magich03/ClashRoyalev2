namespace ClashRoyale.Handlers.Client.Alliance
{
    using System.Linq;
    using System.Threading.Tasks;

    using ClashRoyale.Logic;
    using ClashRoyale.Logic.Alliance;
    using ClashRoyale.Logic.Alliance.Entries;
    using ClashRoyale.Logic.Alliance.Stream;
    using ClashRoyale.Logic.Collections;
    using ClashRoyale.Logic.Commands.Server;
    using ClashRoyale.Logic.Player;
    using ClashRoyale.Messages.Server.Alliance;
    using ClashRoyale.Messages.Server.Avatar;

    /// <summary>
    /// Shared clan operations used by the alliance handlers.
    /// Roles: 1 = member, 2 = leader, 3 = elder, 4 = co-leader.
    /// </summary>
    internal static class ClanHelper
    {
        internal const int RoleMember   = 1;
        internal const int RoleLeader   = 2;
        internal const int RoleElder    = 3;
        internal const int RoleCoLeader = 4;

        internal const int MaxMembers   = 50;

        /// <summary>
        /// Gets the rank of a role, higher is more powerful.
        /// </summary>
        internal static int Rank(int Role)
        {
            switch (Role)
            {
                case RoleLeader:
                    return 4;
                case RoleCoLeader:
                    return 3;
                case RoleElder:
                    return 2;
                case RoleMember:
                    return 1;
            }

            return 0;
        }

        /// <summary>
        /// Gets the clan of the specified player, or null when the player is not (or no longer) in a clan.
        /// </summary>
        internal static async Task<Clan> GetClan(Player Player)
        {
            if (!Player.IsInAlliance)
            {
                return null;
            }

            Clan Clan = await Clans.Get(Player.ClanHighId, Player.ClanLowId);

            if (Clan != null && Clan.Members.ContainsKey(Player.PlayerId))
            {
                return Clan;
            }

            return null;
        }

        /// <summary>
        /// Clears the clan fields of a player that is not connected.
        /// </summary>
        internal static void ClearOfflineAlliance(Player Player)
        {
            Player.SetAllianceId(0, 0);
            Player.SetAllianceRole(0);
            Player.SetAllianceBadge(null);
            Player.SetAllianceName(string.Empty);
        }

        /// <summary>
        /// Adds a stream entry to the clan and pushes it to every online member.
        /// </summary>
        internal static void AddStreamEntry(Clan Clan, StreamEntry Entry)
        {
            Clan.Messages.AddEntry(Entry);
            Clan.Members.Broadcast(() => new AllianceStreamEntryMessage(Entry));
        }

        /// <summary>
        /// Sends the clan data to every online member.
        /// </summary>
        internal static void BroadcastData(Clan Clan)
        {
            Clan.Members.Broadcast(() => new AllianceDataMessage(Clan.GetFullEntry()));
        }

        /// <summary>
        /// Finishes a join (or a creation): gives the client the join command, the clan data and the stream.
        /// The player must already be in <see cref="Clan.Members"/>.
        /// </summary>
        internal static async Task CompleteJoin(Device Device, Clan Clan, bool Creation)
        {
            Player Player = Device.GameMode.Player;

            Clan.UpdateMember(Player);
            Clan.Refresh();

            Device.GameMode.CommandManager.WaitJoinAllianceTurn = true;
            Device.GameMode.CommandManager.AddAvailableServerCommand(new JoinAllianceCommand(Clan.HighId, Clan.LowId, Clan.HeaderEntry.Name, Clan.HeaderEntry.Badge, Creation));

            if (!Creation)
            {
                AllianceEventStreamEntry Event = new AllianceEventStreamEntry(Player, Player);
                Event.SetJoinEvent();
                ClanHelper.AddStreamEntry(Clan, Event);
                ClanHelper.BroadcastData(Clan);
            }

            Device.NetworkManager.SendMessage(new AllianceDataMessage(Clan.GetFullEntry()));
            Device.NetworkManager.SendMessage(new AllianceStreamMessage(Clan.Messages.ToArray()));

            await Clan.Members.TryAddOnlineMember(Player);
            await Clans.Save(Clan);
        }

        /// <summary>
        /// Removes a member from the clan, hands over the leadership if needed and deletes the clan when it is empty.
        /// </summary>
        internal static async Task RemoveMember(Clan Clan, Player Member)
        {
            bool WasLeader = Clan.Members.TryGetValue(Member.PlayerId, out AllianceMemberEntry Entry) && Entry.Role == ClanHelper.RoleLeader;

            Clan.Members.TryRemove(Member);

            if (Clan.Members.GetConnected(Member.PlayerId) != null)
            {
                await Clan.Members.TryRemoveOnlineMember(Member);
            }

            if (Clan.Members.Count == 0)
            {
                await Clans.Delete(Clan);
                return;
            }

            if (WasLeader)
            {
                AllianceMemberEntry Successor = null;

                foreach (AllianceMemberEntry Candidate in Clan.Members.Values)
                {
                    if (Successor == null || ClanHelper.Rank(Candidate.Role) > ClanHelper.Rank(Successor.Role) || ClanHelper.Rank(Candidate.Role) == ClanHelper.Rank(Successor.Role) && Candidate.Score > Successor.Score)
                    {
                        Successor = Candidate;
                    }
                }

                if (Successor != null)
                {
                    await ClanHelper.SetRole(Clan, Successor, ClanHelper.RoleLeader);
                }
            }

            Clan.Refresh();
        }

        /// <summary>
        /// Delivers a clan mail to the inbox of every member (leader and co-leaders only).
        /// </summary>
        internal static async Task SendMail(Player Sender, string Title, string Message)
        {
            Clan Clan = await ClanHelper.GetClan(Sender);

            if (Clan == null || string.IsNullOrWhiteSpace(Message) && string.IsNullOrWhiteSpace(Title))
            {
                return;
            }

            if (!Clan.Members.TryGetValue(Sender.PlayerId, out AllianceMemberEntry SenderEntry) || ClanHelper.Rank(SenderEntry.Role) < ClanHelper.Rank(ClanHelper.RoleCoLeader))
            {
                Logging.Warning(typeof(ClanHelper), "SendMail() - " + Sender.Name + " is not allowed to send clan mail.");
                return;
            }

            Logging.Info(typeof(ClanHelper), "Clan mail from " + Sender.Name + ": title=\"" + Title + "\" message=\"" + Message + "\"");

            foreach (AllianceMemberEntry Entry in Clan.Members.Values.ToArray())
            {
                AllianceMailEntry Mail = new AllianceMailEntry
                {
                    SenderHighId    = Sender.HighId,
                    SenderLowId     = Sender.LowId,
                    SenderName      = Sender.Name,
                    SenderLevel     = Sender.ExpLevel,
                    Title           = Title ?? string.Empty,
                    Message         = Message ?? string.Empty,
                    ClanHighId      = Clan.HighId,
                    ClanLowId       = Clan.LowId,
                    ClanName        = Clan.HeaderEntry.Name,
                    ClanBadge       = Clan.HeaderEntry.Badge
                };

                Player Online = Entry.PlayerId == Sender.PlayerId ? Sender : Clan.Members.GetConnected(Entry.PlayerId);

                if (Online != null)
                {
                    Online.AddMail(Mail);

                    // AvatarStream updates the mail model. InboxList's client handler (0x7f4f8)
                    // rebuilds the open inbox UI, so it must follow the updated mail model.
                    Online.GameMode?.Listener?.SendMessage(new AvatarStreamMessage(Online.Mails));
                    Online.GameMode?.Listener?.SendMessage(new ClashRoyale.Messages.Server.Account.InboxListMessage());
                    await Players.Save(Online);
                }
                else
                {
                    Player Offline = await Players.Get(Entry.HighId, Entry.LowId, false);

                    if (Offline != null)
                    {
                        Offline.AddMail(Mail);
                        await Players.Save(Offline);
                    }
                }
            }
        }

        /// <summary>
        /// Changes the role of a member and updates its avatar (through a server command when online).
        /// </summary>
        internal static async Task SetRole(Clan Clan, AllianceMemberEntry Entry, int Role)
        {
            Entry.SetRole(Role);

            Player Online = Clan.Members.GetConnected(Entry.PlayerId);

            if (Online != null && Online.GameMode != null)
            {
                Online.GameMode.CommandManager.AddAvailableServerCommand(new ChangeAllianceRoleCommand(Clan.AllianceId, Role));
            }
            else
            {
                Player Offline = await Players.Get(Entry.HighId, Entry.LowId, false);

                if (Offline != null)
                {
                    Offline.SetAllianceRole(Role);
                    await Players.Save(Offline);
                }
            }
        }
    }
}
