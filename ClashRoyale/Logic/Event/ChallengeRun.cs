namespace ClashRoyale.Logic.Event
{
    using System;
    using ClashRoyale.Enums;
    using ClashRoyale.Extensions;
    using ClashRoyale.Files.Csv;
    using ClashRoyale.Files.Csv.Logic;
    using Newtonsoft.Json;

    public sealed class ChallengeRun
    {
        public string Id = Guid.NewGuid().ToString("N");
        public int ModeId;
        public int Wins;
        public int Losses;
        public string LastMatchId;
        public bool RewardClaimed;
        [JsonIgnore] public SurvivalModeData Data => LiveEventCatalog.Resolve(ModeId);
        [JsonIgnore] public GameModeData GameMode => Data == null ? null : CsvFiles.Get(Gamefile.GameModes).GetData<GameModeData>(Data.GameMode);
        [JsonIgnore] public bool SingleMatch => ModeId / 1000000 == 73;
        [JsonIgnore] public bool Finished => Data != null && ((SingleMatch && !string.IsNullOrEmpty(LastMatchId)) || Wins >= Data.MaxWins || Losses >= Data.MaxLoss);

        // Native LogicSurvivalState: int mode ID followed by wins, losses, maximum wins.
        public void Encode(ChecksumEncoder stream)
        {
            stream.WriteInt(ModeId);
            stream.WriteVInt(Wins);
            stream.WriteVInt(Losses);
            stream.WriteVInt(Data.MaxWins);
        }

        public bool RecordResult(string matchId, int own, int enemy)
        {
            if (string.IsNullOrEmpty(matchId) || LastMatchId == matchId || Finished || RewardClaimed) return false;
            LastMatchId = matchId;
            if (own > enemy) Wins++;
            else if (own < enemy) Losses++;
            return true;
        }
    }
}
