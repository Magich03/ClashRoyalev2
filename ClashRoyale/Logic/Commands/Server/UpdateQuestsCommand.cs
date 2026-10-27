namespace ClashRoyale.Logic.Commands.Server;
using System.Linq;
using ClashRoyale.Extensions;
using ClashRoyale.Logic.Mode;
using ClashRoyale.Logic.Quests;
public sealed class UpdateQuestsCommand : ServerCommand
{
    public override int Type=>300;
    public QuestEntry[] Quests=System.Array.Empty<QuestEntry>();
    public bool Special;
    public UpdateQuestsCommand() { }
    public UpdateQuestsCommand(QuestEntry[] quests) { Quests=quests; }
    public override void Encode(ChecksumEncoder s) {base.Encode(s);s.WriteBoolean(Special);s.WriteBoolean(true);s.WriteVInt(Quests.Length);foreach(var q in Quests)q.Encode(s,QuestState.Now);}
    public override void Decode(ByteStream s) {base.Decode(s);Special=s.ReadBoolean();if(!s.ReadBoolean())return;int count=s.ReadVInt();if(count<0||count>4)throw new System.IO.InvalidDataException("Invalid quest update count");Quests=Enumerable.Range(0,count).Select(_=>QuestEntry.Decode(s)).ToArray();}
    public override byte Execute(GameMode mode)=>0; // Authoritative state was saved when replacement was generated.
}
