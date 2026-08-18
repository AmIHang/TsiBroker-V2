using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.Options;
using TsiBroker.Core.Certificates;
using TsiBroker.Core.InfrastructureOperators;

namespace TsiBroker.ApiService.Certificates;

public record UpdateCertificateIdentityRequest(
    string? ExpectedServerCommonName,
    string? ExpectedClientCommonName,
    string? ClientCrlUrl,
    string? ServerCrlUrl);

public record UploadCertificateRequest(string CertificateBase64);

public static class CertificateEndpoints
{
    public static void MapCertificateEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/certificates").RequireAuthorization();

        group.MapGet("/{infrastructureOperatorId:guid}", async (
            Guid infrastructureOperatorId,
            CertificateBundleStore store) =>
        {
            var bundle = await store.FindByInfrastructureOperatorIdAsync(infrastructureOperatorId);
            return bundle is null ? Results.NotFound() : Results.Ok(bundle);
        });

        group.MapPut("/{infrastructureOperatorId:guid}", async (
            Guid infrastructureOperatorId,
            UpdateCertificateIdentityRequest request,
            InfrastructureOperatorStore infrastructureOperatorStore,
            CertificateBundleStore store) =>
        {
            if (await infrastructureOperatorStore.FindByIdAsync(infrastructureOperatorId) is null)
            {
                return Results.NotFound();
            }

            var bundle = await store.GetOrCreateForOperatorAsync(infrastructureOperatorId);
            var updated = await store.UpdateIdentityAsync(
                bundle.Id,
                NullIfWhiteSpace(request.ExpectedServerCommonName),
                NullIfWhiteSpace(request.ExpectedClientCommonName),
                NullIfWhiteSpace(request.ClientCrlUrl),
                NullIfWhiteSpace(request.ServerCrlUrl));
            return Results.Ok(updated);
        });

        group.MapPost("/{infrastructureOperatorId:guid}/client-certificate", (
            Guid infrastructureOperatorId,
            UploadCertificateRequest request,
            InfrastructureOperatorStore infrastructureOperatorStore,
            CertificateBundleStore store,
            IOptions<CertificateBundleStoreOptions> options) =>
            SaveCertificateAsync(
                infrastructureOperatorId,
                request,
                infrastructureOperatorStore,
                store,
                bytes => X509CertificateLoader.LoadPkcs12(bytes, options.Value.PfxPassword),
                store.SaveClientCertificateAsync));

        group.MapPost("/{infrastructureOperatorId:guid}/server-ca-certificate", (
            Guid infrastructureOperatorId,
            UploadCertificateRequest request,
            InfrastructureOperatorStore infrastructureOperatorStore,
            CertificateBundleStore store) =>
            SaveCertificateAsync(
                infrastructureOperatorId,
                request,
                infrastructureOperatorStore,
                store,
                X509CertificateLoader.LoadCertificate,
                store.SaveExpectedServerCaCertificateAsync));

        group.MapPost("/{infrastructureOperatorId:guid}/client-ca-certificate", (
            Guid infrastructureOperatorId,
            UploadCertificateRequest request,
            InfrastructureOperatorStore infrastructureOperatorStore,
            CertificateBundleStore store) =>
            SaveCertificateAsync(
                infrastructureOperatorId,
                request,
                infrastructureOperatorStore,
                store,
                X509CertificateLoader.LoadCertificate,
                store.SaveExpectedClientCaCertificateAsync));

        group.MapDelete("/{infrastructureOperatorId:guid}", async (
            Guid infrastructureOperatorId,
            CertificateBundleStore store) =>
        {
            var bundle = await store.FindByInfrastructureOperatorIdAsync(infrastructureOperatorId);
            if (bundle is null || !await store.DeleteAsync(bundle.Id))
            {
                return Results.NotFound();
            }

            return Results.Ok();
        });
    }

    // Shared by all three upload endpoints: validate the operator exists, decode the base64
    // payload, parse it as an X.509 certificate to reject garbage uploads before they're persisted
    // (rather than only failing later when something tries to load it), then hand the raw bytes to
    // the given store method to write to disk and update the bundle's metadata.
    private static async Task<IResult> SaveCertificateAsync(
        Guid infrastructureOperatorId,
        UploadCertificateRequest request,
        InfrastructureOperatorStore infrastructureOperatorStore,
        CertificateBundleStore store,
        Func<byte[], X509Certificate2> parse,
        Func<Guid, byte[], Task<PartnerCertificateBundle?>> save)
    {
        if (await infrastructureOperatorStore.FindByIdAsync(infrastructureOperatorId) is null)
        {
            return Results.NotFound();
        }

        byte[] certificateBytes;
        try
        {
            certificateBytes = Convert.FromBase64String(request.CertificateBase64);
        }
        catch (FormatException)
        {
            return Results.BadRequest(new { error = "invalid_base64" });
        }

        try
        {
            using var parsed = parse(certificateBytes);
        }
        catch (CryptographicException)
        {
            return Results.BadRequest(new { error = "invalid_certificate" });
        }

        var bundle = await store.GetOrCreateForOperatorAsync(infrastructureOperatorId);
        var updated = await save(bundle.Id, certificateBytes);
        return Results.Ok(updated);
    }

    private static string? NullIfWhiteSpace(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
