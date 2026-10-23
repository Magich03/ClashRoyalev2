namespace ClashRoyale.Logic.Commands
{
    using ClashRoyale.Extensions;
    using ClashRoyale.Extensions.Helper;
    using ClashRoyale.Files.Csv.Logic;
    using ClashRoyale.Logic.Home;
    using ClashRoyale.Logic.Mode;
    using ClashRoyale.Logic.Player;

    public class SpellSeenCommand : Command
    {
        /// <summary>
        /// Gets the type of this command.
        /// </summary>
        public override int Type
        {
            get
            {
                return 517;
            }
        }

        private readonly System.Collections.Generic.List<SpellData> Spells = new System.Collections.Generic.List<SpellData>();

        /// <summary>
        /// Initializes a new instance of the <see cref="SpellSeenCommand"/> class.
        /// </summary>
        public SpellSeenCommand()
        {
            // SpellSeenCommand.
        }

        /// <summary>
        /// Decodes the specified stream.
        /// </summary>
        /// <param name="Stream">The stream.</param>
        public override void Decode(ByteStream Stream)
        {
            base.Decode(Stream);

            // LogicSpellPageOpenedCommand (517) in client 3.830: base, then a counted list of card references.
            int Count = Stream.ReadVInt();

            for (int I = 0; I < Count; I++)
            {
                this.Spells.Add(Stream.DecodeData<SpellData>());
            }
        }

        /// <summary>
        /// Executes this instance.
        /// </summary>
        public override byte Execute(GameMode GameMode)
        {
            Home Home = GameMode.Home;

            if (Home != null)
            {
                Player Player = GameMode.Player;

                if (Player != null)
                {
                    foreach (SpellData Spell in this.Spells)
                    {
                        Home.GetSpellByData(Spell)?.SetShowNewIcon(false);
                    }

                    return 0;
                }

                return 2;
            }

            return 1;
        }
    }
}