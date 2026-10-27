namespace ClashRoyale.Messages.Server.Attack
{
    using ClashRoyale.Enums;
    using ClashRoyale.Extensions;
    using ClashRoyale.Logic.Battle.Event;

    public class BattleEventMessage : Message
    {
        /// <summary>
        /// Gets the type of this message.
        /// </summary>
        public override short Type
        {
            get
            {
                return 29500;
            }
        }

        /// <summary>
        /// Gets the service node of this message.
        /// </summary>
        public override Node ServiceNode
        {
            get
            {
                return Node.Sector;
            }
        }

        public BattleEvent Event;

        /// <summary>
        /// Initializes a new instance of the <see cref="BattleEventMessage"/> class.
        /// </summary>
        public BattleEventMessage()
        {
            // BattleEventMessage.
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="BattleEventMessage"/> class.
        /// </summary>
        /// <param name="Stream">The stream.</param>
        public BattleEventMessage(ByteStream Stream) : base(Stream)
        {
            // BattleEventMessage.
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="BattleEventMessage"/> class.
        /// </summary>
        /// <param name="Event">The event.</param>
        public BattleEventMessage(BattleEvent Event)
        {
            this.Event = Event;
        }

        /// <summary>
        /// Decodes this instance.
        /// </summary>
        public override void Decode()
        {
            this.Event = new BattleEvent();
            this.Event.Decode(this.Stream);
        }

        /// <summary>
        /// Encodes this instance;
        /// </summary>
        public override void Encode()
        {
            this.Event.Encode(this.Stream);
        }
    }
}