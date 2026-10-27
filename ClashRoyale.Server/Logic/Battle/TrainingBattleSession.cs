namespace ClashRoyale.Logic.Battle
{
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Threading;

    using ClashRoyale.Logic.Commands;
    using ClashRoyale.Messages.Server.Sector;

    /// <summary>
    /// Runs a training battle for one player: the client simulates the battle itself (lockstep), the server only
    /// advances it with a SectorHeartbeatMessage every turn (10 ticks of 50 ms) carrying the commands to execute.
    /// </summary>
    public class TrainingBattleSession
    {
        private const int TurnMilliseconds = 500;

        public static int ActiveCount => Sessions.Count;
        public static bool Contains(Device device) => Sessions.ContainsKey(device);

        private static readonly ConcurrentDictionary<Device, TrainingBattleSession> Sessions = new ConcurrentDictionary<Device, TrainingBattleSession>();

        private readonly Device Device;
        private readonly List<Command> PendingCommands = new List<Command>();
        private readonly object Locker = new object();

        private Timer Timer;
        private int Turn;

        private TrainingBattleSession(Device Device)
        {
            this.Device = Device;
        }

        /// <summary>
        /// Starts the heartbeat of a new training battle for the device, replacing a running one.
        /// </summary>
        public static void Start(Device Device)
        {
            TrainingBattleSession.Stop(Device);

            TrainingBattleSession Session = new TrainingBattleSession(Device);

            if (TrainingBattleSession.Sessions.TryAdd(Device, Session))
            {
                Session.Timer = new Timer(_ => Session.SendHeartbeat(), null, TrainingBattleSession.TurnMilliseconds, TrainingBattleSession.TurnMilliseconds);
            }
        }

        /// <summary>
        /// Stops the training battle of the device, if any.
        /// </summary>
        public static void Stop(Device Device)
        {
            if (TrainingBattleSession.Sessions.TryRemove(Device, out TrainingBattleSession Session))
            {
                Session.Timer?.Dispose();
                Logging.Info(typeof(TrainingBattleSession), "Training battle stopped after " + Session.Turn + " turns.");
            }
        }

        /// <summary>
        /// Queues a command sent by the client (SectorCommandMessage); it is executed with the next heartbeat.
        /// </summary>
        public static bool AddCommand(Device Device, Command Command)
        {
            if (TrainingBattleSession.Sessions.TryGetValue(Device, out TrainingBattleSession Session))
            {
                lock (Session.Locker)
                {
                    Session.PendingCommands.Add(Command);
                }

                return true;
            }

            return false;
        }

        private void SendHeartbeat()
        {
            if (this.Device.Token == null || !this.Device.Token.IsConnected)
            {
                TrainingBattleSession.Stop(this.Device);
                return;
            }

            Command[] Commands;

            lock (this.Locker)
            {
                Commands = this.PendingCommands.ToArray();
                this.PendingCommands.Clear();
            }

            // The client sends commands with execute tick -1: the server schedules them on the first tick of the turn
            // this heartbeat carries (a turn is 10 ticks).
            foreach (Command Command in Commands)
            {
                Command.ExecuteTick = this.Turn * 10;
                Logging.Info(typeof(TrainingBattleSession), "Turn " + this.Turn + ": " + Command);
            }

            this.Device.NetworkManager.SendMessage(new SectorHearbeatMessage(this.Turn++, 0, Commands));
        }
    }
}
