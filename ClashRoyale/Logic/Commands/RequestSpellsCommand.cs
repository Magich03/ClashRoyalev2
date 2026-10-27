namespace ClashRoyale.Logic.Commands;
using ClashRoyale.Extensions;
using ClashRoyale.Extensions.Helper;
using ClashRoyale.Files.Csv.Logic;
using ClashRoyale.Logic.Mode;
public class RequestSpellsCommand : Command
{
    public override int Type => 551;
    public SpellData Card;
    public bool Executed, Handled;
    public override void Decode(ByteStream s){base.Decode(s);Card=s.DecodeData<SpellData>();}
    public override void Encode(ChecksumEncoder s){base.Encode(s);s.EncodeData(Card);}
    public override byte Execute(GameMode mode)
    {
        if(!Executed){Executed=true;mode.CommandManager.CompletedCardRequests.Enqueue(this);}
        return 0;
    }
}
