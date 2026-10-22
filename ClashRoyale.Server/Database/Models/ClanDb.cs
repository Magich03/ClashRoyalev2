namespace ClashRoyale.Database.Models
{
    using System.Threading.Tasks;

    using ClashRoyale.Logic.Alliance;

    using Newtonsoft.Json;

    public class ClanDb
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
        public Clan Profile;

        /// <summary>
        /// Initializes a new instance of the <see cref="ClanDb"/> class.
        /// </summary>
        public ClanDb()
        {
            // ClanDb.
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ClanDb"/> class.
        /// </summary>
        /// <param name="HighId">The high identifier.</param>
        /// <param name="LowId">The low identifier.</param>
        /// <param name="Json">The JSON string.</param>
        public ClanDb(int HighId, int LowId, string Json)
        {
            this.HighId     = HighId;
            this.LowId      = LowId;
            this.Profile    = JsonConvert.DeserializeObject<Clan>(Json, ClanDb.JsonSettings);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ClanDb"/> class.
        /// </summary>
        /// <param name="Clan">The clan.</param>
        public ClanDb(Clan Clan)
        {
            this.HighId     = Clan.HighId;
            this.LowId      = Clan.LowId;
            this.Profile    = Clan;
        }

        /// <summary>
        /// Creates the specified clan.
        /// </summary>
        /// <param name="Clan">The clan.</param>
        public static async Task Create(Clan Clan)
        {
            await GameDb.Clans.InsertOneAsync(new ClanDb(Clan));
        }

        /// <summary>
        /// Saves the clan in the database.
        /// </summary>
        public static async Task<ClanDb> Save(Clan Clan)
        {
            try
            {
                var clanDb = new ClanDb(Clan);
                var result = await GameDb.Clans.FindOneAndUpdateAsync(Clan.HighId, Clan.LowId, clanDb);

                if (result != null && result.HighId == Clan.HighId && result.LowId == Clan.LowId)
                {
                    return result;
                }
                else
                {
                    Logging.Error(typeof(ClanDb), "Failed to save clan.");
                }
            }
            catch (Exception ex)
            {
                Logging.Error(typeof(ClanDb), $"Error at Save(): {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// Loads the clan from the database.
        /// </summary>
        public static async Task<ClanDb> Load(int HighId, int LowId)
        {
            if (LowId > 0)
            {
                try
                {
                    var entity = await GameDb.Clans.FindAsync(HighId, LowId);

                    if (entity != null)
                    {
                        return entity;
                    }
                    else
                    {
                        Logging.Warning(typeof(ClanDb), $"Clan not found: {HighId}_{LowId}");
                    }
                }
                catch (Exception ex)
                {
                    Logging.Error(typeof(ClanDb), $"Error at Load(): {ex.Message}");
                }
            }
            else
            {
                Logging.Error(typeof(ClanDb), "LowId < 0 at Load().");
            }

            return null;
        }

        /// <summary>
        /// Deletes the clan from the database.
        /// </summary>
        public static async Task<bool> Delete(int HighId, int LowId)
        {
            if (LowId > 0)
            {
                try
                {
                    var result = await GameDb.Clans.DeleteOneAsync(HighId, LowId);

                    if (result)
                    {
                        return true;
                    }
                    else
                    {
                        Logging.Warning(typeof(ClanDb), $"Clan not found for deletion: {HighId}_{LowId}");
                    }
                }
                catch (Exception ex)
                {
                    Logging.Error(typeof(ClanDb), $"Error at Delete(): {ex.Message}");
                }
            }
            else
            {
                Logging.Error(typeof(ClanDb), "LowId <= 0 at Delete().");
            }

            return false;
        }

        /// <summary>
        /// Deserializes the specified entity.
        /// </summary>
        public bool Deserialize(out Clan Clan)
        {
            if (this.Profile != null)
            {
                Clan = this.Profile;
                return true;
            }
            else
            {
                Clan = null;
            }

            return false;
        }
    }
}