namespace ClashRoyale.Messages.Server.Alliance
{
    using ClashRoyale.Enums;
    using ClashRoyale.Extensions;

    public class KickAllianceMemberOkMessage : Message
    {
        /// <summary>
        /// Gets the type of this message.
        /// </summary>
        public override short Type
        {
            get
            {
                return 26310;
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
        /// Initializes a new instance of the <see cref="KickAllianceMemberOkMessage"/> class.
        /// </summary>
        public KickAllianceMemberOkMessage()
        {
            // KickAllianceMemberOkMessage.
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="KickAllianceMemberOkMessage"/> class.
        /// </summary>
        public KickAllianceMemberOkMessage(ByteStream Stream) : base(Stream)
        {
            // KickAllianceMemberOkMessage.
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
