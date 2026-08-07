namespace TsiBroker.Ru.Mock.Storage;

public class MockMessageStoreOptions
{
    public const string SectionName = "MockMessageStore";

    // Directory holding the In/Out/Sent subfolders. Defaults to App_Data under the content root.
    public string? DataDirectory { get; set; }
}
