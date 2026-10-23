namespace ClashRoyale.Database
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Newtonsoft.Json;

    /// <summary>
    /// Generic JSON repository for file-based storage operations.
    /// </summary>
    /// <typeparam name="T">The type of entity to store.</typeparam>
    public class JsonRepository<T> where T : class
    {
        private static readonly JsonSerializerSettings JsonSettings = new JsonSerializerSettings
        {
            ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
            NullValueHandling = NullValueHandling.Ignore,
            MissingMemberHandling = MissingMemberHandling.Ignore
        };

        private readonly SemaphoreSlim _writeLock = new SemaphoreSlim(1, 1);
        private readonly string _folderPath;
        private readonly string _collectionName;

        public JsonRepository(string basePath, string collectionName)
        {
            _collectionName = collectionName;
            _folderPath = Path.Combine(basePath, collectionName);

            // Create directory if it doesn't exist
            if (!Directory.Exists(_folderPath))
            {
                Directory.CreateDirectory(_folderPath);
            }
        }

        /// <summary>
        /// Gets the file path for an entity based on its ID.
        /// </summary>
        private string GetFilePath(int highId, int lowId)
        {
            return Path.Combine(_folderPath, $"{highId}_{lowId}.json");
        }

        /// <summary>
        /// Inserts a new entity.
        /// </summary>
        public async Task InsertOneAsync(T entity)
        {
            try
            {
                var filePath = GetFilePath(GetId(entity, "HighId"), GetId(entity, "LowId"));
                var json = JsonConvert.SerializeObject(entity, Formatting.Indented, JsonSettings);

                await WriteFileAsync(filePath, json);
            }
            catch (Exception ex)
            {
                Logging.Error(typeof(JsonRepository<T>), $"Error in InsertOneAsync: {ex.Message}");
                throw;
            }
        }

        // Writes to a temp file then swaps it in, so a crash or concurrent read never sees a half-written file.
        private async Task WriteFileAsync(string filePath, string json)
        {
            await _writeLock.WaitAsync();

            try
            {
                var tempPath = filePath + ".tmp";
                await File.WriteAllTextAsync(tempPath, json);
                File.Move(tempPath, filePath, true);
            }
            finally
            {
                _writeLock.Release();
            }
        }

        /// <summary>
        /// Finds an entity by high and low ID.
        /// </summary>
        public async Task<T> FindAsync(int highId, int lowId)
        {
            try
            {
                var filePath = GetFilePath(highId, lowId);

                if (!File.Exists(filePath))
                {
                    return null;
                }

                var json = await File.ReadAllTextAsync(filePath);
                return JsonConvert.DeserializeObject<T>(json, JsonSettings);
            }
            catch (Exception ex)
            {
                Logging.Error(typeof(JsonRepository<T>), $"Error in FindAsync: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Finds and updates an entity.
        /// </summary>
        public async Task<T> FindOneAndUpdateAsync(int highId, int lowId, T updatedEntity)
        {
            try
            {
                var filePath = GetFilePath(highId, lowId);
                var json = JsonConvert.SerializeObject(updatedEntity, Formatting.Indented, JsonSettings);

                await WriteFileAsync(filePath, json);
                return updatedEntity;
            }
            catch (Exception ex)
            {
                Logging.Error(typeof(JsonRepository<T>), $"Error in FindOneAndUpdateAsync: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Deletes an entity by ID.
        /// </summary>
        public async Task<bool> DeleteOneAsync(int highId, int lowId)
        {
            try
            {
                var filePath = GetFilePath(highId, lowId);

                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                Logging.Error(typeof(JsonRepository<T>), $"Error in DeleteOneAsync: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Gets the highest low ID for a given high ID (used for seed generation).
        /// </summary>
        public async Task<int> GetMaxLowIdAsync(int highId)
        {
            try
            {
                if (!Directory.Exists(_folderPath))
                {
                    return 0;
                }

                var files = Directory.GetFiles(_folderPath, $"{highId}_*.json");

                if (files.Length == 0)
                {
                    return 0;
                }

                var maxLowId = 0;
                foreach (var file in files)
                {
                    var fileName = Path.GetFileNameWithoutExtension(file);
                    var parts = fileName.Split('_');
                    if (parts.Length == 2 && int.TryParse(parts[1], out var lowId))
                    {
                        maxLowId = Math.Max(maxLowId, lowId);
                    }
                }

                return maxLowId;
            }
            catch (Exception ex)
            {
                Logging.Error(typeof(JsonRepository<T>), $"Error in GetMaxLowIdAsync: {ex.Message}");
                return 0;
            }
        }

        /// <summary>
        /// Gets all entities (used for admin purposes or migrations).
        /// </summary>
        public async Task<List<T>> GetAllAsync()
        {
            try
            {
                var entities = new List<T>();

                if (!Directory.Exists(_folderPath))
                {
                    return entities;
                }

                var files = Directory.GetFiles(_folderPath, "*.json");

                foreach (var file in files)
                {
                    try
                    {
                        var json = await File.ReadAllTextAsync(file);
                        var entity = JsonConvert.DeserializeObject<T>(json, JsonSettings);
                        if (entity != null)
                        {
                            entities.Add(entity);
                        }
                    }
                    catch (Exception ex)
                    {
                        Logging.Warning(typeof(JsonRepository<T>), $"Error reading file {file}: {ex.Message}");
                    }
                }

                return entities;
            }
            catch (Exception ex)
            {
                Logging.Error(typeof(JsonRepository<T>), $"Error in GetAllAsync: {ex.Message}");
                return new List<T>();
            }
        }

        /// <summary>
        /// Helper method to get property value using reflection.
        /// </summary>
        private static int GetId(T entity, string memberName)
        {
            object value = typeof(T).GetField(memberName)?.GetValue(entity) ?? typeof(T).GetProperty(memberName)?.GetValue(entity);

            if (value == null)
            {
                throw new InvalidOperationException(typeof(T).Name + " has no " + memberName + " field or property.");
            }

            return Convert.ToInt32(value);
        }
    }
}
