namespace TsiBroker.Im.Mock.Sending;

public record CiSendResult(bool Success, string Status, string? ResponseXml, string? Error);
