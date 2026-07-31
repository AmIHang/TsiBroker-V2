using Microsoft.Extensions.Logging;
using System.Xml;
using TsiBroker.Im.Api.Heartbeat.Contracts;

namespace TsiBroker.Im.Api.Heartbeat;

public class HeartbeatMessageService(ILogger<HeartbeatMessageService> logger) : IHeartbeatMessageService
{
    private const string ReceivedResponse = "HEART_BEAT_WS_RECEIVED";

    public Task<HeartbeatResponse> ReceiveAsync(HeartbeatRequest request)
    {
        if (request.Message is null)
        {
            throw new ArgumentException(
                "The SOAP body does not contain a message element.");
        }

        logger.LogInformation(
            "Received heartbeat message {Message}",
            request.Message.InnerText);

        return Task.FromResult(new HeartbeatResponse
        {
            Return =
            [
                CreateResultElement()
            ]
        });
    }

    private static XmlElement CreateResultElement()
    {
        var document = new XmlDocument();

        var element = document.CreateElement("return");
        element.InnerText = ReceivedResponse;

        return element;
    }
}
