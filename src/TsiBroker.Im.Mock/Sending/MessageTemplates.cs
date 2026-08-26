using System.Xml.Linq;

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
/// AllowedMessageTypesEvuToBroker). Fields are pre-filled with plausible sample data (matching
/// the RICS codes actually configured in App_Data - see infrastructure-operators.json and
/// railway-undertakings.json - so a send-without-editing round-trips through the broker's own
/// authorization checks) rather than "REPLACE-WITH-..." placeholders, so a tester isn't forced
/// to fill in every field before sending; only MessageIdentifier keeps its REPLACE-WITH token,
/// since SendMessageView.vue's loadTemplate() specifically detects that to auto-generate a
/// fresh unique id instead of reusing a fixed sample value that would collide across sends.
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
        ["ErrorMessage"] = ErrorMessage,
    };

    // The numeric MessageType (spec code, e.g. "4504") paired with each template's key (the TAF/TAP
    // element name, e.g. "ChangeofTrackMessage") - lets the send form's message type picker show
    // "4504 - ChangeofTrackMessage" rather than just the element name, and sort by the numeric
    // code rather than alphabetically. Reads the number straight out of the template XML
    // (MessageHeader/MessageReference/MessageType) rather than hand-duplicating it here, so the
    // two can't drift apart.
    public record TemplateSummary(string Key, string MessageType);

    public static readonly IReadOnlyList<TemplateSummary> Summaries = ByMessageType
        .Select(kv => new TemplateSummary(kv.Key, ExtractMessageTypeCode(kv.Value)))
        .OrderBy(s => int.TryParse(s.MessageType, out var code) ? code : int.MaxValue)
        .ToList();

    private static string ExtractMessageTypeCode(string xml) =>
        XDocument.Parse(xml).Descendants().First(e => e.Name.LocalName == "MessageType").Value;

    private const string ChangeOfTrackMessage = """
        <ChangeofTrackMessage xmlns="http://www.era.europa.eu/schemes/TAFTSI/3.4">
          <MessageHeader>
            <MessageReference>
              <MessageType>4504</MessageType>
              <MessageTypeVersion>3.4</MessageTypeVersion>
              <MessageIdentifier>REPLACE-WITH-UNIQUE-ID</MessageIdentifier>
              <MessageDateTime>2026-08-07T10:00:00Z</MessageDateTime>
            </MessageReference>
            <Sender>0081</Sender>
            <Recipient>3395</Recipient>
          </MessageHeader>
          <MessageStatus>1</MessageStatus>
          <TrainOperationalIdentification>
            <TransportOperationalIdentifiers>
              <ObjectType>TR</ObjectType>
              <Company>0081</Company>
              <Core>43521</Core>
              <Variant>00</Variant>
              <TimetableYear>2026</TimetableYear>
              <StartDate>2026-08-07</StartDate>
            </TransportOperationalIdentifiers>
          </TrainOperationalIdentification>
          <OperationalTrainNumberIdentifier>
            <OperationalTrainNumber>43521</OperationalTrainNumber>
          </OperationalTrainNumberIdentifier>
          <ReferenceOTN>
            <OperationalTrainNumberIdentifier>
              <OperationalTrainNumber>43521</OperationalTrainNumber>
            </OperationalTrainNumberIdentifier>
          </ReferenceOTN>
          <LocationPlannedTrack>
            <CountryCodeISO>AT</CountryCodeISO>
            <LocationPrimaryCode>01194</LocationPrimaryCode>
            <PrimaryLocationName>Wien Hbf</PrimaryLocationName>
          </LocationPlannedTrack>
          <LocationActualTrack>
            <CountryCodeISO>AT</CountryCodeISO>
            <LocationPrimaryCode>01194</LocationPrimaryCode>
            <PrimaryLocationName>Wien Hbf</PrimaryLocationName>
            <LocationSubsidiaryIdentification>
              <LocationSubsidiaryCode>Gleis 3</LocationSubsidiaryCode>
              <AllocationCompany>0081</AllocationCompany>
              <LocationSubsidiaryName>Gleis 3</LocationSubsidiaryName>
            </LocationSubsidiaryIdentification>
          </LocationActualTrack>
          <TransferPoint>
            <CountryCodeISO>AT</CountryCodeISO>
            <LocationPrimaryCode>01194</LocationPrimaryCode>
            <PrimaryLocationName>Wien Hbf</PrimaryLocationName>
          </TransferPoint>
          <TransfereeIM>0081</TransfereeIM>
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
            <Sender>0081</Sender>
            <Recipient>3395</Recipient>
          </MessageHeader>
          <MessageStatus>1</MessageStatus>
          <TrainOperationalIdentification>
            <TransportOperationalIdentifiers>
              <ObjectType>TR</ObjectType>
              <Company>0081</Company>
              <Core>43521</Core>
              <Variant>00</Variant>
              <TimetableYear>2026</TimetableYear>
              <StartDate>2026-08-07</StartDate>
            </TransportOperationalIdentifiers>
          </TrainOperationalIdentification>
          <OperationalTrainNumberIdentifier>
            <OperationalTrainNumber>43521</OperationalTrainNumber>
          </OperationalTrainNumberIdentifier>
          <ReferenceOTN>
            <OperationalTrainNumberIdentifier>
              <OperationalTrainNumber>43521</OperationalTrainNumber>
            </OperationalTrainNumberIdentifier>
          </ReferenceOTN>
          <ResponsibleRU>3395</ResponsibleRU>
          <DelayEventReport>
            <DelayLocation>
              <CountryCodeISO>AT</CountryCodeISO>
              <LocationPrimaryCode>01194</LocationPrimaryCode>
              <PrimaryLocationName>Wien Hbf</PrimaryLocationName>
            </DelayLocation>
            <TrainLocationStatus>00</TrainLocationStatus>
            <DelayCauseTime>
              <DelayCause>11</DelayCause>
              <DelayMinutes>0010</DelayMinutes>
              <DelayEventDateTime>2026-08-07T10:00:00Z</DelayEventDateTime>
              <InternalReferenceIdentifier>REF-0001</InternalReferenceIdentifier>
              <Remarks>Signalstoerung</Remarks>
            </DelayCauseTime>
          </DelayEventReport>
          <TransferPoint>
            <CountryCodeISO>AT</CountryCodeISO>
            <LocationPrimaryCode>01194</LocationPrimaryCode>
            <PrimaryLocationName>Wien Hbf</PrimaryLocationName>
          </TransferPoint>
          <TransfereeIM>0081</TransfereeIM>
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
            <Sender>0081</Sender>
            <Recipient>3395</Recipient>
          </MessageHeader>
          <MessageStatus>1</MessageStatus>
          <TrainOperationalIdentification>
            <TransportOperationalIdentifiers>
              <ObjectType>TR</ObjectType>
              <Company>0081</Company>
              <Core>43521</Core>
              <Variant>00</Variant>
              <TimetableYear>2026</TimetableYear>
              <StartDate>2026-08-07</StartDate>
            </TransportOperationalIdentifiers>
          </TrainOperationalIdentification>
          <OperationalTrainNumberIdentifier>
            <OperationalTrainNumber>43521</OperationalTrainNumber>
          </OperationalTrainNumberIdentifier>
          <ReferenceOTN>
            <OperationalTrainNumberIdentifier>
              <OperationalTrainNumber>43521</OperationalTrainNumber>
            </OperationalTrainNumberIdentifier>
          </ReferenceOTN>
          <ResponsibleRU>3395</ResponsibleRU>
          <TrainLocationReport>
            <Location>
              <CountryCodeISO>AT</CountryCodeISO>
              <LocationPrimaryCode>01194</LocationPrimaryCode>
              <PrimaryLocationName>Wien Hbf</PrimaryLocationName>
            </Location>
            <LocationDateTime>2026-08-07T10:00:00Z</LocationDateTime>
            <TrainLocationStatus>00</TrainLocationStatus>
            <TrainDelay>
              <AgainstBooked>PT0M</AgainstBooked>
              <AgainstReferenced>PT0M</AgainstReferenced>
            </TrainDelay>
          </TrainLocationReport>
          <TransferPoint>
            <CountryCodeISO>AT</CountryCodeISO>
            <LocationPrimaryCode>01194</LocationPrimaryCode>
            <PrimaryLocationName>Wien Hbf</PrimaryLocationName>
          </TransferPoint>
          <TransfereeIM>0081</TransfereeIM>
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
            <Sender>0081</Sender>
            <Recipient>3395</Recipient>
          </MessageHeader>
          <MessageStatus>1</MessageStatus>
          <TrainOperationalIdentification>
            <TransportOperationalIdentifiers>
              <ObjectType>TR</ObjectType>
              <Company>0081</Company>
              <Core>43521</Core>
              <Variant>00</Variant>
              <TimetableYear>2026</TimetableYear>
              <StartDate>2026-08-07</StartDate>
            </TransportOperationalIdentifiers>
          </TrainOperationalIdentification>
          <OperationalTrainNumberIdentifier>
            <OperationalTrainNumber>43521</OperationalTrainNumber>
          </OperationalTrainNumberIdentifier>
          <ReferenceOTN>
            <OperationalTrainNumberIdentifier>
              <OperationalTrainNumber>43521</OperationalTrainNumber>
            </OperationalTrainNumberIdentifier>
          </ReferenceOTN>
          <ResponsibleRU>3395</ResponsibleRU>
          <TrainLocationReport>
            <Location>
              <CountryCodeISO>AT</CountryCodeISO>
              <LocationPrimaryCode>01194</LocationPrimaryCode>
              <PrimaryLocationName>Wien Hbf</PrimaryLocationName>
            </Location>
            <LocationDateTime>2026-08-07T10:00:00Z</LocationDateTime>
            <TrainLocationStatus>00</TrainLocationStatus>
            <TrainDelay>
              <AgainstBooked>PT0M</AgainstBooked>
              <AgainstReferenced>PT0M</AgainstReferenced>
            </TrainDelay>
          </TrainLocationReport>
          <TransferPoint>
            <CountryCodeISO>AT</CountryCodeISO>
            <LocationPrimaryCode>01194</LocationPrimaryCode>
            <PrimaryLocationName>Wien Hbf</PrimaryLocationName>
          </TransferPoint>
          <TransfereeIM>0081</TransfereeIM>
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
            <Sender>0081</Sender>
            <Recipient>3395</Recipient>
          </MessageHeader>
          <MessageStatus>1</MessageStatus>
          <TrainOperationalIdentification>
            <TransportOperationalIdentifiers>
              <ObjectType>TR</ObjectType>
              <Company>0081</Company>
              <Core>43521</Core>
              <Variant>00</Variant>
              <TimetableYear>2026</TimetableYear>
              <StartDate>2026-08-07</StartDate>
            </TransportOperationalIdentifiers>
          </TrainOperationalIdentification>
          <OperationalTrainNumberIdentifier>
            <OperationalTrainNumber>43521</OperationalTrainNumber>
          </OperationalTrainNumberIdentifier>
          <ReferenceOTN>
            <OperationalTrainNumberIdentifier>
              <OperationalTrainNumber>43521</OperationalTrainNumber>
            </OperationalTrainNumberIdentifier>
          </ReferenceOTN>
          <ResponsibleRU>3395</ResponsibleRU>
          <InterruptionPoint>
            <Location>
              <CountryCodeISO>AT</CountryCodeISO>
              <LocationPrimaryCode>01194</LocationPrimaryCode>
              <PrimaryLocationName>Wien Hbf</PrimaryLocationName>
            </Location>
            <DetailedDescriptionOfLocation>zwischen Wien Hbf und Wien Meidling</DetailedDescriptionOfLocation>
            <Interruption>
              <InterruptionDateTime>2026-08-07T10:00:00Z</InterruptionDateTime>
              <InterruptionDescription>Signalstoerung</InterruptionDescription>
              <InternalReferenceIdentifier>REF-0001</InternalReferenceIdentifier>
            </Interruption>
          </InterruptionPoint>
          <TrainRunningInterruptionStatus>
            <TrainInterrupted>1</TrainInterrupted>
          </TrainRunningInterruptionStatus>
          <TransferPoint>
            <CountryCodeISO>AT</CountryCodeISO>
            <LocationPrimaryCode>01194</LocationPrimaryCode>
            <PrimaryLocationName>Wien Hbf</PrimaryLocationName>
          </TransferPoint>
          <TransfereeIM>0081</TransfereeIM>
        </TrainRunningInterruptionMessage>
        """;

    private const string ErrorMessage = """
        <ErrorMessage xmlns="http://www.era.europa.eu/schemes/TAFTSI/3.4">
          <MessageHeader>
            <MessageReference>
              <MessageType>9000</MessageType>
              <MessageTypeVersion>3.4</MessageTypeVersion>
              <MessageIdentifier>REPLACE-WITH-UNIQUE-ID</MessageIdentifier>
              <MessageDateTime>2026-08-07T10:00:00Z</MessageDateTime>
            </MessageReference>
            <Sender>0081</Sender>
            <Recipient>3395</Recipient>
          </MessageHeader>
          <MessageStatus>1</MessageStatus>
          <AdministrativeContactInformation>
            <Name>TsiBroker Im.Mock Support</Name>
          </AdministrativeContactInformation>
          <ErrorCauseReference>
            <MessageReference>
              <MessageType>4500</MessageType>
              <MessageTypeVersion>3.4</MessageTypeVersion>
              <MessageIdentifier>SAMPLE-ORIGINAL-MESSAGE-ID</MessageIdentifier>
              <MessageDateTime>2026-08-07T09:55:00Z</MessageDateTime>
            </MessageReference>
          </ErrorCauseReference>
          <Error>
            <TypeOfError>2</TypeOfError>
            <Severity>2</Severity>
            <ErrorCode>5001</ErrorCode>
            <FreeTextField>Message could not be processed in the backend system.</FreeTextField>
          </Error>
          <TransportOperationalIdentifiers>
            <ObjectType>TR</ObjectType>
            <Company>0081</Company>
            <Core>43521</Core>
            <Variant>00</Variant>
            <TimetableYear>2026</TimetableYear>
            <StartDate>2026-08-07</StartDate>
          </TransportOperationalIdentifiers>
        </ErrorMessage>
        """;
}
