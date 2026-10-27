namespace ClashRoyale.Logic.Commands
{
    using ClashRoyale.Extensions;
    using ClashRoyale.Logic.Mode;

    /// <summary>
    /// LogicDraftChestChooseCommand (520): the player picked a card in a draft chest.
    /// Layout from the client decoder: command base, then one VInt.
    /// </summary>
    public class DraftChestChooseCommand : Command
    {
        public override int Type
        {
            get
            {
                return 520;
            }
        }

        public int Choice;

        public override void Decode(ByteStream Stream)
        {
            base.Decode(Stream);
            this.Choice = Stream.ReadVInt();
        }

        public override void Encode(ChecksumEncoder Stream)
        {
            base.Encode(Stream);
            Stream.WriteVInt(this.Choice);
        }

        public override byte Execute(GameMode GameMode)
        {
            return ClashRoyale.Logic.Seasons.SeasonRewards.Choose(GameMode, this.Choice);
        }
    }
}
