using TsiBroker.Ru.Mock.Storage;

namespace TsiBroker.Ru.Mock.Messages;

public static class MessagesEndpoints
{
    public static void MapMessagesEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/messages", (MockMessageStore store) => Results.Ok(store.List()))
            .RequireAuthorization();

        app.MapPost("/api/messages/reset", (MockMessageStore store) =>
        {
            store.Clear();
            return Results.NoContent();
        }).RequireAuthorization();
    }
}
