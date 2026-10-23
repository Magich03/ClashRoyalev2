namespace ClashRoyale.Messages.Client.Alliance
{
    using ClashRoyale.Enums;
    using ClashRoyale.Extensions;

    public class AskForAllianceStreamMessage : Message
    {
        /// <summary>
        /// Gets the type of this message.
        /// </summary>
        public override short Type
        {
            get
            {
                return 11802;
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

        /// <summary>
        /// Initializes a new instance of the <see cref="AskForAllianceStreamMessage"/> class.
        /// </summary>
        public AskForAllianceStreamMessage()
        {
            // AskForAllianceStreamMessage.
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="AskForAllianceStreamMessage"/> class.
        /// </summary>
        public AskForAllianceStreamMessage(ByteStream Stream) : base(Stream)
        {
            // AskForAllianceStreamMessage.
        }

        /// <summary>
        /// Decodes this instance.
        /// </summary>
        public override void Decode()
        {
            // Empty.
        }

        /// <summary>
        /// Encodes this instance.
        /// </summary>
        public override void Encode()
        {
            // Empty.
        }
    }
}
