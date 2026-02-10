using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace MangaMesh.Shared.Stores
{
    public static class JsonFileStore
    {
        private static readonly SemaphoreSlim _lock = new(1, 1);

        public static async Task<List<T>> LoadAsync<T>(string path)
        {
            await _lock.WaitAsync();
            try
            {
                if (!File.Exists(path))
                    return new List<T>();

                var json = await File.ReadAllTextAsync(path);
                return JsonSerializer.Deserialize<List<T>>(json)
                       ?? new List<T>();
            }
            finally
            {
                _lock.Release();
            }
        }

        public static async Task<T> LoadSingleAsync<T>(string path)
        {
            await _lock.WaitAsync();
            try
            {
                if (!File.Exists(path))
                    return default;

                var json = await File.ReadAllTextAsync(path);
                return JsonSerializer.Deserialize<T>(json);
            }
            finally
            {
                _lock.Release();
            }
        }

        public static async Task SaveAsync<T>(string path, List<T> items)
        {
            await _lock.WaitAsync();
            try
            {
                var json = JsonSerializer.Serialize(
                    items,
                    new JsonSerializerOptions { WriteIndented = true }
                );

                await File.WriteAllTextAsync(path, json);
            }
            finally
            {
                _lock.Release();
            }
        }

        public static async Task SaveAsync<T>(string path, T item)
        {
            await _lock.WaitAsync();
            try
            {
                var json = JsonSerializer.Serialize(
                    item,
                    new JsonSerializerOptions { WriteIndented = true }
                );

                await File.WriteAllTextAsync(path, json);
            }
            finally
            {
                _lock.Release();
            }
        }
    }


}
