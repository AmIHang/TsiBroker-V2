namespace TsiBroker.Im.Mock.Sending;

/// <summary>
/// Starter templates for the message types this mock initially supports sending, so testers
/// don't have to hand-write TAF/TAP XML for the common cases. Simplified - not the full/official
/// TAF/TAP schemas, just enough structure (a MessageHeader the broker's routing understands) to
/// be a useful, editable starting point. Dictionary keys and the payload's root element both use
/// the official TAF/TAP element name (see TsiBroker.Im.Mock.UI/src/lib/messageXml.ts -
/// extractMessageType, which reads that root element back off a logged message); MessageHeader/
/// MessageReference/MessageType instead carries the official TAF/TAP numeric message type code,
/// which is what the broker's routing matches against (see TsiMessageAuthorizationService -
/// AllowedMessageTypesEvuToBroker).
/// </summary>
public static class MessageTemplates
{
    public static readonly IReadOnlyDictionary<string, string> ByMessageType = new Dictionary<string, string>
    {
        ["ChangeOfTrackMessage"] = ChangeOfTrackMessage,
        ["TrainDelayCauseMessage"] = TrainDelayCauseMessage,
        ["TrainRunningForecastMessage"] = TrainRunningForecastMessage,
        ["TrainRunningInformationMessage"] = TrainRunningInformationMessage,
        ["TrainRunningInterruptionMessage"] = TrainRunningInterruptionMessage,
    };

    private const string ChangeOfTrackMessage = """
        <ChangeOfTrackMessage>
          <MessageHeader>
            <MessageReference>
              <MessageType>4504</MessageType>
              <MessageIdentifier>REPLACE-WITH-UNIQUE-ID</MessageIdentifier>
            </MessageReference>
            <Sender>REPLACE-WITH-ISB-RICS</Sender>
            <Recipient>REPLACE-WITH-EVU-RICS</Recipient>
          </MessageHeader>
          <ChangeOfTrackInformation>
            <TrainIdentifier>
              <OperationalTrainNumber>REPLACE-WITH-TRAIN-NUMBER</OperationalTrainNumber>
              <StartDate>2026-08-07</StartDate>
            </TrainIdentifier>
            <Location>
              <PrimaryLocationCode>REPLACE-WITH-LOCATION-CODE</PrimaryLocationCode>
            </Location>
            <PlannedTrack>REPLACE-WITH-PLANNED-TRACK</PlannedTrack>
            <NewTrack>REPLACE-WITH-NEW-TRACK</NewTrack>
            <Timestamp>2026-08-07T10:00:00Z</Timestamp>
          </ChangeOfTrackInformation>
        </ChangeOfTrackMessage>
        """;

    private const string TrainDelayCauseMessage = """
        <TrainDelayCauseMessage>
          <MessageHeader>
            <MessageReference>
              <MessageType>4001</MessageType>
              <MessageIdentifier>REPLACE-WITH-UNIQUE-ID</MessageIdentifier>
            </MessageReference>
            <Sender>REPLACE-WITH-ISB-RICS</Sender>
            <Recipient>REPLACE-WITH-EVU-RICS</Recipient>
          </MessageHeader>
          <DelayCauseInformation>
            <TrainIdentifier>
              <OperationalTrainNumber>REPLACE-WITH-TRAIN-NUMBER</OperationalTrainNumber>
              <StartDate>2026-08-07</StartDate>
            </TrainIdentifier>
            <Location>
              <PrimaryLocationCode>REPLACE-WITH-LOCATION-CODE</PrimaryLocationCode>
            </Location>
            <DelayCauseCode>REPLACE-WITH-CAUSE-CODE</DelayCauseCode>
            <DelayCauseText>REPLACE-WITH-CAUSE-TEXT</DelayCauseText>
            <Delay>PT0M</Delay>
            <Timestamp>2026-08-07T10:00:00Z</Timestamp>
          </DelayCauseInformation>
        </TrainDelayCauseMessage>
        """;

    private const string TrainRunningForecastMessage = """
        <TrainRunningForecastMessage>
          <MessageHeader>
            <MessageReference>
              <MessageType>4004</MessageType>
              <MessageIdentifier>REPLACE-WITH-UNIQUE-ID</MessageIdentifier>
            </MessageReference>
            <Sender>REPLACE-WITH-ISB-RICS</Sender>
            <Recipient>REPLACE-WITH-EVU-RICS</Recipient>
          </MessageHeader>
          <TrainRunningForecast>
            <TrainIdentifier>
              <OperationalTrainNumber>REPLACE-WITH-TRAIN-NUMBER</OperationalTrainNumber>
              <StartDate>2026-08-07</StartDate>
            </TrainIdentifier>
            <Location>
              <PrimaryLocationCode>REPLACE-WITH-LOCATION-CODE</PrimaryLocationCode>
            </Location>
            <ForecastTimestamp>2026-08-07T10:00:00Z</ForecastTimestamp>
            <ForecastDelay>PT0M</ForecastDelay>
          </TrainRunningForecast>
        </TrainRunningForecastMessage>
        """;

    private const string TrainRunningInformationMessage = """
        <TrainRunningInformationMessage>
          <MessageHeader>
            <MessageReference>
              <MessageType>4005</MessageType>
              <MessageIdentifier>REPLACE-WITH-UNIQUE-ID</MessageIdentifier>
            </MessageReference>
            <Sender>REPLACE-WITH-ISB-RICS</Sender>
            <Recipient>REPLACE-WITH-EVU-RICS</Recipient>
          </MessageHeader>
          <TrainRunningInformation>
            <TrainIdentifier>
              <OperationalTrainNumber>REPLACE-WITH-TRAIN-NUMBER</OperationalTrainNumber>
              <StartDate>2026-08-07</StartDate>
            </TrainIdentifier>
            <Location>
              <PrimaryLocationCode>REPLACE-WITH-LOCATION-CODE</PrimaryLocationCode>
            </Location>
            <Timestamp>2026-08-07T10:00:00Z</Timestamp>
            <Delay>PT0M</Delay>
          </TrainRunningInformation>
        </TrainRunningInformationMessage>
        """;

    private const string TrainRunningInterruptionMessage = """
        <TrainRunningInterruptionMessage>
          <MessageHeader>
            <MessageReference>
              <MessageType>4006</MessageType>
              <MessageIdentifier>REPLACE-WITH-UNIQUE-ID</MessageIdentifier>
            </MessageReference>
            <Sender>REPLACE-WITH-ISB-RICS</Sender>
            <Recipient>REPLACE-WITH-EVU-RICS</Recipient>
          </MessageHeader>
          <TrainRunningInterruption>
            <TrainIdentifier>
              <OperationalTrainNumber>REPLACE-WITH-TRAIN-NUMBER</OperationalTrainNumber>
              <StartDate>2026-08-07</StartDate>
            </TrainIdentifier>
            <Location>
              <PrimaryLocationCode>REPLACE-WITH-LOCATION-CODE</PrimaryLocationCode>
            </Location>
            <InterruptionReason>REPLACE-WITH-REASON</InterruptionReason>
            <Timestamp>2026-08-07T10:00:00Z</Timestamp>
          </TrainRunningInterruption>
        </TrainRunningInterruptionMessage>
        """;
}
