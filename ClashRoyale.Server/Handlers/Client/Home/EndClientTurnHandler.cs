namespace ClashRoyale.Handlers.Client.Home
{
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    using ClashRoyale.Exceptions;
    using ClashRoyale.Logic;
    using ClashRoyale.Logic.Alliance;
    using ClashRoyale.Handlers.Client.Alliance;
    using ClashRoyale.Logic.Collections;
    using ClashRoyale.Logic.Commands;
    using ClashRoyale.Logic.Player;
    using ClashRoyale.Messages;
    using ClashRoyale.Messages.Client.Home;

    public static class EndClientTurnHandler
    {
        /// <summary>
        /// Handles the specified <see cref="Message"/>.
        /// </summary>
        /// <param name="Device">The device.</param>
        /// <param name="Message">The message.</param>
        /// <param name="Cancellation">The cancellation.</param>
        public static async Task Handle(Device Device, Message Message, CancellationToken Cancellation)
        {
            var EndClientTurnMessage = (EndClientTurnMessage) Message;

            if (EndClientTurnMessage == null)
            {
                throw new LogicException(typeof(EndClientTurnHandler), nameof(EndClientTurnMessage) + " == null at Handle(Device, Message, CancellationToken).");
            }

            if (EndClientTurnMessage.Commands != null)
            {
                EndClientTurnMessage.Commands.ForEach(Command =>
                {
                    if (Command.ExecuteTick <= EndClientTurnMessage.Tick)
                    {
                        Device.GameMode.CommandManager.AddCommand(Command);
                    }
                });
            }

            if (EndClientTurnMessage.Commands != null)
            {
                foreach (SendAllianceMailCommand Mail in EndClientTurnMessage.Commands.OfType<SendAllianceMailCommand>())
                {
                    await ClanHelper.SendMail(Device.GameMode.Player, Mail.Title, Mail.Message);
                }
            }

            bool hasRewardClaim = Device.GameMode.CommandManager.Queue.Any(c => c.Type == 393);
            for (int I = Device.GameMode.Time; I < EndClientTurnMessage.Tick; I++)
            {
                Device.GameMode.UpdateOneTick();
            }

            while (Device.GameMode.CommandManager.CompletedCardRequests.TryDequeue(out var request))
                if (!request.Handled) { request.Handled=true; await ClanDonationHandler.Request(Device,request.Card); }

            while (Device.GameMode.CommandManager.CompletedFriendlyChallenges.TryDequeue(out var challenge))
                if (!challenge.Handled) { challenge.Handled=true; await ClanFriendlyBattleHandler.Create(Device,challenge); }

            Player Player = Device.GameMode.Player;
            // Persist the consumed chest and its rewards together after the server command is acknowledged.
            if (hasRewardClaim || EndClientTurnMessage.Commands?.Any(c => c is ChallengeCommand || c is SetHomeFlagCommand || c is ClaimSeasonRewardCommand || c is DraftChestChooseCommand || c is ClaimQuestRewardCommand || c is ClaimMainQuestRewardCommand || c is RequestNewQuestCommand || c is QuestsSeenCommand) == true)
                await ClashRoyale.Logic.Collections.Players.Save(Player);

            if (EndClientTurnMessage.Commands?.OfType<ChallengeCommand>().Any(c => c.Result != 0 && (c.Type == 541 || c.Type == 504)) == true)
            {
                ClashRoyale.Logic.Battle.LadderMatchmaking.Cancel(Device, false);
                Device.NetworkManager.SendMessage(new ClashRoyale.Messages.Server.Home.OwnHomeDataMessage(Player, Player.Home, ClashRoyale.Extensions.Utils.TimeUtil.Timestamp));
            }
            if (Player.IsInAlliance)
            {
                Clan Clan = await Clans.Get(Player.ClanHighId, Player.ClanLowId);

                if (Clan != null)
                {
                    if (Clan.Members.TryGetValue(Player.PlayerId, out var Member))
                    {
                        Member.SetPlayer(Player);
                    }
                }
                else
                {
                    Logging.Error(typeof(EndClientTurnHandler), "Clan == null at Handle(Device, Message, CancellationToken).");
                }
            }

            /* if (Device.GameMode.State == HomeState.Home)
            {
                if (EndClientTurnMessage.Checksum != Device.GameMode.Checksum)
                {
                    Logging.Error(this.GetType(), "Player is out of sync (S: " + Device.GameMode.Checksum + ", C: " + EndClientTurnMessage.Checksum + ").");
                    Device.NetworkManager.SendMessage(new OutOfSyncMessage(Device, EndClientTurnMessage.Checksum, Device.GameMode.Checksum));
                }
            } */
        }
    }
}
