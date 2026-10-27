namespace ClashRoyale.Handlers.Client.Attack;
using System.Threading;
using System.Threading.Tasks;
using ClashRoyale.Logic;
using ClashRoyale.Logic.Battle;
using ClashRoyale.Messages;
using ClashRoyale.Messages.Client.Attack;
public static class SendRematchHandler
{
    public static async Task Handle(Device device, Message message, CancellationToken cancellation)
    {
        await MatchBattleSession.Rematch(device, ((SendRematchMessage)message).Ready);
    }
}
