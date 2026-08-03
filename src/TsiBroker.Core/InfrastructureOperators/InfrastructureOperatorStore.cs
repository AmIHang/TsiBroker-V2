using System.Text.Json;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace TsiBroker.Core.InfrastructureOperators;

public class InfrastructureOperatorStoreOptions
{
    public const string SectionName = "InfrastructureOperators";

    // Directory the JSON data file is written to. Defaults to App_Data under the content root
    // (fine for local dev); in Docker, point this at a mounted volume so data survives container recreation.
    public string? DataDirectory { get; set; }
}

public class InfrastructureOperatorStore
{
    private readonly string _filePath;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public InfrastructureOperatorStore(IHostEnvironment env, IOptions<InfrastructureOperatorStoreOptions> options)
    {
        var dataDirectory = string.IsNullOrWhiteSpace(options.Value.DataDirectory)
            ? Path.Combine(env.ContentRootPath, "App_Data")
            : options.Value.DataDirectory;
        _filePath = Path.Combine(dataDirectory, "infrastructure-operators.json");
    }

    public async Task<List<InfrastructureOperator>> GetAllAsync()
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

    public async Task<InfrastructureOperator?> FindByRicsCodeAsync(string ricsCode)
    {
        var operators = await GetAllAsync();
        return operators.FirstOrDefault(o =>
            string.Equals(o.RicsCode, ricsCode, StringComparison.OrdinalIgnoreCase));
    }

    public async Task<InfrastructureOperator> AddAsync(string name, string ricsCode, string systemUrl)
    {
        await _lock.WaitAsync();
        try
        {
            var operators = await ReadAsync();
            var created = new InfrastructureOperator
            {
                Id = Guid.NewGuid(),
                Name = name,
                RicsCode = ricsCode,
                SystemUrl = systemUrl,
                IsActive = true,
            };
            operators.Add(created);
            await WriteAsync(operators);
            return created;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<InfrastructureOperator?> UpdateAsync(Guid id, string name, string ricsCode, string systemUrl)
    {
        await _lock.WaitAsync();
        try
        {
            var operators = await ReadAsync();
            var target = operators.FirstOrDefault(o => o.Id == id);
            if (target is null)
            {
                return null;
            }

            target.Name = name;
            target.RicsCode = ricsCode;
            target.SystemUrl = systemUrl;
            await WriteAsync(operators);
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
            var operators = await ReadAsync();
            var target = operators.FirstOrDefault(o => o.Id == id);
            if (target is null)
            {
                return false;
            }

            target.IsActive = isActive;
            await WriteAsync(operators);
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
            var operators = await ReadAsync();
            if (operators.RemoveAll(o => o.Id == id) == 0)
            {
                return false;
            }

            await WriteAsync(operators);
            return true;
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task<List<InfrastructureOperator>> ReadAsync()
    {
        if (!File.Exists(_filePath))
        {
            return [];
        }

        await using var stream = File.OpenRead(_filePath);
        var operators = await JsonSerializer.DeserializeAsync<List<InfrastructureOperator>>(stream);
        return operators ?? [];
    }

    private async Task WriteAsync(List<InfrastructureOperator> operators)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_filePath)!);
        await using var stream = File.Create(_filePath);
        await JsonSerializer.SerializeAsync(stream, operators, new JsonSerializerOptions { WriteIndented = true });
    }
}
