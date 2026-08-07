namespace TsiBroker.Ru.Mock.Sending;

public class RuClientOptions
{
    public const string SectionName = "RuClient";

    // Default target for outgoing sends (POST /api/send and the Out/ folder watcher) when the
    // caller doesn't specify one - normally TsiBroker.Ru.Api's /message endpoint.
    public string TargetUrl { get; set; } = "https://localhost:7263/message";

    // Default target for the WhoAmI config check (POST /api/whoami) - a separate route on the
    // same broker (GET, no payload), not something /message's TargetUrl can double as. Add
    // further *Url options here the same way as more of TsiBroker.Ru.Api's routes get a mock
    // counterpart.
    public string WhoAmIUrl { get; set; } = "https://localhost:7263/whoami";

    // X-Api-Key sent with outgoing sends - the ApiKeyEvuToBroker of the RailwayUndertaking this
    // mock plays. Must be set (via appsettings/env or the UI's API key field) to authenticate
    // against a real TsiBroker.Ru.Api - see TsiMessageAuthorizationService.
    public string ApiKey { get; set; } = string.Empty;

    // Prefilled into the UI's Sender (RICS) field - the RICS code this mock plays as.
    public string DefaultSenderRics { get; set; } = "0080";
}
