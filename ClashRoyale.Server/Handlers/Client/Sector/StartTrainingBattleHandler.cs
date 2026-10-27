namespace ClashRoyale.Handlers.Client.Sector
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    using ClashRoyale.Enums;
    using ClashRoyale.Exceptions;
    using ClashRoyale.Logic;
    using ClashRoyale.Logic.Battle;
    using ClashRoyale.Messages;
    using ClashRoyale.Messages.Client.Attack;
    using ClashRoyale.Messages.Server.Sector;

    public static class StartTrainingBattleHandler
    {
        /// <summary>
        /// Handles the specified <see cref="Message"/>.
        /// </summary>
        /// <param name="Device">The device.</param>
        /// <param name="Message">The message.</param>
        /// <param name="Cancellation">The cancellation.</param>
        public static async Task Handle(Device Device, Message Message, CancellationToken Cancellation)
        {
            var StartTrainingBattleMessage = (StartTrainingBattleMessage) Message;

            if (StartTrainingBattleMessage == null)
            {
                throw new LogicException(typeof(StartTrainingBattleHandler), nameof(StartTrainingBattleMessage) + " == null at Handle(Device, Message, CancellationToken).");
            }

            if (StartTrainingBattleMessage.NpcData == null)
            {
                Logging.Warning(typeof(StartTrainingBattleHandler), "NpcData == null at Handle(Device, Message, CancellationToken).");
                return;
            }

            if (ClashRoyale.Handlers.Client.Alliance.ClanFriendlyBattleHandler.Contains(Device) || MatchBattleSession.Contains(Device)) return;
            LadderMatchmaking.Cancel(Device, false);
            MatchBattleSession.Stop(Device);
            // StartMissionMessage in client 3.830: answer with the battle state of a training battle against the NPC.
            byte[] State = BattleStateEncoder.EncodeTrainingBattle(Device.GameMode.Player, StartTrainingBattleMessage.NpcData, Environment.TickCount & 0x7FFFFFFF);

            Logging.Info(typeof(StartTrainingBattleHandler), "Training battle against " + StartTrainingBattleMessage.NpcData.Name + ", state " + State.Length + " bytes: " + BitConverter.ToString(State));

            Device.NetworkManager.SendMessage(new SectorStateMessage(State));

            TrainingBattleSession.Start(Device);
        }
    }
}