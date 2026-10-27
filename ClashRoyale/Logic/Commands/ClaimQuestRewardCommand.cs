namespace ClashRoyale.Logic.Commands;
using ClashRoyale.Extensions;
using ClashRoyale.Logic.Mode;
using ClashRoyale.Logic.Quests;
public sealed class ClaimQuestRewardCommand : Command
{
    public override int Type=>510;
    public int QuestId;public bool Special;
    public override void Decode(ByteStream s){base.Decode(s);QuestId=s.ReadVInt();Special=s.ReadBoolean();}
    public override void Encode(ChecksumEncoder s){base.Encode(s);s.WriteVInt(QuestId);s.WriteBoolean(Special);}
    public override byte Execute(GameMode mode)=>mode.Home.Quests.Claim(mode,QuestId,Special,QuestState.Now);
}
