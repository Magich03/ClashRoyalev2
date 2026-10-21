namespace ClashRoyale.Database.Models
{
    using System.Threading.Tasks;

    using ClashRoyale.Logic.Player;

    using Newtonsoft.Json;

    public class PlayerDb
    {
        /// <summary>
        /// The settings for the <see cref="JsonConvert" /> class.
        /// </summary>
        private static readonly JsonSerializerSettings JsonSettings = new JsonSerializerSettings
        {
            TypeNameHandling            = TypeNameHandling.None,            MissingMemberHandling   = MissingMemberHandling.Ignore,
            DefaultValueHandling        = DefaultValueHandling.Include,     NullValueHandling       = NullValueHandling.Ignore,
            ReferenceLoopHandling       = ReferenceLoopHandling.Ignore,     Formatting              = Formatting.None
        };

        public int HighId;
        public int LowId;
        public Player Profile;

        /// <summary>
        /// Initializes a new instance of the <see cref="PlayerDb"/> class.
        /// </summary>
        public PlayerDb()
        {
            // PlayerDb.
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="PlayerDb"/> class.
        /// </summary>
        /// <param name="HighId">The high identifier.</param>
        /// <param name="LowId">The low identifier.</param>
        /// <param name="Json">The JSON string.</param>
        public PlayerDb(int HighId, int LowId, string Json)
        {
            this.HighId     = HighId;
            this.LowId      = LowId;
            this.Profile    = JsonConvert.DeserializeObject<Player>(Json, PlayerDb.JsonSettings);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="PlayerDb"/> class.
        /// </summary>
        /// <param name="Player">The player.</param>
        public PlayerDb(Player Player)
        {
            this.HighId     = Player.HighId;
            this.LowId      = Player.LowId;
            this.Profile    = Player;
        }

        /// <summary>
        /// Creates the specified player.
        /// </summary>
        /// <param name="Player">The player.</param>
        public static async Task Create(Player Player)
        {
            await GameDb.Players.InsertOneAsync(new PlayerDb(Player));
        }

        /// <summary>
        /// Saves the player in the database.
        /// </summary>
        public static async Task<PlayerDb> Save(Player Player)
        {
            try
            {
                var playerDb = new PlayerDb(Player);
                var result = await GameDb.Players.FindOneAndUpdateAsync(Player.HighId, Player.LowId, playerDb);

                if (result != null && result.HighId == Player.HighId && result.LowId == Player.LowId)
                {
                    return result;
                }
                else
                {
                    Logging.Error(typeof(PlayerDb), "Failed to save player.");
                }
            }
            catch (Exception ex)
            {
                Logging.Error(typeof(PlayerDb), $"Error at Save(): {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// Loads the player from the database.
        /// </summary>
        public static async Task<PlayerDb> Load(int HighId, int LowId)
        {
            if (LowId > 0)
            {
                try
                {
                    var entity = await GameDb.Players.FindAsync(HighId, LowId);

                    if (entity != null)
                    {
                        return entity;
                    }
                    else
                    {
                        Logging.Warning(typeof(PlayerDb), $"Player not found: {HighId}_{LowId}");
                    }
                }
                catch (Exception ex)
                {
                    Logging.Error(typeof(PlayerDb), $"Error at Load(): {ex.Message}");
                }
            }
            else
            {
                Logging.Error(typeof(PlayerDb), "LowId < 0 at Load().");
            }

            return null;
        }

        /// <summary>
        /// Deletes the player from the database.
        /// </summary>
        public static async Task<bool> Delete(int HighId, int LowId)
        {
            if (LowId > 0)
            {
                try
                {
                    var result = await GameDb.Players.DeleteOneAsync(HighId, LowId);

                    if (result)
                    {
                        return true;
                    }
                    else
                    {
                        Logging.Warning(typeof(PlayerDb), $"Player not found for deletion: {HighId}_{LowId}");
                    }
                }
                catch (Exception ex)
                {
                    Logging.Error(typeof(PlayerDb), $"Error at Delete(): {ex.Message}");
                }
            }
            else
            {
                Logging.Error(typeof(PlayerDb), "LowId <= 0 at Delete().");
            }

            return false;
        }

        /// <summary>
        /// Deserializes the specified entity.
        /// </summary>
        public bool Deserialize(out Player Player)
        {
            if (this.Profile != null)
            {
                Player = this.Profile;
                return true;
            }
            else
            {
                Player = null;
            }

            return false;
        }
    }
}