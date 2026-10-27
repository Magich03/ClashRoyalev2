namespace ClashRoyale.Handlers.Client.Sector;

using ClashRoyale.Logic;
using ClashRoyale.Logic.Battle;
using ClashRoyale.Messages;
using ClashRoyale.Messages.Client.Sector;

public static class RequestSectorStateHandler
{
    public static Task Handle(Device device, Message message, CancellationToken cancellation)
    {
        MatchBattleSession.SendNativeState(device, ((RequestSectorStateMessage)message).ClientTick);
        return Task.CompletedTask;
    }
}
