namespace ClashRoyale.Handlers.Client.Sector
{
    using System.Threading;
    using System.Threading.Tasks;

    using ClashRoyale.Enums;
    using ClashRoyale.Exceptions;
    using ClashRoyale.Logic;
    using ClashRoyale.Messages;
    using ClashRoyale.Messages.Client.Sector;

    public static class RequestSectorStateHandler
    {
        /// <summary>
        /// Handles the specified <see cref="Message"/>.
        /// </summary>
        /// <param name="Device">The device.</param>
        /// <param name="Message">The message.</param>
        /// <param name="Cancellation">The cancellation.</param>
        public static async Task Handle(Device Device, Message Message, CancellationToken Cancellation)
        {
            var RequestSectorStateMessage = (RequestSectorStateMessage) Message;

            if (RequestSectorStateMessage == null)
            {
                throw new LogicException(typeof(RequestSectorStateHandler), nameof(RequestSectorStateMessage) + " == null at Handle(Device, Message, CancellationToken).");
            }

            if (Device.GameMode.State == HomeState.Attack)
            {
                // Client is requesting current battle state
                Device.GameMode.SectorManager.SendSectorState();
            }
            else
            {
                Logging.Info(typeof(RequestSectorStateHandler), "State != HomeState.Attack at Handle(Device, Message, CancellationToken).");
            }
        }
    }
}
