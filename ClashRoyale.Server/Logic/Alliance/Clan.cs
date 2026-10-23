namespace ClashRoyale.Logic.Alliance
{
    using System.Linq;

    using ClashRoyale.Extensions;
    using ClashRoyale.Logic.Alliance.Entries;
    using ClashRoyale.Logic.Alliance.Slots;

    using Newtonsoft.Json;

    [JsonObject(MemberSerialization.OptIn)]
    public class Clan
    {
        [JsonProperty("highId")] 	    public int HighId;
        [JsonProperty("lowId")] 	    public int LowId;

        [JsonProperty("description")] 	public string Description;

        [JsonProperty("header")] 		public AllianceHeaderEntry HeaderEntry;     
        [JsonProperty("members")] 		public AllianceMemberEntries Members;
        [JsonProperty("crownChestProgress")] public ClanChestProgress ChestProgress = new ClanChestProgress();

        [JsonProperty("messages")] 		public AllianceStreamEntries Messages;

        /// <summary>
        /// Gets the alliance id.
        /// </summary>
        public long AllianceId
        {
            get
            {
                return (long) (uint) this.HighId << 32 | (uint) this.LowId;
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Clan"/> class.
        /// </summary>
        public Clan()
        {
            this.HeaderEntry	= new AllianceHeaderEntry();
            this.Members    	= new AllianceMemberEntries();
            this.Messages   	= new AllianceStreamEntries();
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Clan"/> class.
        /// </summary>
        /// <param name="HighId">The high identifier.</param>
        /// <param name="LowId">The low identifier.</param>
        public Clan(int HighId, int LowId) : this()
        {
            this.HighId         = HighId;
            this.LowId 	        = LowId;

            this.HeaderEntry.SetAlliance(this.HighId, this.LowId, this.Members.Count);
        }

        /// <summary>
        /// Encodes this instance.
        /// </summary>
        public void Encode(ByteStream Stream)
        {
            this.HeaderEntry.Encode(Stream);
			
            Stream.WriteString(this.Description);

            AllianceMemberEntry[] Entries = this.Members.Values.ToArray();

            Stream.WriteVInt(Entries.Length);

            for (int I = 0; I < Entries.Length; I++)
            {
                Entries[I].Encode(Stream);
            }

            Stream.WriteBoolean(true);
            this.ChestProgress.Status(this, System.DateTimeOffset.UtcNow).Encode(Stream);
        }

        /// <summary>
        /// Called when this alliance has finished being loaded.
        /// </summary>
        public void LoadingFinished()
        {
            this.Refresh();
        }

        /// <summary>
        /// Keeps the header (id, member count, score, access type) in sync with the clan state.
        /// </summary>
        public void Refresh()
        {
            this.HeaderEntry.SetAlliance(this.HighId, this.LowId, this.Members.Count);
            this.HeaderEntry.Score = this.Members.Values.Sum(Member => Member.Score);
            this.HeaderEntry.Donations = this.Members.Values.Sum(Member => Member.Donations);

            if (this.HeaderEntry.Type < 1 || this.HeaderEntry.Type > 3)
            {
                this.HeaderEntry.Type = 1;
            }
        }

        /// <summary>
        /// Updates the member entry of the specified player with its current profile.
        /// </summary>
        public void UpdateMember(ClashRoyale.Logic.Player.Player Player)
        {
            if (this.Members.TryGetValue(Player.PlayerId, out AllianceMemberEntry Member))
            {
                Member.SetPlayer(Player);
            }
        }

        /// <summary>
        /// Builds the full entry sent in the alliance data message.
        /// </summary>
        public AllianceFullEntry GetFullEntry()
        {
            this.Refresh();
            return new AllianceFullEntry(this.HeaderEntry, this.Members.Values.OrderByDescending(Member => Member.Score).ToArray(), this.Description ?? string.Empty)
            {
                CrownChest = this.ChestProgress.Status(this, System.DateTimeOffset.UtcNow)
            };
        }

        /// <summary>
        /// Returns a <see cref="string" /> that represents this instance.
        /// </summary>
        /// <returns>
        /// A <see cref="string" /> that represents this instance.
        /// </returns>
        public override string ToString()
        {
            return this.HighId + "-" + this.LowId;
        }
    }
}