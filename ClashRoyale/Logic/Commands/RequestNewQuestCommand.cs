namespace ClashRoyale.Logic.Commands
{
    using ClashRoyale.Extensions;
    using ClashRoyale.Logic.Mode;

    public class RequestNewQuestCommand : Command
    {
        /// <summary>
        /// Gets the type of this command.
        /// </summary>
        public override int Type
        {
            get
            {
                return 584;
            }
        }

        public int QuestIndex;
        public int QuestUnknown;

        /// <summary>
        /// Initializes a new instance of the <see cref="RequestNewQuestCommand"/> class.
        /// </summary>
        public RequestNewQuestCommand()
        {
            // RequestNewQuestCommand.
        }

        /// <summary>
        /// Decodes this instance.
        /// </summary>
        public override void Decode(ByteStream Stream)
        {
            base.Decode(Stream);

            // LogicDiscardQuestCommand (584) in client 3.830: base, VInt, bool.
            this.QuestIndex     = Stream.ReadVInt();
            this.QuestUnknown   = Stream.ReadBoolean() ? 1 : 0;
        }

        /// <summary>
        /// Encodes this instance.
        /// </summary>
        public override void Encode(ChecksumEncoder Stream)
        {
            base.Encode(Stream);

            Stream.WriteVInt(this.QuestIndex);
            Stream.WriteBoolean(this.QuestUnknown != 0);
        }

        /// <summary>
        /// Executes this instance.
        /// </summary>
        public override byte Execute(GameMode GameMode)
        {
            return GameMode.Home.Quests.Discard(GameMode, this.QuestIndex, this.QuestUnknown != 0, ClashRoyale.Logic.Quests.QuestState.Now);
        }
    }
}