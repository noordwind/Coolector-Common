using System;
using System.Threading.Tasks;
using Collectively.Common.Types;
using Collectively.Common.Extensions;
using Newtonsoft.Json;
using StackExchange.Redis;
using System.Collections.Generic;
using System.Linq;

namespace Collectively.Common.Caching
{
    public class RedisCache : ICache
    {
        private readonly IDatabase _database;
        private readonly RedisSettings _settings;
        private bool Available => _database != null && _settings.Enabled;

        public RedisCache(Maybe<RedisDatabase> database, RedisSettings settings)
        {
            _database = database.HasValue ? database.Value.Database : null;
            _settings = settings;
        }

        public async Task<Maybe<T>> GetAsync<T>(string key) where T : class
            => Available ? Deserialize<T>(await _database.StringGetAsync(GetKey(key))) : default(T);

        public async Task<IEnumerable<T>> GetManyAsync<T>(params string[] keys) where T : class
        {
            if (!Available)
            {
                return Enumerable.Empty<T>();
            }
            if (keys == null || !keys.Any())
            {
                return Enumerable.Empty<T>();
            }
            var results = await _database.StringGetAsync(keys.Select(x => (RedisKey)GetKey(x)).ToArray());
            var values = new List<T>();
            foreach (var result in results)
            {
                values.Add(Deserialize<T>(result));
            }
            
            return values;
        }

        public async Task AddAsync(string key, object value, TimeSpan? expiry = null)
        {
            if (!Available)
            {
                return;
            }
            await _database.StringSetAsync(GetKey(key), Serialize(value), expiry);
        }

        public async Task GeoAddAsync(string key, double longitude, double latitude, string name)
        {
            if (!Available)
            {
                return;
            }
            await _database.GeoAddAsync(GetKey(key), longitude, latitude, name);
        }

        public async Task GeoRemoveAsync(string key, string name)
        {
            if (!Available)
            {
                return;
            }
            await _database.GeoRemoveAsync(GetKey(key), name);
        }

        public async Task<IEnumerable<GeoResult>> GetGeoRadiusAsync(string key, double longitude, double latitude, double radius)
        {
            if (!Available)
            {
                return Enumerable.Empty<GeoResult>();
            }
            var results = await _database.GeoRadiusAsync(GetKey(key), longitude, latitude, radius);
            if (results == null)
            {
                return Enumerable.Empty<GeoResult>();
            }

            return results.Select(x => new GeoResult
            {
                Name = x.Member.ToString(),
                Distance = x.Distance,
                Longitude = x.Position?.Longitude,
                Latitude = x.Position?.Latitude
            });
        }

        public async Task DeleteAsync(string key)
        {
            if (!Available)
            {
                return;
            }
            await AddAsync(GetKey(key), null, TimeSpan.FromMilliseconds(1));
        }

        private static string GetKey(string key) => key.ToLowerInvariant();

        private static string Serialize<T>(T value) => JsonConvert.SerializeObject(value);

        private static T Deserialize<T>(string serializedObject)
            => serializedObject.Empty() ? default(T) : JsonConvert.DeserializeObject<T>(serializedObject);

    }
}