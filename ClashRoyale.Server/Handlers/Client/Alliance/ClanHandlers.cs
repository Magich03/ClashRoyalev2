namespace ClashRoyale.Handlers.Client.Alliance
{
    using System;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    using ClashRoyale.Logic;
    using ClashRoyale.Logic.Alliance;
    using ClashRoyale.Logic.Alliance.Entries;
    using ClashRoyale.Logic.Alliance.Stream;
    using ClashRoyale.Logic.Collections;
    using ClashRoyale.Logic.Commands.Server;
    using ClashRoyale.Logic.Player;
    using ClashRoyale.Messages;
    using ClashRoyale.Messages.Client.Alliance;
    using ClashRoyale.Messages.Server.Alliance;

    public static class AskForJoinableAlliancesListHandler
    {
        public static async Task Handle(Device Device, Message Message, CancellationToken Cancellation)
        {
            Player Player = Device.GameMode.Player;

            AllianceHeaderEntry[] Entries = Clans.GetAll()
                .Where(Clan => Clan.AllianceId != Player.AllianceId && Clan.Members.Count > 0 && Clan.Members.Count < ClanHelper.MaxMembers && Clan.HeaderEntry.Type != 3)
                .Select(Clan =>
                {
                    Clan.Refresh();
                    return Clan.HeaderEntry;
                })
                .OrderByDescending(Header => Header.Score)
                .Take(40)
                .ToArray();

            Device.NetworkManager.SendMessage(new JoinableAllianceListMessage(Entries));

            await Task.CompletedTask;
        }
    }

    public static class SearchAlliancesHandler
    {
        public static async Task Handle(Device Device, Message Message, CancellationToken Cancellation)
        {
            var Search = (SearchAllianceMessage) Message;
            string Name = Search.Name?.Trim() ?? string.Empty;

            AllianceHeaderEntry[] Entries = Clans.GetAll()
                .Where(Clan =>
                {
                    Clan.Refresh();

                    if (Clan.Members.Count == 0)
                    {
                        return false;
                    }

                    if (Name.Length > 0 && (Clan.HeaderEntry.Name == null || Clan.HeaderEntry.Name.IndexOf(Name, StringComparison.OrdinalIgnoreCase) < 0) && !string.Equals(Clan.ToString(), Name))
                    {
                        return false;
                    }

                    if (Search.Location != null && Clan.HeaderEntry.Region != Search.Location)
                    {
                        return false;
                    }

                    if (Search.MinimumPlayers > 0 && Clan.Members.Count < Search.MinimumPlayers)
                    {
                        return false;
                    }

                    if (Search.MaximumPlayers > 0 && Clan.Members.Count > Search.MaximumPlayers)
                    {
                        return false;
                    }

                    if (Search.MinimumScore > 0 && Clan.HeaderEntry.Score < Search.MinimumScore)
                    {
                        return false;
                    }

                    return !Search.OpenOnly || Clan.HeaderEntry.Type == 1;
                })
                .Select(Clan => Clan.HeaderEntry)
                .OrderByDescending(Header => Header.Score)
                .Take(64)
                .ToArray();

            Device.NetworkManager.SendMessage(new SearchClansDataMessage(Search.Name ?? string.Empty, Entries));

            await Task.CompletedTask;
        }
    }

    public static class JoinAllianceHandler
    {
        public static async Task Handle(Device Device, Message Message, CancellationToken Cancellation)
        {
            var JoinAllianceMessage = (JoinAllianceMessage) Message;
            Player Player = Device.GameMode.Player;

            if (Player.IsInAlliance)
            {
                Logging.Warning(typeof(JoinAllianceHandler), "Player is already in a clan.");
                return;
            }

            Clan Clan = await Clans.Get(JoinAllianceMessage.HighId, JoinAllianceMessage.LowId);

            if (Clan == null || Clan.Members.Count == 0)
            {
                Device.NetworkManager.SendMessage(new AllianceJoinFailedMessage { Reason = 1 });
                return;
            }

            if (Clan.Members.Count >= ClanHelper.MaxMembers)
            {
                Device.NetworkManager.SendMessage(new AllianceJoinFailedMessage { Reason = 2 });
                return;
            }

            if (Clan.HeaderEntry.Type != 1)
            {
                Device.NetworkManager.SendMessage(new AllianceJoinFailedMessage { Reason = 3 });
                return;
            }

            if (Player.Score < Clan.HeaderEntry.RequiredScore)
            {
                Device.NetworkManager.SendMessage(new AllianceJoinFailedMessage { Reason = 4 });
                return;
            }

            if (Clan.Members.TryAdd(Player))
            {
                await ClanHelper.CompleteJoin(Device, Clan, false);
            }
        }
    }

    public static class LeaveAllianceHandler
    {
        public static async Task Handle(Device Device, Message Message, CancellationToken Cancellation)
        {
            Player Player = Device.GameMode.Player;

            if (!Player.IsInAlliance)
            {
                return;
            }

            long AllianceId = Player.AllianceId;
            Clan Clan = await ClanHelper.GetClan(Player);

            if (Clan != null)
            {
                await ClanHelper.RemoveMember(Clan, Player);

                if (Clan.Members.Count > 0)
                {
                    AllianceEventStreamEntry Event = new AllianceEventStreamEntry(Player, Player);
                    Event.SetLeaveEvent();
                    ClanHelper.AddStreamEntry(Clan, Event);
                    ClanHelper.BroadcastData(Clan);

                    await Clans.Save(Clan);
                }
            }

            Device.GameMode.CommandManager.WaitLeaveAllianceTurn = true;
            Device.GameMode.CommandManager.AddAvailableServerCommand(new LeaveAllianceCommand(AllianceId, false));
        }
    }

    public static class ChatToAllianceStreamHandler
    {
        public static async Task Handle(Device Device, Message Message, CancellationToken Cancellation)
        {
            var ChatMessage = (ChatToAllianceStreamMessage) Message;
            Player Player = Device.GameMode.Player;
            string Text = ChatMessage.Message?.Trim();

            if (string.IsNullOrEmpty(Text))
            {
                return;
            }

            if (await PrivateClanCommands.TryHandle(Device, Text, Cancellation)) return;

            if (Text.Length > 256)
            {
                Text = Text.Substring(0, 256);
            }

            Clan Clan = await ClanHelper.GetClan(Player);

            if (Clan != null)
            {
                ClanHelper.AddStreamEntry(Clan, new ChatStreamEntry(Player, Text));
                await Clans.Save(Clan);
            }
        }
    }

    public static class AskForAllianceStreamHandler
    {
        public static async Task Handle(Device Device, Message Message, CancellationToken Cancellation)
        {
            Clan Clan = await ClanHelper.GetClan(Device.GameMode.Player);

            if (Clan != null)
            {
                Device.NetworkManager.SendMessage(new AllianceStreamMessage(Clan.Messages.ToArray()));
            }
        }
    }

    public static class ChangeAllianceMemberRoleHandler
    {
        public static async Task Handle(Device Device, Message Message, CancellationToken Cancellation)
        {
            var RoleMessage = (ChangeAllianceMemberRoleMessage) Message;
            Player Player = Device.GameMode.Player;
            Clan Clan = await ClanHelper.GetClan(Player);

            if (Clan == null || RoleMessage.MemberId == Player.PlayerId)
            {
                return;
            }

            if (!Clan.Members.TryGetValue(Player.PlayerId, out AllianceMemberEntry Self) || !Clan.Members.TryGetValue(RoleMessage.MemberId, out AllianceMemberEntry Target))
            {
                return;
            }

            int NewRole = RoleMessage.NewRole;
            int SelfRank = ClanHelper.Rank(Self.Role);

            if (ClanHelper.Rank(NewRole) == 0 || SelfRank <= ClanHelper.Rank(Target.Role) || SelfRank < ClanHelper.Rank(ClanHelper.RoleCoLeader))
            {
                Device.NetworkManager.SendMessage(new AllianceChangeFailedMessage { Reason = 1 });
                return;
            }

            if (ClanHelper.Rank(NewRole) >= SelfRank && !(Self.Role == ClanHelper.RoleLeader && NewRole == ClanHelper.RoleLeader))
            {
                Device.NetworkManager.SendMessage(new AllianceChangeFailedMessage { Reason = 1 });
                return;
            }

            bool Promotion = ClanHelper.Rank(NewRole) > ClanHelper.Rank(Target.Role);

            await ClanHelper.SetRole(Clan, Target, NewRole);

            if (NewRole == ClanHelper.RoleLeader)
            {
                // Handing over the leadership makes the old leader a co-leader.
                await ClanHelper.SetRole(Clan, Self, ClanHelper.RoleCoLeader);
            }

            Player TargetPlayer = Clan.Members.GetConnected(Target.PlayerId) ?? await Players.Get(Target.HighId, Target.LowId, false);

            if (TargetPlayer != null)
            {
                AllianceEventStreamEntry Event = new AllianceEventStreamEntry(Player, TargetPlayer);

                if (Promotion)
                {
                    Event.SetPromoteEvent();
                }
                else
                {
                    Event.SetDemoteEvent();
                }

                ClanHelper.AddStreamEntry(Clan, Event);
            }

            Device.NetworkManager.SendMessage(new ChangeAllianceMemberRoleOkMessage { MemberId = Target.PlayerId, NewRole = NewRole });
            ClanHelper.BroadcastData(Clan);

            await Clans.Save(Clan);
        }
    }

    public static class KickAllianceMemberHandler
    {
        public static async Task Handle(Device Device, Message Message, CancellationToken Cancellation)
        {
            var KickMessage = (KickAllianceMemberMessage) Message;
            Player Player = Device.GameMode.Player;
            Clan Clan = await ClanHelper.GetClan(Player);

            if (Clan == null || KickMessage.MemberId == Player.PlayerId)
            {
                return;
            }

            if (!Clan.Members.TryGetValue(Player.PlayerId, out AllianceMemberEntry Self) || !Clan.Members.TryGetValue(KickMessage.MemberId, out AllianceMemberEntry Target))
            {
                return;
            }

            if (ClanHelper.Rank(Self.Role) < ClanHelper.Rank(ClanHelper.RoleElder) || ClanHelper.Rank(Self.Role) <= ClanHelper.Rank(Target.Role))
            {
                Device.NetworkManager.SendMessage(new AllianceChangeFailedMessage { Reason = 1 });
                return;
            }

            Player Online = Clan.Members.GetConnected(Target.PlayerId);
            Player TargetPlayer = Online ?? await Players.Get(Target.HighId, Target.LowId, false);

            if (TargetPlayer == null)
            {
                Clan.Members.TryRemove(Target.PlayerId, out _);
            }
            else
            {
                await ClanHelper.RemoveMember(Clan, TargetPlayer);

                if (Online != null && Online.GameMode != null)
                {
                    Online.GameMode.CommandManager.AddAvailableServerCommand(new LeaveAllianceCommand(Clan.AllianceId, true));
                }
                else
                {
                    ClanHelper.ClearOfflineAlliance(TargetPlayer);
                    await Players.Save(TargetPlayer);
                }

                AllianceEventStreamEntry Event = new AllianceEventStreamEntry(Player, TargetPlayer);
                Event.SetKickEvent();
                ClanHelper.AddStreamEntry(Clan, Event);
            }

            Device.NetworkManager.SendMessage(new KickAllianceMemberOkMessage { MemberId = Target.PlayerId });
            ClanHelper.BroadcastData(Clan);

            await Clans.Save(Clan);
        }
    }

    public static class EditAllianceHandler
    {
        public static async Task Handle(Device Device, Message Message, CancellationToken Cancellation)
        {
            var EditMessage = (EditAllianceMessage) Message;
            Player Player = Device.GameMode.Player;
            Clan Clan = await ClanHelper.GetClan(Player);

            if (Clan == null || !Clan.Members.TryGetValue(Player.PlayerId, out AllianceMemberEntry Self))
            {
                return;
            }

            if (ClanHelper.Rank(Self.Role) < ClanHelper.Rank(ClanHelper.RoleCoLeader) || EditMessage.HiringType < 1 || EditMessage.HiringType > 3)
            {
                Device.NetworkManager.SendMessage(new AllianceChangeFailedMessage { Reason = 1 });
                return;
            }

            Clan.Description                = EditMessage.Description ?? string.Empty;
            Clan.HeaderEntry.Type           = EditMessage.HiringType;
            Clan.HeaderEntry.RequiredScore  = Math.Max(0, EditMessage.RequiredScore);

            if (EditMessage.Badge != null)
            {
                Clan.HeaderEntry.Badge = EditMessage.Badge;
            }

            if (EditMessage.Location != null)
            {
                Clan.HeaderEntry.Region = EditMessage.Location;
            }

            ClanHelper.BroadcastData(Clan);

            await Clans.Save(Clan);
        }
    }
}
