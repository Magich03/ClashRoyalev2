namespace ClashRoyale.Database.Models
{
    using System.Threading.Tasks;

    using ClashRoyale.Logic.Battle;

    using Newtonsoft.Json;

    public class BattleDb
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
        public BattleLog Profile;

        /// <summary>
        /// Initializes a new instance of the <see cref="BattleDb"/> class.
        /// </summary>
        public BattleDb()
        {
            // BattleDb.
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="BattleDb"/> class.
        /// </summary>
        /// <param name="HighId">The high identifier.</param>
        /// <param name="LowId">The low identifier.</param>
        /// <param name="Json">The JSON string.</param>
        public BattleDb(int HighId, int LowId, string Json)
        {
            this.HighId     = HighId;
            this.LowId      = LowId;
            this.Profile    = JsonConvert.DeserializeObject<BattleLog>(Json, BattleDb.JsonSettings);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="BattleDb"/> class.
        /// </summary>
        /// <param name="BattleLog">The battle log.</param>
        public BattleDb(BattleLog BattleLog)
        {
            this.HighId     = BattleLog.HighId;
            this.LowId      = BattleLog.LowId;
            this.Profile    = BattleLog;
        }

        /// <summary>
        /// Creates the specified battle log.
        /// </summary>
        /// <param name="BattleLog">The battle log.</param>
        public static async Task Create(BattleLog BattleLog)
        {
            await GameDb.Battles.InsertOneAsync(new BattleDb(BattleLog));
        }

        /// <summary>
        /// Saves the battle log in the database.
        /// </summary>
        public static async Task<BattleDb> Save(BattleLog BattleLog)
        {
            try
            {
                var battleDb = new BattleDb(BattleLog);
                var result = await GameDb.Battles.FindOneAndUpdateAsync(BattleLog.HighId, BattleLog.LowId, battleDb);

                if (result != null && result.HighId == BattleLog.HighId && result.LowId == BattleLog.LowId)
                {
                    return result;
                }
                else
                {
                    Logging.Error(typeof(BattleDb), "Failed to save battle.");
                }
            }
            catch (System.Exception ex)
            {
                Logging.Error(typeof(BattleDb), $"Error at Save(): {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// Loads the battle from the database.
        /// </summary>
        public static async Task<BattleDb> Load(int HighId, int LowId)
        {
            if (LowId > 0)
            {
                try
                {
                    var entity = await GameDb.Battles.FindAsync(HighId, LowId);

                    if (entity != null)
                    {
                        return entity;
                    }
                    else
                    {
                        Logging.Warning(typeof(BattleDb), $"Battle not found: {HighId}_{LowId}");
                    }
                }
                catch (System.Exception ex)
                {
                    Logging.Error(typeof(BattleDb), $"Error at Load(): {ex.Message}");
                }
            }
            else
            {
                Logging.Error(typeof(BattleDb), "LowId < 0 at Load().");
            }

            return null;
        }

        /// <summary>
        /// Deletes the battle from the database.
        /// </summary>
        public static async Task<bool> Delete(int HighId, int LowId)
        {
            if (LowId > 0)
            {
                try
                {
                    var result = await GameDb.Battles.DeleteOneAsync(HighId, LowId);

                    if (result)
                    {
                        return true;
                    }
                    else
                    {
                        Logging.Warning(typeof(BattleDb), $"Battle not found for deletion: {HighId}_{LowId}");
                    }
                }
                catch (System.Exception ex)
                {
                    Logging.Error(typeof(BattleDb), $"Error at Delete(): {ex.Message}");
                }
            }
            else
            {
                Logging.Error(typeof(BattleDb), "LowId <= 0 at Delete().");
            }

            return false;
        }

        /// <summary>
        /// Deserializes the specified entity.
        /// </summary>
        public bool Deserialize(out BattleLog BattleLog)
        {
            if (this.Profile != null)
            {
                BattleLog = this.Profile;
                return true;
            }
            else
            {
                BattleLog = null;
            }

            return false;
        }
    }
}