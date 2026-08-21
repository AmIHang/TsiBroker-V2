namespace TsiBroker.Im.Mock.Sending;

/// <summary>
/// Starter templates for the message types this mock initially supports sending, so testers
/// don't have to hand-write TAF/TAP XML for the common cases. These follow the official ERA
/// TAF/TSI schema (namespace http://www.era.europa.eu/schemes/TAFTSI/3.4, element names/nesting/
/// order matching the xsd.exe-generated classes real EVU systems deserialize against - e.g.
/// Community.Operations.Interfaces...Era.Tsi.Generated.ChangeofTrackMessage) rather than an
/// ad-hoc simplification, since real customer systems parse this with a strict XmlSerializer.
/// Only elements the schema marks required (no matching "...Specified" opt-out property) are
/// included, plus enough of the optional structure for the one flow we've verified end-to-end
/// (ChangeOfTrack); deeply optional data (brakes, traction, GNSS, wagon telemetry, ...) is left
/// out to keep these editable rather than exhaustive. No XML attributes are used anywhere (e.g.
/// MessageHeader/Sender's optional CI_InstanceNumber attribute is omitted) so the existing
/// attribute-free regex tooling (see TsiBroker.Im.Mock.UI/src/lib/messageXml.ts and
/// SendMessageView.vue's applyRicsToPayload/replaceTagContent) keeps working unchanged.
/// Dictionary keys and the payload's root element both use the official TAF/TAP element name;
/// MessageHeader/MessageReference/MessageType carries the official numeric message type code,
/// which is what the broker's routing matches against (see TsiMessageAuthorizationService -
/// AllowedMessageTypesEvuToBroker).
/// </summary>
public static class MessageTemplates
{
    public static readonly IReadOnlyDictionary<string, string> ByMessageType = new Dictionary<string, string>
    {
        ["ChangeofTrackMessage"] = ChangeOfTrackMessage,
        ["TrainDelayCauseMessage"] = TrainDelayCauseMessage,
        ["TrainRunningForecastMessage"] = TrainRunningForecastMessage,
        ["TrainRunningInformationMessage"] = TrainRunningInformationMessage,
        ["TrainRunningInterruptionMessage"] = TrainRunningInterruptionMessage,
    };

    private const string ChangeOfTrackMessage = """
        <ChangeofTrackMessage xmlns="http://www.era.europa.eu/schemes/TAFTSI/3.4">
          <MessageHeader>
            <MessageReference>
              <MessageType>4504</MessageType>
              <MessageTypeVersion>3.4</MessageTypeVersion>
              <MessageIdentifier>REPLACE-WITH-UNIQUE-ID</MessageIdentifier>
              <MessageDateTime>2026-08-07T10:00:00Z</MessageDateTime>
            </MessageReference>
            <Sender>REPLACE-WITH-ISB-RICS</Sender>
            <Recipient>REPLACE-WITH-EVU-RICS</Recipient>
          </MessageHeader>
          <MessageStatus>1</MessageStatus>
          <TrainOperationalIdentification>
            <TransportOperationalIdentifiers>
              <ObjectType>TR</ObjectType>
              <Company>REPLACE-WITH-COMPANY-RICS</Company>
              <Core>REPLACE-WITH-TRAIN-NUMBER</Core>
              <Variant>REPLACE-WITH-VARIANT</Variant>
              <TimetableYear>2026</TimetableYear>
              <StartDate>2026-08-07</StartDate>
            </TransportOperationalIdentifiers>
          </TrainOperationalIdentification>
          <OperationalTrainNumberIdentifier>
            <OperationalTrainNumber>REPLACE-WITH-TRAIN-NUMBER</OperationalTrainNumber>
          </OperationalTrainNumberIdentifier>
          <ReferenceOTN>
            <OperationalTrainNumberIdentifier>
              <OperationalTrainNumber>REPLACE-WITH-TRAIN-NUMBER</OperationalTrainNumber>
            </OperationalTrainNumberIdentifier>
          </ReferenceOTN>
          <LocationPlannedTrack>
            <CountryCodeISO>REPLACE-WITH-COUNTRY-CODE</CountryCodeISO>
            <LocationPrimaryCode>REPLACE-WITH-LOCATION-CODE</LocationPrimaryCode>
            <PrimaryLocationName>REPLACE-WITH-LOCATION-NAME</PrimaryLocationName>
          </LocationPlannedTrack>
          <LocationActualTrack>
            <CountryCodeISO>REPLACE-WITH-COUNTRY-CODE</CountryCodeISO>
            <LocationPrimaryCode>REPLACE-WITH-LOCATION-CODE</LocationPrimaryCode>
            <PrimaryLocationName>REPLACE-WITH-NEW-TRACK</PrimaryLocationName>
            <LocationSubsidiaryIdentification>
              <LocationSubsidiaryCode>REPLACE-WITH-NEW-TRACK</LocationSubsidiaryCode>
              <AllocationCompany>REPLACE-WITH-ALLOCATION-COMPANY</AllocationCompany>
              <LocationSubsidiaryName>REPLACE-WITH-NEW-TRACK</LocationSubsidiaryName>
            </LocationSubsidiaryIdentification>
          </LocationActualTrack>
          <TransferPoint>
            <CountryCodeISO>REPLACE-WITH-COUNTRY-CODE</CountryCodeISO>
            <LocationPrimaryCode>REPLACE-WITH-LOCATION-CODE</LocationPrimaryCode>
            <PrimaryLocationName>REPLACE-WITH-LOCATION-NAME</PrimaryLocationName>
          </TransferPoint>
          <TransfereeIM>REPLACE-WITH-TRANSFEREE-IM</TransfereeIM>
        </ChangeofTrackMessage>
        """;

    private const string TrainDelayCauseMessage = """
        <TrainDelayCauseMessage xmlns="http://www.era.europa.eu/schemes/TAFTSI/3.4">
          <MessageHeader>
            <MessageReference>
              <MessageType>4001</MessageType>
              <MessageTypeVersion>3.4</MessageTypeVersion>
              <MessageIdentifier>REPLACE-WITH-UNIQUE-ID</MessageIdentifier>
              <MessageDateTime>2026-08-07T10:00:00Z</MessageDateTime>
            </MessageReference>
            <Sender>REPLACE-WITH-ISB-RICS</Sender>
            <Recipient>REPLACE-WITH-EVU-RICS</Recipient>
          </MessageHeader>
          <MessageStatus>1</MessageStatus>
          <TrainOperationalIdentification>
            <TransportOperationalIdentifiers>
              <ObjectType>TR</ObjectType>
              <Company>REPLACE-WITH-COMPANY-RICS</Company>
              <Core>REPLACE-WITH-TRAIN-NUMBER</Core>
              <Variant>REPLACE-WITH-VARIANT</Variant>
              <TimetableYear>2026</TimetableYear>
              <StartDate>2026-08-07</StartDate>
            </TransportOperationalIdentifiers>
          </TrainOperationalIdentification>
          <OperationalTrainNumberIdentifier>
            <OperationalTrainNumber>REPLACE-WITH-TRAIN-NUMBER</OperationalTrainNumber>
          </OperationalTrainNumberIdentifier>
          <ReferenceOTN>
            <OperationalTrainNumberIdentifier>
              <OperationalTrainNumber>REPLACE-WITH-TRAIN-NUMBER</OperationalTrainNumber>
            </OperationalTrainNumberIdentifier>
          </ReferenceOTN>
          <ResponsibleRU>REPLACE-WITH-RESPONSIBLE-RU-RICS</ResponsibleRU>
          <DelayEventReport>
            <DelayLocation>
              <CountryCodeISO>REPLACE-WITH-COUNTRY-CODE</CountryCodeISO>
              <LocationPrimaryCode>REPLACE-WITH-LOCATION-CODE</LocationPrimaryCode>
              <PrimaryLocationName>REPLACE-WITH-LOCATION-NAME</PrimaryLocationName>
            </DelayLocation>
            <TrainLocationStatus>00</TrainLocationStatus>
            <DelayCauseTime>
              <DelayCause>11</DelayCause>
              <DelayMinutes>REPLACE-WITH-DELAY-MINUTES</DelayMinutes>
              <DelayEventDateTime>2026-08-07T10:00:00Z</DelayEventDateTime>
              <InternalReferenceIdentifier>REPLACE-WITH-UNIQUE-ID</InternalReferenceIdentifier>
              <Remarks>REPLACE-WITH-CAUSE-TEXT</Remarks>
            </DelayCauseTime>
          </DelayEventReport>
          <TransferPoint>
            <CountryCodeISO>REPLACE-WITH-COUNTRY-CODE</CountryCodeISO>
            <LocationPrimaryCode>REPLACE-WITH-LOCATION-CODE</LocationPrimaryCode>
            <PrimaryLocationName>REPLACE-WITH-LOCATION-NAME</PrimaryLocationName>
          </TransferPoint>
          <TransfereeIM>REPLACE-WITH-TRANSFEREE-IM</TransfereeIM>
        </TrainDelayCauseMessage>
        """;

    private const string TrainRunningForecastMessage = """
        <TrainRunningForecastMessage xmlns="http://www.era.europa.eu/schemes/TAFTSI/3.4">
          <MessageHeader>
            <MessageReference>
              <MessageType>4004</MessageType>
              <MessageTypeVersion>3.4</MessageTypeVersion>
              <MessageIdentifier>REPLACE-WITH-UNIQUE-ID</MessageIdentifier>
              <MessageDateTime>2026-08-07T10:00:00Z</MessageDateTime>
            </MessageReference>
            <Sender>REPLACE-WITH-ISB-RICS</Sender>
            <Recipient>REPLACE-WITH-EVU-RICS</Recipient>
          </MessageHeader>
          <MessageStatus>1</MessageStatus>
          <TrainOperationalIdentification>
            <TransportOperationalIdentifiers>
              <ObjectType>TR</ObjectType>
              <Company>REPLACE-WITH-COMPANY-RICS</Company>
              <Core>REPLACE-WITH-TRAIN-NUMBER</Core>
              <Variant>REPLACE-WITH-VARIANT</Variant>
              <TimetableYear>2026</TimetableYear>
              <StartDate>2026-08-07</StartDate>
            </TransportOperationalIdentifiers>
          </TrainOperationalIdentification>
          <OperationalTrainNumberIdentifier>
            <OperationalTrainNumber>REPLACE-WITH-TRAIN-NUMBER</OperationalTrainNumber>
          </OperationalTrainNumberIdentifier>
          <ReferenceOTN>
            <OperationalTrainNumberIdentifier>
              <OperationalTrainNumber>REPLACE-WITH-TRAIN-NUMBER</OperationalTrainNumber>
            </OperationalTrainNumberIdentifier>
          </ReferenceOTN>
          <ResponsibleRU>REPLACE-WITH-RESPONSIBLE-RU-RICS</ResponsibleRU>
          <TrainLocationReport>
            <Location>
              <CountryCodeISO>REPLACE-WITH-COUNTRY-CODE</CountryCodeISO>
              <LocationPrimaryCode>REPLACE-WITH-LOCATION-CODE</LocationPrimaryCode>
              <PrimaryLocationName>REPLACE-WITH-LOCATION-NAME</PrimaryLocationName>
            </Location>
            <LocationDateTime>2026-08-07T10:00:00Z</LocationDateTime>
            <TrainLocationStatus>00</TrainLocationStatus>
            <TrainDelay>
              <AgainstBooked>PT0M</AgainstBooked>
              <AgainstReferenced>PT0M</AgainstReferenced>
            </TrainDelay>
          </TrainLocationReport>
          <TransferPoint>
            <CountryCodeISO>REPLACE-WITH-COUNTRY-CODE</CountryCodeISO>
            <LocationPrimaryCode>REPLACE-WITH-LOCATION-CODE</LocationPrimaryCode>
            <PrimaryLocationName>REPLACE-WITH-LOCATION-NAME</PrimaryLocationName>
          </TransferPoint>
          <TransfereeIM>REPLACE-WITH-TRANSFEREE-IM</TransfereeIM>
        </TrainRunningForecastMessage>
        """;

    private const string TrainRunningInformationMessage = """
        <TrainRunningInformationMessage xmlns="http://www.era.europa.eu/schemes/TAFTSI/3.4">
          <MessageHeader>
            <MessageReference>
              <MessageType>4005</MessageType>
              <MessageTypeVersion>3.4</MessageTypeVersion>
              <MessageIdentifier>REPLACE-WITH-UNIQUE-ID</MessageIdentifier>
              <MessageDateTime>2026-08-07T10:00:00Z</MessageDateTime>
            </MessageReference>
            <Sender>REPLACE-WITH-ISB-RICS</Sender>
            <Recipient>REPLACE-WITH-EVU-RICS</Recipient>
          </MessageHeader>
          <MessageStatus>1</MessageStatus>
          <TrainOperationalIdentification>
            <TransportOperationalIdentifiers>
              <ObjectType>TR</ObjectType>
              <Company>REPLACE-WITH-COMPANY-RICS</Company>
              <Core>REPLACE-WITH-TRAIN-NUMBER</Core>
              <Variant>REPLACE-WITH-VARIANT</Variant>
              <TimetableYear>2026</TimetableYear>
              <StartDate>2026-08-07</StartDate>
            </TransportOperationalIdentifiers>
          </TrainOperationalIdentification>
          <OperationalTrainNumberIdentifier>
            <OperationalTrainNumber>REPLACE-WITH-TRAIN-NUMBER</OperationalTrainNumber>
          </OperationalTrainNumberIdentifier>
          <ReferenceOTN>
            <OperationalTrainNumberIdentifier>
              <OperationalTrainNumber>REPLACE-WITH-TRAIN-NUMBER</OperationalTrainNumber>
            </OperationalTrainNumberIdentifier>
          </ReferenceOTN>
          <ResponsibleRU>REPLACE-WITH-RESPONSIBLE-RU-RICS</ResponsibleRU>
          <TrainLocationReport>
            <Location>
              <CountryCodeISO>REPLACE-WITH-COUNTRY-CODE</CountryCodeISO>
              <LocationPrimaryCode>REPLACE-WITH-LOCATION-CODE</LocationPrimaryCode>
              <PrimaryLocationName>REPLACE-WITH-LOCATION-NAME</PrimaryLocationName>
            </Location>
            <LocationDateTime>2026-08-07T10:00:00Z</LocationDateTime>
            <TrainLocationStatus>00</TrainLocationStatus>
            <TrainDelay>
              <AgainstBooked>PT0M</AgainstBooked>
              <AgainstReferenced>PT0M</AgainstReferenced>
            </TrainDelay>
          </TrainLocationReport>
          <TransferPoint>
            <CountryCodeISO>REPLACE-WITH-COUNTRY-CODE</CountryCodeISO>
            <LocationPrimaryCode>REPLACE-WITH-LOCATION-CODE</LocationPrimaryCode>
            <PrimaryLocationName>REPLACE-WITH-LOCATION-NAME</PrimaryLocationName>
          </TransferPoint>
          <TransfereeIM>REPLACE-WITH-TRANSFEREE-IM</TransfereeIM>
        </TrainRunningInformationMessage>
        """;

    private const string TrainRunningInterruptionMessage = """
        <TrainRunningInterruptionMessage xmlns="http://www.era.europa.eu/schemes/TAFTSI/3.4">
          <MessageHeader>
            <MessageReference>
              <MessageType>4006</MessageType>
              <MessageTypeVersion>3.4</MessageTypeVersion>
              <MessageIdentifier>REPLACE-WITH-UNIQUE-ID</MessageIdentifier>
              <MessageDateTime>2026-08-07T10:00:00Z</MessageDateTime>
            </MessageReference>
            <Sender>REPLACE-WITH-ISB-RICS</Sender>
            <Recipient>REPLACE-WITH-EVU-RICS</Recipient>
          </MessageHeader>
          <MessageStatus>1</MessageStatus>
          <TrainOperationalIdentification>
            <TransportOperationalIdentifiers>
              <ObjectType>TR</ObjectType>
              <Company>REPLACE-WITH-COMPANY-RICS</Company>
              <Core>REPLACE-WITH-TRAIN-NUMBER</Core>
              <Variant>REPLACE-WITH-VARIANT</Variant>
              <TimetableYear>2026</TimetableYear>
              <StartDate>2026-08-07</StartDate>
            </TransportOperationalIdentifiers>
          </TrainOperationalIdentification>
          <OperationalTrainNumberIdentifier>
            <OperationalTrainNumber>REPLACE-WITH-TRAIN-NUMBER</OperationalTrainNumber>
          </OperationalTrainNumberIdentifier>
          <ReferenceOTN>
            <OperationalTrainNumberIdentifier>
              <OperationalTrainNumber>REPLACE-WITH-TRAIN-NUMBER</OperationalTrainNumber>
            </OperationalTrainNumberIdentifier>
          </ReferenceOTN>
          <ResponsibleRU>REPLACE-WITH-RESPONSIBLE-RU-RICS</ResponsibleRU>
          <InterruptionPoint>
            <Location>
              <CountryCodeISO>REPLACE-WITH-COUNTRY-CODE</CountryCodeISO>
              <LocationPrimaryCode>REPLACE-WITH-LOCATION-CODE</LocationPrimaryCode>
              <PrimaryLocationName>REPLACE-WITH-LOCATION-NAME</PrimaryLocationName>
            </Location>
            <DetailedDescriptionOfLocation>REPLACE-WITH-LOCATION-DESCRIPTION</DetailedDescriptionOfLocation>
            <Interruption>
              <InterruptionDateTime>2026-08-07T10:00:00Z</InterruptionDateTime>
              <InterruptionDescription>REPLACE-WITH-REASON</InterruptionDescription>
              <InternalReferenceIdentifier>REPLACE-WITH-UNIQUE-ID</InternalReferenceIdentifier>
            </Interruption>
          </InterruptionPoint>
          <TrainRunningInterruptionStatus>
            <TrainInterrupted>1</TrainInterrupted>
          </TrainRunningInterruptionStatus>
          <TransferPoint>
            <CountryCodeISO>REPLACE-WITH-COUNTRY-CODE</CountryCodeISO>
            <LocationPrimaryCode>REPLACE-WITH-LOCATION-CODE</LocationPrimaryCode>
            <PrimaryLocationName>REPLACE-WITH-LOCATION-NAME</PrimaryLocationName>
          </TransferPoint>
          <TransfereeIM>REPLACE-WITH-TRANSFEREE-IM</TransfereeIM>
        </TrainRunningInterruptionMessage>
        """;
}
