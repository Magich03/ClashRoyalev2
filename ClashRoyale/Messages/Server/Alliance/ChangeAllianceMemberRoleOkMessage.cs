namespace ClashRoyale.Messages.Server.Alliance
{
    using ClashRoyale.Enums;
    using ClashRoyale.Extensions;

    public class ChangeAllianceMemberRoleOkMessage : Message
    {
        /// <summary>
        /// Gets the type of this message.
        /// </summary>
        public override short Type
        {
            get
            {
                return 29058;
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

        public int NewRole;

        /// <summary>
        /// Initializes a new instance of the <see cref="ChangeAllianceMemberRoleOkMessage"/> class.
        /// </summary>
        public ChangeAllianceMemberRoleOkMessage()
        {
            // ChangeAllianceMemberRoleOkMessage.
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ChangeAllianceMemberRoleOkMessage"/> class.
        /// </summary>
        public ChangeAllianceMemberRoleOkMessage(ByteStream Stream) : base(Stream)
        {
            // ChangeAllianceMemberRoleOkMessage.
        }

        /// <summary>
        /// Decodes this instance.
        /// </summary>
        public override void Decode()
        {
            this.MemberId = this.Stream.ReadLong();
            this.NewRole = this.Stream.ReadVInt();
        }

        /// <summary>
        /// Encodes this instance.
        /// </summary>
        public override void Encode()
        {
            this.Stream.WriteLong(this.MemberId);
            this.Stream.WriteVInt(this.NewRole);
        }
    }
}
