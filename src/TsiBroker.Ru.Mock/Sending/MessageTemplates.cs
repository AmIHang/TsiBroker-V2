namespace TsiBroker.Ru.Mock.Sending;

/// <summary>
/// Starter templates for the message types this mock initially supports sending, so testers
/// don't have to hand-write TAF/TAP XML for the common cases. These follow the official ERA
/// TAF/TSI schema (namespace http://www.era.europa.eu/schemes/TAFTSI/3.4, element names/nesting/
/// order matching the xsd.exe-generated classes real ISB systems deserialize against - e.g.
/// Community.Operations.Interfaces...Era.Tsi.Generated) rather than an ad-hoc simplification,
/// since real customer systems parse this with a strict XmlSerializer. Only elements the schema
/// marks required (no matching "...Specified" opt-out property) are included, plus one
/// representative loco/wagon so the message still carries a meaningful composition; deeply
/// optional data (brakes, traction details, GNSS, wagon telemetry, ...) is left out to keep
/// these editable rather than exhaustive. No XML attributes are used anywhere so the existing
/// attribute-free regex tooling (see TsiBroker.Ru.Mock.UI/src/lib/messageXml.ts and
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
        ["TrainCompositionMessage"] = TrainCompositionMessage,
        ["TrainReadyMessage"] = TrainReadyMessage,
    };

    private const string TrainCompositionMessage = """
        <TrainCompositionMessage xmlns="http://www.era.europa.eu/schemes/TAFTSI/3.4">
          <MessageHeader>
            <MessageReference>
              <MessageType>3003</MessageType>
              <MessageTypeVersion>3.4</MessageTypeVersion>
              <MessageIdentifier>REPLACE-WITH-UNIQUE-ID</MessageIdentifier>
              <MessageDateTime>2026-08-07T10:00:00Z</MessageDateTime>
            </MessageReference>
            <Sender>3395</Sender>
            <Recipient>0081</Recipient>
          </MessageHeader>
          <MessageStatus>1</MessageStatus>
          <TransportOperationalIdentifiers>
            <ObjectType>TR</ObjectType>
            <Company>3395</Company>
            <Core>43521</Core>
            <Variant>00</Variant>
            <TimetableYear>2026</TimetableYear>
            <StartDate>2026-08-07</StartDate>
          </TransportOperationalIdentifiers>
          <OperationalTrainNumberIdentifier>
            <OperationalTrainNumber>43521</OperationalTrainNumber>
          </OperationalTrainNumberIdentifier>
          <ReferenceOTN>
            <OperationalTrainNumberIdentifier>
              <OperationalTrainNumber>43521</OperationalTrainNumber>
            </OperationalTrainNumberIdentifier>
          </ReferenceOTN>
          <TransferPoint>
            <CountryCodeISO>AT</CountryCodeISO>
            <LocationPrimaryCode>01194</LocationPrimaryCode>
            <PrimaryLocationName>Wien Hbf</PrimaryLocationName>
          </TransferPoint>
          <TransfereeIM>0081</TransfereeIM>
          <TrainCompositionJourneySection>
            <JourneySection>
              <JourneySectionOrigin>
                <CountryCodeISO>AT</CountryCodeISO>
                <LocationPrimaryCode>01194</LocationPrimaryCode>
                <PrimaryLocationName>Wien Hbf</PrimaryLocationName>
              </JourneySectionOrigin>
              <JourneySectionDestination>
                <CountryCodeISO>AT</CountryCodeISO>
                <LocationPrimaryCode>01313</LocationPrimaryCode>
                <PrimaryLocationName>Salzburg Hbf</PrimaryLocationName>
              </JourneySectionDestination>
              <ResponsibilityActualSection>
                <ResponsibleRU>3395</ResponsibleRU>
                <ResponsibleIM>0081</ResponsibleIM>
              </ResponsibilityActualSection>
              <ResponsibilityNextSection>
                <ResponsibleRU>3395</ResponsibleRU>
                <ResponsibleIM>0081</ResponsibleIM>
              </ResponsibilityNextSection>
            </JourneySection>
            <LocoIdent>
              <TractionType>00</TractionType>
              <LocoTypeNumber>
                <TypeCode1>94</TypeCode1>
                <TypeCode2>80</TypeCode2>
                <CountryCode>81</CountryCode>
                <SeriesNumber>1116</SeriesNumber>
                <SerialNumber>234</SerialNumber>
                <ControlDigit>5</ControlDigit>
              </LocoTypeNumber>
              <LocoNumber>1116 234-5</LocoNumber>
            </LocoIdent>
            <WagonData>
              <WagonNumberFreight>4576 123-4</WagonNumberFreight>
              <WagonTrainPosition>1</WagonTrainPosition>
            </WagonData>
          </TrainCompositionJourneySection>
        </TrainCompositionMessage>
        """;

    private const string TrainReadyMessage = """
        <TrainReadyMessage xmlns="http://www.era.europa.eu/schemes/TAFTSI/3.4">
          <MessageHeader>
            <MessageReference>
              <MessageType>4500</MessageType>
              <MessageTypeVersion>3.4</MessageTypeVersion>
              <MessageIdentifier>REPLACE-WITH-UNIQUE-ID</MessageIdentifier>
              <MessageDateTime>2026-08-07T10:00:00Z</MessageDateTime>
            </MessageReference>
            <Sender>3395</Sender>
            <Recipient>0081</Recipient>
          </MessageHeader>
          <MessageStatus>1</MessageStatus>
          <TransportOperationalIdentifiers>
            <ObjectType>TR</ObjectType>
            <Company>3395</Company>
            <Core>43521</Core>
            <Variant>00</Variant>
            <TimetableYear>2026</TimetableYear>
            <StartDate>2026-08-07</StartDate>
          </TransportOperationalIdentifiers>
          <OperationalTrainNumberIdentifier>
            <OperationalTrainNumber>43521</OperationalTrainNumber>
          </OperationalTrainNumberIdentifier>
          <ReferenceOTN>
            <OperationalTrainNumberIdentifier>
              <OperationalTrainNumber>43521</OperationalTrainNumber>
            </OperationalTrainNumberIdentifier>
          </ReferenceOTN>
          <ResponsibleRU>3395</ResponsibleRU>
          <TrainContactDetails>+43 664 1234567</TrainContactDetails>
          <TrainLocation>
            <CountryCodeISO>AT</CountryCodeISO>
            <LocationPrimaryCode>01194</LocationPrimaryCode>
            <PrimaryLocationName>Wien Hbf</PrimaryLocationName>
          </TrainLocation>
          <TrainReadyStatus>
            <TrainReady>1</TrainReady>
            <TrainNotReadyDescription>-</TrainNotReadyDescription>
          </TrainReadyStatus>
          <TransferPoint>
            <CountryCodeISO>AT</CountryCodeISO>
            <LocationPrimaryCode>01194</LocationPrimaryCode>
            <PrimaryLocationName>Wien Hbf</PrimaryLocationName>
          </TransferPoint>
          <TransfereeIM>0081</TransfereeIM>
        </TrainReadyMessage>
        """;
}
