namespace ClashRoyale.Logic.Alliance.Entries
{
    using System;
    using ClashRoyale.Extensions;

    /// <summary>Client 3.830 clan crown chest status (decoder 0x26a426).</summary>
    public class AllianceCrownChestEntry
    {
        public int State;
        public int TimeLeft;
        public int Progress;
        public int StartTime;
        public int EndTime;
        public int Id;
        public int EventType;
        public int NextEventType;

        // Friday 00:00 UTC through Monday 00:00 UTC.
        public static AllianceCrownChestEntry CreateWeekly(DateTimeOffset now)
        {
            now = now.ToUniversalTime();
            int daysSinceFriday = ((int)now.DayOfWeek - (int)DayOfWeek.Friday + 7) % 7;
            var start = new DateTimeOffset(now.Date, TimeSpan.Zero).AddDays(-daysSinceFriday);
            var end = start.AddDays(3);
            bool active = now < end;
            if (!active)
            {
                start = start.AddDays(7);
                end = start.AddDays(3);
            }
            return new AllianceCrownChestEntry
            {
                State = active ? 1 : 0,
                TimeLeft = (int)Math.Ceiling(((active ? end : start) - now).TotalSeconds),
                StartTime = checked((int)start.ToUnixTimeSeconds()),
                EndTime = checked((int)end.ToUnixTimeSeconds()),
                Id = (int)(start.ToUnixTimeSeconds() / (7 * 86400)),
                EventType = 0,
                NextEventType = 0
            };
        }

        public void Encode(ChecksumEncoder stream)
        {
            stream.WriteVInt(this.State);
            stream.WriteVInt(this.TimeLeft);
            stream.WriteVInt(this.Progress);
            // These timestamps are fixed-width integers, unlike the other fields.
            stream.WriteInt(this.StartTime);
            stream.WriteInt(this.EndTime);
            stream.WriteVInt(this.Id);
            stream.WriteVInt(this.EventType);
            stream.WriteVInt(this.NextEventType);
        }
    }
}
