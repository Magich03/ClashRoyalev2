namespace ClashRoyale.Messages.Client.Alliance
{
    using ClashRoyale.Enums;
    using ClashRoyale.Extensions;

    public class KickAllianceMemberMessage : Message
    {
        /// <summary>
        /// Gets the type of this message.
        /// </summary>
        public override short Type
        {
            get
            {
                return 17800;
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

        public string Message;

        /// <summary>
        /// Initializes a new instance of the <see cref="KickAllianceMemberMessage"/> class.
        /// </summary>
        public KickAllianceMemberMessage()
        {
            // KickAllianceMemberMessage.
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="KickAllianceMemberMessage"/> class.
        /// </summary>
        public KickAllianceMemberMessage(ByteStream Stream) : base(Stream)
        {
            // KickAllianceMemberMessage.
        }

        /// <summary>
        /// Decodes this instance.
        /// </summary>
        public override void Decode()
        {
            this.MemberId = this.Stream.ReadLong();
            this.Message = this.Stream.ReadString();
        }

        /// <summary>
        /// Encodes this instance.
        /// </summary>
        public override void Encode()
        {
            this.Stream.WriteLong(this.MemberId);
            this.Stream.WriteString(this.Message);
        }
    }
}
