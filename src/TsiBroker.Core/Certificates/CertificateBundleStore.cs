using System.Text.Json;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace TsiBroker.Core.Certificates;

public class CertificateBundleStoreOptions
{
    public const string SectionName = "CertificateBundles";

    // Directory the JSON metadata file and the certificates/ subfolder are written to. Defaults to
    // App_Data under the content root (fine for local dev); in Docker, point this at a mounted
    // volume so certificates survive container recreation — same convention as
    // InfrastructureOperatorStoreOptions.DataDirectory.
    public string? DataDirectory { get; set; }

    // PFX (PKCS#12) password applied when opening every stored client certificate. Deliberately
    // kept out of the JSON metadata file — set via environment variable
    // (CertificateBundles__PfxPassword) or dotnet user-secrets locally, the same way
    // RabbitMqOptions.Password is kept out of appsettings.json.
    public string? PfxPassword { get; set; }

    // A certificate expiring within this many days triggers a warning from
    // CertificateExpiryMonitor, so rotation happens ahead of an outage rather than after one (spec
    // 4.3: certificate exchange "kann jederzeit ohne Abstimmung ... erfolgen" — partners aren't
    // obligated to announce rotation, so the broker needs its own early warning).
    public int ExpiryWarningThresholdDays { get; set; } = 30;
}

public class CertificateBundleStore
{
    private const string CertificatesSubdirectory = "certificates";

    private readonly string _metadataFilePath;
    private readonly string _certificatesDirectory;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public CertificateBundleStore(IHostEnvironment env, IOptions<CertificateBundleStoreOptions> options)
    {
        var dataDirectory = string.IsNullOrWhiteSpace(options.Value.DataDirectory)
            ? Path.Combine(env.ContentRootPath, "App_Data")
            : options.Value.DataDirectory;
        _metadataFilePath = Path.Combine(dataDirectory, "certificate-bundles.json");
        _certificatesDirectory = Path.Combine(dataDirectory, CertificatesSubdirectory);
    }

    public async Task<List<PartnerCertificateBundle>> GetAllAsync()
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

    public async Task<PartnerCertificateBundle?> FindByIdAsync(Guid id)
    {
        var bundles = await GetAllAsync();
        return bundles.FirstOrDefault(b => b.Id == id);
    }

    public async Task<PartnerCertificateBundle?> FindByInfrastructureOperatorIdAsync(Guid infrastructureOperatorId)
    {
        var bundles = await GetAllAsync();
        return bundles.FirstOrDefault(b => b.InfrastructureOperatorId == infrastructureOperatorId);
    }

    // Idempotent: returns the existing bundle for this operator if one is already there, since
    // every operator has exactly one certificate bundle (not a list).
    public async Task<PartnerCertificateBundle> GetOrCreateForOperatorAsync(Guid infrastructureOperatorId)
    {
        await _lock.WaitAsync();
        try
        {
            var bundles = await ReadAsync();
            var existing = bundles.FirstOrDefault(b => b.InfrastructureOperatorId == infrastructureOperatorId);
            if (existing is not null)
            {
                return existing;
            }

            var created = new PartnerCertificateBundle
            {
                Id = Guid.NewGuid(),
                InfrastructureOperatorId = infrastructureOperatorId,
            };
            bundles.Add(created);
            await WriteAsync(bundles);
            return created;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<PartnerCertificateBundle?> UpdateIdentityAsync(
        Guid id,
        string? expectedServerCommonName,
        string? expectedClientCommonName,
        string? clientCrlUrl,
        string? serverCrlUrl)
    {
        await _lock.WaitAsync();
        try
        {
            var bundles = await ReadAsync();
            var target = bundles.FirstOrDefault(b => b.Id == id);
            if (target is null)
            {
                return null;
            }

            target.ExpectedServerCommonName = expectedServerCommonName;
            target.ExpectedClientCommonName = expectedClientCommonName;
            target.ClientCrlUrl = clientCrlUrl;
            target.ServerCrlUrl = serverCrlUrl;
            await WriteAsync(bundles);
            return target;
        }
        finally
        {
            _lock.Release();
        }
    }

    // The three certificate slots (client cert, expected server CA, expected client CA) share this
    // one save path, keyed by which file name field to update — avoids three near-identical
    // read/write-file/update-metadata/rewrite-json methods for what's the same procedure with a
    // different destination field and file suffix.
    public Task<PartnerCertificateBundle?> SaveClientCertificateAsync(Guid id, byte[] certificateBytes) =>
        SaveCertificateFileAsync(id, certificateBytes, "client.pfx", (b, f) => b.ClientCertificateFileName = f);

    public Task<PartnerCertificateBundle?> SaveExpectedServerCaCertificateAsync(Guid id, byte[] certificateBytes) =>
        SaveCertificateFileAsync(id, certificateBytes, "server-ca.cer", (b, f) => b.ExpectedServerCaCertificateFileName = f);

    public Task<PartnerCertificateBundle?> SaveExpectedClientCaCertificateAsync(Guid id, byte[] certificateBytes) =>
        SaveCertificateFileAsync(id, certificateBytes, "client-ca.cer", (b, f) => b.ExpectedClientCaCertificateFileName = f);

    private async Task<PartnerCertificateBundle?> SaveCertificateFileAsync(
        Guid id,
        byte[] certificateBytes,
        string fileSuffix,
        Action<PartnerCertificateBundle, string> assignFileName)
    {
        await _lock.WaitAsync();
        try
        {
            var bundles = await ReadAsync();
            var target = bundles.FirstOrDefault(b => b.Id == id);
            if (target is null)
            {
                return null;
            }

            var fileName = $"{id}-{fileSuffix}";
            Directory.CreateDirectory(_certificatesDirectory);
            await File.WriteAllBytesAsync(Path.Combine(_certificatesDirectory, fileName), certificateBytes);

            assignFileName(target, fileName);
            await WriteAsync(bundles);
            return target;
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
            var bundles = await ReadAsync();
            var target = bundles.FirstOrDefault(b => b.Id == id);
            if (target is null)
            {
                return false;
            }

            bundles.Remove(target);
            await WriteAsync(bundles);

            foreach (var fileName in new[]
                     {
                         target.ClientCertificateFileName,
                         target.ExpectedServerCaCertificateFileName,
                         target.ExpectedClientCaCertificateFileName,
                     })
            {
                if (fileName is not null)
                {
                    File.Delete(Path.Combine(_certificatesDirectory, fileName));
                }
            }

            return true;
        }
        finally
        {
            _lock.Release();
        }
    }

    // Resolves a stored file name to its full path — used by PartnerCertificateProvider and
    // CertificateExpiryMonitor to load the actual X509Certificate2. Nothing is cached in memory
    // here, so a certificate swapped in on disk (or re-uploaded via the admin API) takes effect on
    // the very next call — the "Austausch ... kann jederzeit ... erfolgen" runtime-reload
    // requirement from spec 4.3, without needing any explicit invalidation mechanism.
    public string ResolveCertificatePath(string fileName) => Path.Combine(_certificatesDirectory, fileName);

    private async Task<List<PartnerCertificateBundle>> ReadAsync()
    {
        if (!File.Exists(_metadataFilePath))
        {
            return [];
        }

        await using var stream = File.OpenRead(_metadataFilePath);
        var bundles = await JsonSerializer.DeserializeAsync<List<PartnerCertificateBundle>>(stream);
        return bundles ?? [];
    }

    private async Task WriteAsync(List<PartnerCertificateBundle> bundles)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_metadataFilePath)!);
        await using var stream = File.Create(_metadataFilePath);
        await JsonSerializer.SerializeAsync(stream, bundles, new JsonSerializerOptions { WriteIndented = true });
    }
}
