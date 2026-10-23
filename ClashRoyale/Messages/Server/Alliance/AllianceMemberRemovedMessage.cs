namespace ClashRoyale.Messages.Server.Alliance
{
    using ClashRoyale.Enums;
    using ClashRoyale.Extensions;

    public class AllianceMemberRemovedMessage : Message
    {
        /// <summary>
        /// Gets the type of this message.
        /// </summary>
        public override short Type
        {
            get
            {
                return 21753;
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

        public long MemberId;

        /// <summary>
        /// Initializes a new instance of the <see cref="AllianceMemberRemovedMessage"/> class.
        /// </summary>
        public AllianceMemberRemovedMessage()
        {
            // AllianceMemberRemovedMessage.
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="AllianceMemberRemovedMessage"/> class.
        /// </summary>
        public AllianceMemberRemovedMessage(ByteStream Stream) : base(Stream)
        {
            // AllianceMemberRemovedMessage.
        }

        /// <summary>
        /// Decodes this instance.
        /// </summary>
        public override void Decode()
        {
            this.MemberId = this.Stream.ReadLong();
        }

        /// <summary>
        /// Encodes this instance.
        /// </summary>
        public override void Encode()
        {
            this.Stream.WriteLong(this.MemberId);
        }
    }
}
