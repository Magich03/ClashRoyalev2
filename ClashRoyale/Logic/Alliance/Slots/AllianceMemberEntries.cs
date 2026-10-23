namespace ClashRoyale.Logic.Alliance.Slots
{
    using System.Collections.Concurrent;
    using System.Linq;
    using System.Threading.Tasks;

    using ClashRoyale.Logic.Alliance.Entries;
    using ClashRoyale.Logic.Player;
    using ClashRoyale.Messages.Server.Alliance;

    public class AllianceMemberEntries : ConcurrentDictionary<long, AllianceMemberEntry>
    {
        private readonly ConcurrentDictionary<long, Player> Connected;

        /// <summary>
        /// Initializes a new instance of the <see cref="AllianceMemberEntries"/> class.
        /// </summary>
        public AllianceMemberEntries()
        {
            this.Connected = new ConcurrentDictionary<long, Player>();
        }

        /// <summary>
        /// Tries to add the specified player.
        /// </summary>
        /// <param name="Player">The player.</param>
        public bool TryAdd(Player Player)
        {
            if (!this.ContainsKey(Player.PlayerId))
            {
                if (this.Count < 50)
                {
                    AllianceMemberEntry Entry = new AllianceMemberEntry(Player, 1);

                    if (this.Count == 0)
                    {
                        Entry.SetRole(2);
                    }

                    if (this.TryAdd(Player.PlayerId, Entry))
                    {
                        return true;
                    }
                    else
                    {
                        Logging.Error(this.GetType(), "TryAdd(PlayerId, Entry) != true at TryAdd(Player).");
                    }
                }
                else
                {
                    Logging.Warning(this.GetType(), "Count >= 50 at TryAdd(Player).");
                }
            }
            else
            {
                Logging.Error(this.GetType(), "ContainsKey(PlayerId) != false at TryAdd(Player).");
            }

            return false;
        }

        /// <summary>
        /// Removes the specified player.
        /// </summary>
        /// <param name="Player">The player.</param>
        public bool TryRemove(Player Player)
        {
            if (Player != null)
            {
                if (this.TryRemove(Player.PlayerId, out AllianceMemberEntry _))
                {
                    return true;
                }
                else
                {
                    Logging.Warning(this.GetType(), "TryRemove(PlayerId, out _) != true at TryRemove(Player).");
                }
            }
            else
            {
                Logging.Error(this.GetType(), "Player == null at TryRemove(Player).");
            }

            return false;
        }

        /// <summary>
        /// Adds the specified player to the online list.
        /// </summary>
        public async Task<bool> TryAddOnlineMember(Player Player)
        {
            // A relog replaces the previous session's player instance.
            this.Connected[Player.PlayerId] = Player;
            await this.SendStatusUpdated();
            return true;
        }

        /// <summary>
        /// Removes the specified player from the online list.
        /// </summary>
        public async Task<bool> TryRemoveOnlineMember(Player Player)
        {
            if (this.Connected.TryRemove(Player.PlayerId, out _))
            {
                await this.SendStatusUpdated();
                return true;
            }
            else
            {
                Logging.Warning(this.GetType(), "TryRemove(PlayerId, out _) != true at TryRemoveOnlineMember(Player).");
            }

            return false;
        }

        /// <summary>
        /// Sends the message saying that the alliance status has been updated.
        /// </summary>
        private async Task SendStatusUpdated()
        {
            var OnlineMembers = this.Connected.Values.ToArray();

            await Task.Run(() =>
            {
                foreach (Player Member in OnlineMembers)
                {
                    Member.GameMode.Listener.SendMessage(new AllianceOnlineStatusUpdatedMessage(OnlineMembers.Length));
                }
            });
        }

        /// <summary>
        /// Returns this instance data as an array.
        /// </summary>
        /// <summary>
        /// Gets the online player with the specified id, or null.
        /// </summary>
        public Player GetConnected(long PlayerId)
        {
            this.Connected.TryGetValue(PlayerId, out Player Player);
            return Player;
        }

        /// <summary>
        /// Sends a message to every online member. A new message is built per member because a message is encoded in place.
        /// </summary>
        public void Broadcast(System.Func<Messages.Message> CreateMessage, long ExceptId = 0)
        {
            foreach (Player Member in this.Connected.Values.ToArray())
            {
                if (Member.PlayerId != ExceptId && Member.GameMode?.Listener != null)
                {
                    Member.GameMode.Listener.SendMessage(CreateMessage());
                }
            }
        }

        public AllianceMemberEntry[] ToArray()
        {
            return this.Values.ToArray();
        }
    }
}