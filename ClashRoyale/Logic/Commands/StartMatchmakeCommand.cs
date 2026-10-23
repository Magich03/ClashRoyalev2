namespace ClashRoyale.Logic.Commands
{
    using ClashRoyale.Extensions;
    using ClashRoyale.Logic.Home;
    using ClashRoyale.Logic.Mode;
    using ClashRoyale.Logic.Player;
    using ClashRoyale.Messages.Server.Matchmaking;

    public class StartMatchmakeCommand : Command
    {
        /// <summary>
        /// Gets the type of this command.
        /// </summary>
        public override int Type
        {
            get
            {
                return 594;
            }
        }

        public int MatchType;
        private bool MatchmakingOption;
        private int DeckIndex = -1;
        public bool Is2v2 { get => this.MatchType == 1; set => this.MatchType = value ? 1 : 0; }

        /// <summary>
        /// Initializes a new instance of the <see cref="StartMatchmakeCommand"/> class.
        /// </summary>
        public StartMatchmakeCommand()
        {                       
            // StartMatchmakeCommand.
        }

        /// <summary>
        /// Decodes the specified stream.
        /// </summary>
        /// <param name="Stream">The stream.</param>
        public override void Decode(ByteStream Stream)
        {
            base.Decode(Stream);

            // Client 3.830 layout: base, VInt, bool, VInt.
            // Native 0xa2058 compares the FIRST VInt (+0x1c) to 1 for teams.
            this.MatchType = Stream.ReadVInt();
            this.MatchmakingOption = Stream.ReadBoolean();
            this.DeckIndex = Stream.ReadVInt();
        }

        /// <summary>
        /// Encodes the specified stream.
        /// </summary>
        /// <param name="Stream">The stream.</param>
        public override void Encode(ChecksumEncoder Stream)
        {
            base.Encode(Stream);

            Stream.WriteVInt(this.MatchType);
            Stream.WriteBoolean(this.MatchmakingOption);
            Stream.WriteVInt(this.DeckIndex);
        }

        /// <summary>
        /// Executes this instance.
        /// </summary>
        public override byte Execute(GameMode GameMode)
        {
            Home Home       = GameMode.Home;
            Player Player   = GameMode.Player;

            if (Home != null)
            {
                if (Player != null)
                {
                    if (Player.Arena.TrainingCamp)
                    {
                        return 3;
                    }

                    if (!GameMode.Listener.IsAndroid)
                    {
                        GameMode.Listener.SendMessage(new MatchmakeFailedMessage());
                    }
                    else
                    {
                        GameMode.Listener.Matchmaking(this.Is2v2);
                    }
                    
                    return 0;
                }

                return 2;
            }

            return 1;
        }
    }
}