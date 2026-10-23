namespace ClashRoyale.Logic.Commands
{
    using ClashRoyale.Extensions;
    using ClashRoyale.Logic.Mode;

    /// <summary>
    /// Clan mail sent by a leader or co-leader. The server delivers it in EndClientTurnHandler,
    /// because the clan collection lives in the server project.
    /// </summary>
    public class SendAllianceMailCommand : Command
    {
        public string Title;
        public string Message;

        public override int Type
        {
            get
            {
                return 589;
            }
        }

        public override void Decode(ByteStream Stream)
        {
            // Client 3.830 layout: string, string, base.
            this.Title   = Stream.ReadString();
            this.Message = Stream.ReadString();

            base.Decode(Stream);
        }

        public override byte Execute(GameMode GameMode)
        {
            return 0;
        }
    }
}
