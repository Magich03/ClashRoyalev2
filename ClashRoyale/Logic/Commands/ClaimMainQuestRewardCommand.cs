namespace ClashRoyale.Logic.Commands;
using ClashRoyale.Logic.Mode;
public sealed class ClaimMainQuestRewardCommand : Command
{
    public override int Type=>540;
    public override byte Execute(GameMode mode)=>mode.Home.Quests.ClaimMain(mode);
}
