namespace ClashRoyale.Messages.Server.Avatar
{
    using ClashRoyale.Enums;
    using ClashRoyale.Extensions;

    public class AvatarStreamMessage : Message
    {
        /// <summary>
        /// Gets the type of this message.
        /// </summary>
        public override short Type
        {
            get
            {
                return 29567;
            }
        }

        /// <summary>
        /// Gets the service node of this message.
        /// </summary>
        public override Node ServiceNode
        {
            get
            {
                return Node.Avatar;
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="AvatarStreamMessage"/> class.
        /// </summary>
        public AvatarStreamMessage()
        {
            // AvatarStreamMessage.
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="AvatarStreamMessage"/> class.
        /// </summary>
        /// <param name="Stream">The stream.</param>
        public AvatarStreamMessage(ByteStream Stream) : base(Stream)
        {
            // AvatarStreamMessage.
        }

        /// <summary>
        /// Encodes this instance.
        /// </summary>
        public System.Collections.Generic.List<ClashRoyale.Logic.Player.AllianceMailEntry> Mails;

        public AvatarStreamMessage(System.Collections.Generic.List<ClashRoyale.Logic.Player.AllianceMailEntry> Mails)
        {
            this.Mails = Mails;
        }

        public override void Encode()
        {
            // The inbox used to be a hard-coded official capture; now it carries the player's clan mails.
            int Count = this.Mails?.Count ?? 0;

            this.Stream.WriteVInt(Count);

            // Oldest first, newest last, like the official capture (ids ascending).
            for (int I = 0; I < Count; I++)
            {
                this.Stream.WriteVInt(ClashRoyale.Logic.Player.AllianceMailEntry.StreamType);
                this.Mails[I].Encode(this.Stream);
            }
        }
    }
}