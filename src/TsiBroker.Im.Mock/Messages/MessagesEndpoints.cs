using TsiBroker.Im.Mock.Storage;

namespace TsiBroker.Im.Mock.Messages;

public static class MessagesEndpoints
{
    public static void MapMessagesEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/messages", (MockMessageStore store) => Results.Ok(store.List()))
            .RequireAuthorization();

        app.MapPost("/api/messages/reset", (MockMessageStore store) =>
        {
            store.ResetOnStartup();
            return Results.NoContent();
        }).RequireAuthorization();
    }
}
