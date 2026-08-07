using TsiBroker.Ru.Mock.Storage;

namespace TsiBroker.Ru.Mock.Receiving;

/// <summary>
/// Plays the EVU side of the broker's config-update notification (see EvuApiClient and
/// RailwayUndertakingEndpoints' POST /{id}/trigger-config-update in TsiBroker.ApiService) - the
/// one broker -> EVU call that is actually wired up today, unlike /message. Stateless: the mock
/// has no local config to refresh, so a 2xx here just proves the broker reached this URL. See
/// infrastructure/evu-endpoints.openapi.yaml for the documented contract - keep both in sync
/// whenever either changes.
/// </summary>
public static class ReceiveConfigUpdateEndpoints
{
    public static void MapReceiveConfigUpdateEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/config/update", async (
            MockMessageStore store,
            ResponseConfigStore configStore,
            CancellationToken cancellationToken) =>
        {
            const string content = "<ConfigUpdateNotification/>";
            const string messageIdentifier = "config-update";

            var config = configStore.Current;
            if (config.DelayMs > 0)
            {
                await Task.Delay(config.DelayMs, cancellationToken);
            }

            if (config.Mode == MockResponseMode.HttpError)
            {
                await store.SaveReceivedAsync(content, messageIdentifier, "HttpError", cancellationToken);
                return Results.Problem(
                    title: "Simulated EVU failure",
                    detail: "The mock is currently configured to fail every incoming message.",
                    statusCode: StatusCodes.Status500InternalServerError);
            }

            if (config.Mode == MockResponseMode.Unauthorized)
            {
                await store.SaveReceivedAsync(content, messageIdentifier, "Unauthorized", cancellationToken);
                return Results.Problem(
                    title: "Simulated invalid API key",
                    detail: "The mock is currently configured to reject every incoming message as if the "
                        + "broker presented an invalid or missing API key.",
                    statusCode: StatusCodes.Status401Unauthorized);
            }

            if (config.Mode == MockResponseMode.Forbidden)
            {
                await store.SaveReceivedAsync(content, messageIdentifier, "Forbidden", cancellationToken);
                return Results.Problem(
                    title: "Simulated missing permissions",
                    detail: "The mock is currently configured to reject every incoming message as not "
                        + "authorized (message type not allowed for this EVU/ISB assignment).",
                    statusCode: StatusCodes.Status403Forbidden);
            }

            if (config.Mode == MockResponseMode.Nack)
            {
                // Unlike /message, this route has no response body to carry a NACK status in -
                // the broker only looks at the HTTP status code (see EvuApiClient). A distinct
                // 409 keeps Nack tellable apart from HttpError's 500 when watching status codes.
                await store.SaveReceivedAsync(content, messageIdentifier, "NACK", cancellationToken);
                return Results.Problem(
                    title: "Simulated negative acknowledgement",
                    detail: "The mock is currently configured to reject every incoming config-update notification.",
                    statusCode: StatusCodes.Status409Conflict);
            }

            await store.SaveReceivedAsync(content, messageIdentifier, "ACK", cancellationToken);
            return Results.Ok();
        });
    }
}
