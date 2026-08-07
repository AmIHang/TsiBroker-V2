namespace TsiBroker.Im.Mock.Sending;

/// <summary>
/// Starter templates for the message types this mock initially supports sending, so testers
/// don't have to hand-write TAF/TAP XML for the common cases. Simplified - not the full/official
/// TAF/TAP schemas, just enough structure (a MessageHeader the broker's routing understands) to
/// be a useful, editable starting point. Message type names match what the broker's routing
/// expects verbatim (see TsiMessageAuthorizationService - AllowedMessageTypesEvuToBroker).
/// </summary>
public static class MessageTemplates
{
    public static readonly IReadOnlyDictionary<string, string> ByMessageType = new Dictionary<string, string>
    {
        ["ChangeOfTrackMessage"] = ChangeOfTrackMessage,
        ["DelayCauseMessageService"] = DelayCauseMessageService,
        ["TrainRunningForcecastMessage"] = TrainRunningForcecastMessage,
        ["TrainRunningInformationMessage"] = TrainRunningInformationMessage,
        ["TrainRunningInterruptionMessage"] = TrainRunningInterruptionMessage,
    };

    private const string ChangeOfTrackMessage = """
        <ChangeOfTrackMessage>
          <MessageHeader>
            <MessageReference>
              <MessageType>ChangeOfTrackMessage</MessageType>
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

    private const string DelayCauseMessageService = """
        <DelayCauseMessageService>
          <MessageHeader>
            <MessageReference>
              <MessageType>DelayCauseMessageService</MessageType>
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
        </DelayCauseMessageService>
        """;

    private const string TrainRunningForcecastMessage = """
        <TrainRunningForcecastMessage>
          <MessageHeader>
            <MessageReference>
              <MessageType>TrainRunningForcecastMessage</MessageType>
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
        </TrainRunningForcecastMessage>
        """;

    private const string TrainRunningInformationMessage = """
        <TrainRunningInformationMessage>
          <MessageHeader>
            <MessageReference>
              <MessageType>TrainRunningInformationMessage</MessageType>
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
              <MessageType>TrainRunningInterruptionMessage</MessageType>
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
