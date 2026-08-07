namespace TsiBroker.Im.Mock.Sending;

public class CiClientOptions
{
    public const string SectionName = "CiClient";

    // Default target for outgoing sends (POST /api/send and the Out/ folder watcher) when the
    // caller doesn't specify one - normally TsiBroker.Im.Api's /ci endpoint.
    public string TargetUrl { get; set; } = "https://localhost:7262/ci";

    public string SenderAlias { get; set; } = "ISB-MOCK";

    // Prefilled into the UI's Sender (RICS) field - the RICS code this mock plays as.
    public string DefaultSenderRics { get; set; } = "8430";
}
