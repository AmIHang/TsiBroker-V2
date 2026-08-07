namespace TsiBroker.Ru.Mock.Sending;

public record RuSendResult(bool Success, string Status, string? ResponseBody, string? Error);
