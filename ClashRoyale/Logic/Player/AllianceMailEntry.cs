namespace ClashRoyale.Logic.Player
{
    using System;

    using ClashRoyale.Extensions;
    using ClashRoyale.Extensions.Helper;
    using ClashRoyale.Files.Csv.Logic;

    using Newtonsoft.Json;

    /// <summary>
    /// Clan mail shown in the player's inbox (AllianceMailAvatarStreamEntry, avatar stream entry type 6).
    /// The layout matches an official AvatarStreamMessage capture.
    /// </summary>
    [JsonObject(MemberSerialization.OptIn)]
    public class AllianceMailEntry
    {
        public const int StreamType = 6;

        [JsonProperty("highId")]        public int HighId;
        [JsonProperty("lowId")]         public int LowId;

        [JsonProperty("senderHighId")]  public int SenderHighId;
        [JsonProperty("senderLowId")]   public int SenderLowId;
        [JsonProperty("senderName")]    public string SenderName;
        [JsonProperty("senderLevel")]   public int SenderLevel;

        [JsonProperty("title")]         public string Title;
        [JsonProperty("message")]       public string Message;

        [JsonProperty("clanHighId")]    public int ClanHighId;
        [JsonProperty("clanLowId")]     public int ClanLowId;
        [JsonProperty("clanName")]      public string ClanName;
        [JsonProperty("clanBadge")]     public AllianceBadgeData ClanBadge;

        [JsonProperty("created")]       public DateTime Created = DateTime.UtcNow;
        [JsonProperty("new")]           public bool IsNew = true;

        public long StreamId
        {
            get
            {
                return (long) this.HighId << 32 | (uint) this.LowId;
            }
        }

        public void Encode(ChecksumEncoder Stream)
        {
            // AvatarStreamEntry base.
            Stream.WriteInt(this.HighId);
            Stream.WriteInt(this.LowId);
            Stream.WriteBoolean(true);
            Stream.WriteInt(this.SenderHighId);
            Stream.WriteInt(this.SenderLowId);
            Stream.WriteString(this.SenderName);
            Stream.WriteVInt(this.SenderLevel);
            Stream.WriteVInt(Math.Max(0, (int) DateTime.UtcNow.Subtract(this.Created).TotalSeconds));
            // Client +0x18 is a hidden-entry flag (getter 0x2940f6), not unread state.
            // The inbox renderer at 0x86d86 skips entries when it is true.
            Stream.WriteBoolean(false);
            Stream.WriteBoolean(false);

            // AllianceMailAvatarStreamEntry.
            Stream.WriteString(this.Message);
            Stream.WriteBoolean(true);
            Stream.WriteInt(this.SenderHighId);
            Stream.WriteInt(this.SenderLowId);
            Stream.WriteString(this.Title);
            Stream.WriteInt(this.ClanHighId);
            Stream.WriteInt(this.ClanLowId);
            Stream.WriteString(this.ClanName);
            Stream.EncodeData(this.ClanBadge);
        }
    }
}
