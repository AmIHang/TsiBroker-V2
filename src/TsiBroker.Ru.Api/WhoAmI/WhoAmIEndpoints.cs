using System.Text;
using System.Xml;
using System.Xml.Serialization;
using TsiBroker.Core.InfrastructureOperators;
using TsiBroker.Core.RailwayUndertakings;

namespace TsiBroker.Ru.Api.WhoAmI;

public static class WhoAmIEndpoints
{
    private static readonly XmlSerializer Serializer = new(typeof(WhoAmIResponse));

    public static void MapWhoAmIEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/whoami", async (
            HttpRequest request,
            RailwayUndertakingStore railwayUndertakingStore,
            InfrastructureOperatorStore infrastructureOperatorStore) =>
        {
            RailwayUndertaking? railwayUndertaking = null;
            if (request.Headers.TryGetValue(ApiKeyHeader.Name, out var apiKeyValues)
                && !string.IsNullOrWhiteSpace(apiKeyValues.ToString()))
            {
                railwayUndertaking = await railwayUndertakingStore.FindByApiKeyEvuToBrokerAsync(apiKeyValues.ToString());
                if (railwayUndertaking is not null && !railwayUndertaking.IsActive)
                {
                    railwayUndertaking = null;
                }

                // An authenticated request from this EVU is itself evidence it's back online —
                // see TsiMessageEndpoints.cs for the equivalent /message-triggered resume.
                if (railwayUndertaking is { IsQueuePaused: true })
                {
                    await railwayUndertakingStore.SetQueuePauseStateAsync(
                        railwayUndertaking.Id, isPaused: false, reason: null, backoffStep: 0);
                }
            }

            WhoAmIResponse response;
            if (railwayUndertaking is null)
            {
                response = UnknownResponse();
            }
            else
            {
                var infrastructureOperators = await infrastructureOperatorStore.GetAllAsync();
                response = BuildResponse(railwayUndertaking, infrastructureOperators);
            }

            return Results.Text(SerializeToXml(response), "application/xml");
        });
    }

    private static WhoAmIResponse UnknownResponse() => new()
    {
        Name = "<unknown>",
        RicsCodes = [],
        Permissions = [],
    };

    private static WhoAmIResponse BuildResponse(
        RailwayUndertaking railwayUndertaking,
        List<InfrastructureOperator> infrastructureOperators)
    {
        var permissions = railwayUndertaking.InfrastructureOperatorAssignments
            .Where(assignment => assignment.IsActive)
            .Select(assignment =>
            {
                var infrastructureOperator = infrastructureOperators.FirstOrDefault(o =>
                    o.Id == assignment.InfrastructureOperatorId && o.IsActive);
                return (assignment, infrastructureOperator);
            })
            .Where(x => x.infrastructureOperator is not null)
            .SelectMany(x => BuildPermissions(railwayUndertaking.RicsCodes, x.infrastructureOperator!.RicsCode, x.assignment))
            .ToList();

        return new WhoAmIResponse
        {
            Name = railwayUndertaking.Name,
            RicsCodes = railwayUndertaking.RicsCodes,
            Permissions = permissions,
        };
    }

    private static IEnumerable<WhoAmIPermission> BuildPermissions(
        List<string> railwayUndertakingRicsCodes,
        string infrastructureOperatorRicsCode,
        IsbAssignment assignment)
    {
        foreach (var ruRicsCode in railwayUndertakingRicsCodes)
        {
            foreach (var messageType in assignment.AllowedMessageTypesEvuToBroker)
            {
                yield return new WhoAmIPermission
                {
                    Sender = ruRicsCode,
                    Receiver = infrastructureOperatorRicsCode,
                    MessageType = messageType,
                };
            }

            foreach (var messageType in assignment.AllowedMessageTypesBrokerToEvu)
            {
                yield return new WhoAmIPermission
                {
                    Sender = infrastructureOperatorRicsCode,
                    Receiver = ruRicsCode,
                    MessageType = messageType,
                };
            }
        }
    }

    private static string SerializeToXml(WhoAmIResponse response)
    {
        // XmlSerializer emits the default xsi/xsd xmlns attributes unless an empty
        // namespace set is passed explicitly.
        var namespaces = new XmlSerializerNamespaces();
        namespaces.Add(string.Empty, string.Empty);

        using var stringWriter = new Utf8StringWriter();
        using var xmlWriter = XmlWriter.Create(stringWriter, new XmlWriterSettings { Indent = true });
        Serializer.Serialize(xmlWriter, response, namespaces);
        return stringWriter.ToString();
    }

    // StringWriter reports UTF-16 in its Encoding property regardless of the string content,
    // which XmlWriter would otherwise copy into the <?xml ... encoding="..."?> declaration.
    private sealed class Utf8StringWriter : StringWriter
    {
        public override Encoding Encoding => Encoding.UTF8;
    }
}
