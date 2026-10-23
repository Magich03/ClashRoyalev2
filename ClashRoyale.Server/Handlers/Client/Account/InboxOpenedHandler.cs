namespace ClashRoyale.Handlers.Client.Account
{
    using System.Threading;
    using System.Threading.Tasks;

    using ClashRoyale.Logic;
    using ClashRoyale.Messages;
    using ClashRoyale.Messages.Server.Account;

    public static class InboxOpenedHandler
    {
        /// <summary>
        /// Refreshes clan mail before the news-list response rebuilds the visible inbox.
        /// NetworkManager serializes encryption and sends to keep both responses in order.
        /// </summary>
        public static async Task Handle(Device Device, Message Message, CancellationToken Cancellation)
        {
            var Player = Device.GameMode.Player;

            Device.NetworkManager.SendMessage(new ClashRoyale.Messages.Server.Avatar.AvatarStreamMessage(Player.Mails));
            Device.NetworkManager.SendMessage(new InboxListMessage());
            Player.Mails.ForEach(Mail => Mail.IsNew = false);

            await Task.CompletedTask;
        }
    }
}
