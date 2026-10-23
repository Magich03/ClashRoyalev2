namespace ClashRoyale.Messages.Server.Alliance
{
    using ClashRoyale.Enums;
    using ClashRoyale.Extensions;

    public class AllianceJoinFailedMessage : Message
    {
        /// <summary>
        /// Gets the type of this message.
        /// </summary>
        public override short Type
        {
            get
            {
                return 25107;
            }
        }

        /// <summary>
        /// Gets the service node of this message.
        /// </summary>
        public override Node ServiceNode
        {
            get
            {
                return Node.Alliance;
            }
        }

        public int Reason;

        /// <summary>
        /// Initializes a new instance of the <see cref="AllianceJoinFailedMessage"/> class.
        /// </summary>
        public AllianceJoinFailedMessage()
        {
            // AllianceJoinFailedMessage.
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="AllianceJoinFailedMessage"/> class.
        /// </summary>
        public AllianceJoinFailedMessage(ByteStream Stream) : base(Stream)
        {
            // AllianceJoinFailedMessage.
        }

        /// <summary>
        /// Decodes this instance.
        /// </summary>
        public override void Decode()
        {
            this.Reason = this.Stream.ReadVInt();
        }

        /// <summary>
        /// Encodes this instance.
        /// </summary>
        public override void Encode()
        {
            this.Stream.WriteVInt(this.Reason);
        }
    }
}
