namespace ClashRoyale.Messages.Server.Avatar
{
    using ClashRoyale.Enums;
    using ClashRoyale.Logic.Player;

    /// <summary>
    /// Pushes one new entry into the inbox of an online player (client: vint type, entry).
    /// </summary>
    public class AvatarStreamEntryMessage : Message
    {
        public override short Type
        {
            get
            {
                return 22837;
            }
        }

        public override Node ServiceNode
        {
            get
            {
                return Node.Avatar;
            }
        }

        public AllianceMailEntry Mail;

        public AvatarStreamEntryMessage(AllianceMailEntry Mail)
        {
            this.Mail = Mail;
        }

        public override void Encode()
        {
            this.Stream.WriteVInt(AllianceMailEntry.StreamType);
            this.Mail.Encode(this.Stream);
        }
    }
}
