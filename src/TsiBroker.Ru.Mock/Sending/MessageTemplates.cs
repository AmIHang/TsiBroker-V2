namespace TsiBroker.Ru.Mock.Sending;

/// <summary>
/// Starter templates for the message types this mock initially supports sending, so testers
/// don't have to hand-write TAF/TAP XML for the common cases. Simplified - not the full/official
/// TAF/TAP schemas, just enough structure (a MessageHeader the broker's routing understands) to
/// be a useful, editable starting point. Dictionary keys and the payload's root element both use
/// the official TAF/TAP element name; MessageHeader/MessageReference/MessageType instead carries
/// the official TAF/TAP numeric message type code, which is what the broker's routing matches
/// against (see TsiMessageAuthorizationService - AllowedMessageTypesEvuToBroker).
/// </summary>
public static class MessageTemplates
{
    public static readonly IReadOnlyDictionary<string, string> ByMessageType = new Dictionary<string, string>
    {
        ["TrainCompositionMessage"] = TrainCompositionMessage,
        ["TrainReadyMessage"] = TrainReadyMessage,
    };

    private const string TrainCompositionMessage = """
        <TrainCompositionMessage>
          <MessageHeader>
            <MessageReference>
              <MessageType>3003</MessageType>
              <MessageIdentifier>REPLACE-WITH-UNIQUE-ID</MessageIdentifier>
            </MessageReference>
            <Sender>REPLACE-WITH-EVU-RICS</Sender>
            <Recipient>REPLACE-WITH-ISB-RICS</Recipient>
          </MessageHeader>
          <TrainComposition>
            <TrainIdentifier>
              <OperationalTrainNumber>REPLACE-WITH-TRAIN-NUMBER</OperationalTrainNumber>
              <StartDate>2026-08-07</StartDate>
            </TrainIdentifier>
            <Location>
              <PrimaryLocationCode>REPLACE-WITH-LOCATION-CODE</PrimaryLocationCode>
            </Location>
            <TotalLength>REPLACE-WITH-LENGTH-M</TotalLength>
            <TotalGrossWeight>REPLACE-WITH-WEIGHT-KG</TotalGrossWeight>
            <NumberOfWagons>REPLACE-WITH-WAGON-COUNT</NumberOfWagons>
            <Timestamp>2026-08-07T10:00:00Z</Timestamp>
          </TrainComposition>
        </TrainCompositionMessage>
        """;

    private const string TrainReadyMessage = """
        <TrainReadyMessage>
          <MessageHeader>
            <MessageReference>
              <MessageType>4500</MessageType>
              <MessageIdentifier>REPLACE-WITH-UNIQUE-ID</MessageIdentifier>
            </MessageReference>
            <Sender>REPLACE-WITH-EVU-RICS</Sender>
            <Recipient>REPLACE-WITH-ISB-RICS</Recipient>
          </MessageHeader>
          <TrainReady>
            <TrainIdentifier>
              <OperationalTrainNumber>REPLACE-WITH-TRAIN-NUMBER</OperationalTrainNumber>
              <StartDate>2026-08-07</StartDate>
            </TrainIdentifier>
            <Location>
              <PrimaryLocationCode>REPLACE-WITH-LOCATION-CODE</PrimaryLocationCode>
            </Location>
            <ReadyTimestamp>2026-08-07T10:00:00Z</ReadyTimestamp>
          </TrainReady>
        </TrainReadyMessage>
        """;
}
