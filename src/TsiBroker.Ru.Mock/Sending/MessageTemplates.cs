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
/// AllowedMessageTypesEvuToBroker).
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
            <Sender>REPLACE-WITH-EVU-RICS</Sender>
            <Recipient>REPLACE-WITH-ISB-RICS</Recipient>
          </MessageHeader>
          <MessageStatus>1</MessageStatus>
          <TransportOperationalIdentifiers>
            <ObjectType>TR</ObjectType>
            <Company>REPLACE-WITH-COMPANY-RICS</Company>
            <Core>REPLACE-WITH-TRAIN-NUMBER</Core>
            <Variant>REPLACE-WITH-VARIANT</Variant>
            <TimetableYear>2026</TimetableYear>
            <StartDate>2026-08-07</StartDate>
          </TransportOperationalIdentifiers>
          <OperationalTrainNumberIdentifier>
            <OperationalTrainNumber>REPLACE-WITH-TRAIN-NUMBER</OperationalTrainNumber>
          </OperationalTrainNumberIdentifier>
          <ReferenceOTN>
            <OperationalTrainNumberIdentifier>
              <OperationalTrainNumber>REPLACE-WITH-TRAIN-NUMBER</OperationalTrainNumber>
            </OperationalTrainNumberIdentifier>
          </ReferenceOTN>
          <TransferPoint>
            <CountryCodeISO>REPLACE-WITH-COUNTRY-CODE</CountryCodeISO>
            <LocationPrimaryCode>REPLACE-WITH-LOCATION-CODE</LocationPrimaryCode>
            <PrimaryLocationName>REPLACE-WITH-LOCATION-NAME</PrimaryLocationName>
          </TransferPoint>
          <TransfereeIM>REPLACE-WITH-TRANSFEREE-IM</TransfereeIM>
          <TrainCompositionJourneySection>
            <JourneySection>
              <JourneySectionOrigin>
                <CountryCodeISO>REPLACE-WITH-COUNTRY-CODE</CountryCodeISO>
                <LocationPrimaryCode>REPLACE-WITH-LOCATION-CODE</LocationPrimaryCode>
                <PrimaryLocationName>REPLACE-WITH-ORIGIN-NAME</PrimaryLocationName>
              </JourneySectionOrigin>
              <JourneySectionDestination>
                <CountryCodeISO>REPLACE-WITH-COUNTRY-CODE</CountryCodeISO>
                <LocationPrimaryCode>REPLACE-WITH-LOCATION-CODE</LocationPrimaryCode>
                <PrimaryLocationName>REPLACE-WITH-DESTINATION-NAME</PrimaryLocationName>
              </JourneySectionDestination>
              <ResponsibilityActualSection>
                <ResponsibleRU>REPLACE-WITH-RESPONSIBLE-RU-RICS</ResponsibleRU>
                <ResponsibleIM>REPLACE-WITH-RESPONSIBLE-IM-RICS</ResponsibleIM>
              </ResponsibilityActualSection>
              <ResponsibilityNextSection>
                <ResponsibleRU>REPLACE-WITH-RESPONSIBLE-RU-RICS</ResponsibleRU>
                <ResponsibleIM>REPLACE-WITH-RESPONSIBLE-IM-RICS</ResponsibleIM>
              </ResponsibilityNextSection>
            </JourneySection>
            <LocoIdent>
              <TractionType>00</TractionType>
              <LocoTypeNumber>
                <TypeCode1>REPLACE-WITH-TYPE-CODE-1</TypeCode1>
                <TypeCode2>REPLACE-WITH-TYPE-CODE-2</TypeCode2>
                <CountryCode>REPLACE-WITH-COUNTRY-CODE</CountryCode>
                <SeriesNumber>REPLACE-WITH-SERIES-NUMBER</SeriesNumber>
                <SerialNumber>REPLACE-WITH-SERIAL-NUMBER</SerialNumber>
                <ControlDigit>REPLACE-WITH-CONTROL-DIGIT</ControlDigit>
              </LocoTypeNumber>
              <LocoNumber>REPLACE-WITH-LOCO-NUMBER</LocoNumber>
            </LocoIdent>
            <WagonData>
              <WagonNumberFreight>REPLACE-WITH-WAGON-NUMBER</WagonNumberFreight>
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
            <Sender>REPLACE-WITH-EVU-RICS</Sender>
            <Recipient>REPLACE-WITH-ISB-RICS</Recipient>
          </MessageHeader>
          <MessageStatus>1</MessageStatus>
          <TransportOperationalIdentifiers>
            <ObjectType>TR</ObjectType>
            <Company>REPLACE-WITH-COMPANY-RICS</Company>
            <Core>REPLACE-WITH-TRAIN-NUMBER</Core>
            <Variant>REPLACE-WITH-VARIANT</Variant>
            <TimetableYear>2026</TimetableYear>
            <StartDate>2026-08-07</StartDate>
          </TransportOperationalIdentifiers>
          <OperationalTrainNumberIdentifier>
            <OperationalTrainNumber>REPLACE-WITH-TRAIN-NUMBER</OperationalTrainNumber>
          </OperationalTrainNumberIdentifier>
          <ReferenceOTN>
            <OperationalTrainNumberIdentifier>
              <OperationalTrainNumber>REPLACE-WITH-TRAIN-NUMBER</OperationalTrainNumber>
            </OperationalTrainNumberIdentifier>
          </ReferenceOTN>
          <ResponsibleRU>REPLACE-WITH-RESPONSIBLE-RU-RICS</ResponsibleRU>
          <TrainContactDetails>REPLACE-WITH-CONTACT-DETAILS</TrainContactDetails>
          <TrainLocation>
            <CountryCodeISO>REPLACE-WITH-COUNTRY-CODE</CountryCodeISO>
            <LocationPrimaryCode>REPLACE-WITH-LOCATION-CODE</LocationPrimaryCode>
            <PrimaryLocationName>REPLACE-WITH-LOCATION-NAME</PrimaryLocationName>
          </TrainLocation>
          <TrainReadyStatus>
            <TrainReady>1</TrainReady>
            <TrainNotReadyDescription>REPLACE-WITH-NOT-READY-DESCRIPTION</TrainNotReadyDescription>
          </TrainReadyStatus>
          <TransferPoint>
            <CountryCodeISO>REPLACE-WITH-COUNTRY-CODE</CountryCodeISO>
            <LocationPrimaryCode>REPLACE-WITH-LOCATION-CODE</LocationPrimaryCode>
            <PrimaryLocationName>REPLACE-WITH-LOCATION-NAME</PrimaryLocationName>
          </TransferPoint>
          <TransfereeIM>REPLACE-WITH-TRANSFEREE-IM</TransfereeIM>
        </TrainReadyMessage>
        """;
}
