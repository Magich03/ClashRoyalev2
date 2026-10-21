namespace ClashRoyale.Database
{
    using ClashRoyale.Database.Models;
    using System;
    using System.IO;
    using System.Threading.Tasks;

    public static class GameDb
    {
        public static bool Initialized { get; set; }

        public static JsonRepository<PlayerDb> Players;
        public static JsonRepository<ClanDb> Clans;
        public static JsonRepository<BattleDb> Battles;

        private static readonly string DataPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data");

        /// <summary>
        /// Initializes the database and collections using JSON file storage.
        /// </summary>
        public static void Initialize()
        {
            if (GameDb.Initialized)
            {
                Console.WriteLine("GameDb is already initialized.");
                return;
            }

            try
            {
                Console.WriteLine("Initializing JSON file-based storage...");

                // Create data directory if it doesn't exist
                if (!Directory.Exists(DataPath))
                {
                    Directory.CreateDirectory(DataPath);
                }

                // Initialize repositories
                GameDb.Players = new JsonRepository<PlayerDb>(DataPath, "Players");
                GameDb.Clans = new JsonRepository<ClanDb>(DataPath, "Clans");
                GameDb.Battles = new JsonRepository<BattleDb>(DataPath, "Battles");

                // Verify repositories are not null
                if (GameDb.Players == null || GameDb.Clans == null || GameDb.Battles == null)
                {
                    throw new Exception("Failed to initialize one or more repositories.");
                }

                GameDb.Initialized = true;
                Console.WriteLine("GameDb initialized successfully with JSON file storage.");
                Console.WriteLine($"Data directory: {DataPath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error initializing GameDb: {ex.Message}");
                Logging.Error(typeof(GameDb), $"GameDb initialization failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Gets the seed for the specified collection (Players).
        /// </summary>
        public static int GetPlayersSeed()
        {
            try
            {
                var task = Task.Run(async () => await GameDb.Players.GetMaxLowIdAsync(Config.ServerId));
                return task.Result;
            }
            catch (Exception ex)
            {
                Logging.Error(typeof(GameDb), $"Error getting players seed: {ex.Message}");
                return 0;
            }
        }

        /// <summary>
        /// Gets the seed for the specified collection (Clans).
        /// </summary>
        public static int GetClansSeed()
        {
            try
            {
                var task = Task.Run(async () => await GameDb.Clans.GetMaxLowIdAsync(Config.ServerId));
                return task.Result;
            }
            catch (Exception ex)
            {
                Logging.Error(typeof(GameDb), $"Error getting clans seed: {ex.Message}");
                return 0;
            }
        }

        /// <summary>
        /// Gets the seed for the specified collection (Battles).
        /// </summary>
        public static int GetBattlesSeed()
        {
            try
            {
                var task = Task.Run(async () => await GameDb.Battles.GetMaxLowIdAsync(Config.ServerId));
                return task.Result;
            }
            catch (Exception ex)
            {
                Logging.Error(typeof(GameDb), $"Error getting battles seed: {ex.Message}");
                return 0;
            }
        }
    }
}