namespace ClashRoyale.Handlers.Client.Alliance
{
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    using ClashRoyale.Exceptions;
    using ClashRoyale.Logic;
    using ClashRoyale.Logic.Alliance;
    using ClashRoyale.Logic.Alliance.Entries;
    using ClashRoyale.Logic.Collections;
    using ClashRoyale.Logic.Commands.Server;
    using ClashRoyale.Messages;
    using ClashRoyale.Messages.Client.Alliance;
    using ClashRoyale.Messages.Server.Alliance;

    public static class CreateAllianceHandler
    {
        /// <summary>
        /// Handles the specified <see cref="Message"/>.
        /// </summary>
        /// <param name="Device">The device.</param>
        /// <param name="Message">The message.</param>
        /// <param name="Cancellation">The cancellation.</param>
        public static async Task Handle(Device Device, Message Message, CancellationToken Cancellation)
        {
            var CreateAllianceMessage = (CreateAllianceMessage) Message;

            if (CreateAllianceMessage == null)
            {
                throw new LogicException(typeof(CreateAllianceHandler), nameof(CreateAllianceMessage) + " == null at Handle(Device, Message, CancellationToken).");
            }

            if (string.IsNullOrEmpty(CreateAllianceMessage.Name))
            {
                throw new LogicException(typeof(CreateAllianceHandler), "Name == null at Handle(Device, Message, CancellationToken).");
            }
            else
            {
                if (CreateAllianceMessage.Name.Length < 3)
                {
                    throw new LogicException(typeof(CreateAllianceHandler), "Name.Length < 3 at Handle(Device, Message, CancellationToken).");
                }
            }

            if (CreateAllianceMessage.BadgeData == null)
            {
                throw new LogicException(typeof(CreateAllianceHandler), "BadgeData == null at Handle(Device, Message, CancellationToken).");
            }

            if (CreateAllianceMessage.RegionData == null)
            {
                throw new LogicException(typeof(CreateAllianceHandler), "RegionData == null at Handle(Device, Message, CancellationToken).");
            }

            if (CreateAllianceMessage.RequiredScore < 0)
            {
                throw new LogicException(typeof(CreateAllianceHandler), "RequiredScore < 0 at Handle(Device, Message, CancellationToken).");
            }

            if (CreateAllianceMessage.AccessType < 0)
            {
                throw new LogicException(typeof(CreateAllianceHandler), "AccessType < 0 at Handle(Device, Message, CancellationToken).");
            }
            else
            {
                if (CreateAllianceMessage.AccessType > 3)
                {
                    throw new LogicException(typeof(CreateAllianceHandler), "AccessType > 3 at Handle(Device, Message, CancellationToken).");
                }
            }

            Clan Clan = new Clan()
            {
                HeaderEntry =
                {
                    Name    = CreateAllianceMessage.Name,
                    Badge   = CreateAllianceMessage.BadgeData,
                    Region  = CreateAllianceMessage.RegionData,
                    Type    = CreateAllianceMessage.AccessType,

                    RequiredScore = CreateAllianceMessage.RequiredScore,
                },
                Description = CreateAllianceMessage.Description
            };

            if (Clan.Members.TryAdd(Device.GameMode.Player))
            {
                Clan = await Clans.Create(Clan);

                if (Clan != null)
                {
                    await ClanHelper.CompleteJoin(Device, Clan, true);
                }
                else
                {
                    Logging.Warning(typeof(CreateAllianceHandler), "Clan == null at Handle(Device, Message, CancellationToken).");
                }
            }
            else
            {
                Logging.Warning(typeof(CreateAllianceHandler), "Members.TryAdd(Player, true) != true at Handle(Device, Message, CancellationToken).");
            }
        }
    }
}
