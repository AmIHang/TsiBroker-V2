using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Extensions.Options;
using TsiBroker.Core.RailwayUndertakings;

namespace TsiBroker.ApiService.RailwayUndertakings;

public class RailwayUndertakingStoreOptions
{
    public const string SectionName = "RailwayUndertakings";

    // Directory the JSON data file is written to. Defaults to App_Data under the content root
    // (fine for local dev); in Docker, point this at a mounted volume so data survives container recreation.
    public string? DataDirectory { get; set; }
}

public class RailwayUndertakingStore
{
    private readonly string _filePath;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public RailwayUndertakingStore(IWebHostEnvironment env, IOptions<RailwayUndertakingStoreOptions> options)
    {
        var dataDirectory = string.IsNullOrWhiteSpace(options.Value.DataDirectory)
            ? Path.Combine(env.ContentRootPath, "App_Data")
            : options.Value.DataDirectory;
        _filePath = Path.Combine(dataDirectory, "railway-undertakings.json");
    }

    public async Task<List<RailwayUndertaking>> GetAllAsync()
    {
        await _lock.WaitAsync();
        try
        {
            return await ReadAsync();
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<RailwayUndertaking> AddAsync(
        string name,
        List<string> ricsCodes,
        string systemUrl,
        List<IsbAssignment> infrastructureOperatorAssignments)
    {
        await _lock.WaitAsync();
        try
        {
            var undertakings = await ReadAsync();
            var created = new RailwayUndertaking
            {
                Id = Guid.NewGuid(),
                Name = name,
                RicsCodes = ricsCodes,
                SystemUrl = systemUrl,
                ApiKeyEvuToBroker = GenerateApiKey(),
                ApiKeyBrokerToEvu = GenerateApiKey(),
                InfrastructureOperatorAssignments = infrastructureOperatorAssignments,
                IsActive = true,
            };
            undertakings.Add(created);
            await WriteAsync(undertakings);
            return created;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<RailwayUndertaking?> UpdateAsync(
        Guid id,
        string name,
        List<string> ricsCodes,
        string systemUrl,
        string apiKeyEvuToBroker,
        string apiKeyBrokerToEvu,
        List<IsbAssignment> infrastructureOperatorAssignments)
    {
        await _lock.WaitAsync();
        try
        {
            var undertakings = await ReadAsync();
            var target = undertakings.FirstOrDefault(u => u.Id == id);
            if (target is null)
            {
                return null;
            }

            target.Name = name;
            target.RicsCodes = ricsCodes;
            target.SystemUrl = systemUrl;
            target.ApiKeyEvuToBroker = apiKeyEvuToBroker;
            target.ApiKeyBrokerToEvu = apiKeyBrokerToEvu;
            target.InfrastructureOperatorAssignments = infrastructureOperatorAssignments;
            await WriteAsync(undertakings);
            return target;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<RailwayUndertaking?> RegenerateApiKeyEvuToBrokerAsync(Guid id)
    {
        await _lock.WaitAsync();
        try
        {
            var undertakings = await ReadAsync();
            var target = undertakings.FirstOrDefault(u => u.Id == id);
            if (target is null)
            {
                return null;
            }

            target.ApiKeyEvuToBroker = GenerateApiKey();
            await WriteAsync(undertakings);
            return target;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<RailwayUndertaking?> RegenerateApiKeyBrokerToEvuAsync(Guid id)
    {
        await _lock.WaitAsync();
        try
        {
            var undertakings = await ReadAsync();
            var target = undertakings.FirstOrDefault(u => u.Id == id);
            if (target is null)
            {
                return null;
            }

            target.ApiKeyBrokerToEvu = GenerateApiKey();
            await WriteAsync(undertakings);
            return target;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<bool> SetActiveAsync(Guid id, bool isActive)
    {
        await _lock.WaitAsync();
        try
        {
            var undertakings = await ReadAsync();
            var target = undertakings.FirstOrDefault(u => u.Id == id);
            if (target is null)
            {
                return false;
            }

            target.IsActive = isActive;
            await WriteAsync(undertakings);
            return true;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        await _lock.WaitAsync();
        try
        {
            var undertakings = await ReadAsync();
            if (undertakings.RemoveAll(u => u.Id == id) == 0)
            {
                return false;
            }

            await WriteAsync(undertakings);
            return true;
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task<List<RailwayUndertaking>> ReadAsync()
    {
        if (!File.Exists(_filePath))
        {
            return [];
        }

        await using var stream = File.OpenRead(_filePath);
        var undertakings = await JsonSerializer.DeserializeAsync<List<RailwayUndertaking>>(stream);
        return undertakings ?? [];
    }

    private async Task WriteAsync(List<RailwayUndertaking> undertakings)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_filePath)!);
        await using var stream = File.Create(_filePath);
        await JsonSerializer.SerializeAsync(stream, undertakings, new JsonSerializerOptions { WriteIndented = true });
    }

    // Alphanumeric plus a curated set of special characters that never need escaping in JSON or XML
    // (excludes " ' < > & \ and whitespace/control characters).
    private const string ApiKeyAlphabet =
        "ABCDEFGHIJKLMNOPQRSTUVWXYZ" +
        "abcdefghijklmnopqrstuvwxyz" +
        "0123456789" +
        "!#$%()*+,-./:;=?@[]^_{|}~";

    private static string GenerateApiKey() => RandomNumberGenerator.GetString(ApiKeyAlphabet, 40);
}
