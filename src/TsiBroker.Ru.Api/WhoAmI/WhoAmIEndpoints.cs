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
        app.MapPost("/whoami", async (
            HttpRequest request,
            RailwayUndertakingStore railwayUndertakingStore,
            InfrastructureOperatorStore infrastructureOperatorStore) =>
        {
            if (!request.Headers.TryGetValue(ApiKeyHeader.Name, out var apiKeyValues)
                || string.IsNullOrWhiteSpace(apiKeyValues.ToString()))
            {
                return Results.Problem(
                    title: "Missing API key",
                    detail: $"The {ApiKeyHeader.Name} header is required.",
                    statusCode: StatusCodes.Status401Unauthorized);
            }

            var railwayUndertaking = await railwayUndertakingStore.FindByApiKeyEvuToBrokerAsync(apiKeyValues.ToString());
            if (railwayUndertaking is null || !railwayUndertaking.IsActive)
            {
                return Results.Problem(
                    title: "Invalid API key",
                    detail: "No active EVU was found for the supplied API key.",
                    statusCode: StatusCodes.Status401Unauthorized);
            }

            var infrastructureOperators = await infrastructureOperatorStore.GetAllAsync();
            var response = BuildResponse(railwayUndertaking, infrastructureOperators);
            return Results.Text(SerializeToXml(response), "application/xml");
        });
    }

    private static WhoAmIResponse BuildResponse(
        RailwayUndertaking railwayUndertaking,
        List<InfrastructureOperator> infrastructureOperators)
    {
        var assignedOperators = railwayUndertaking.InfrastructureOperatorAssignments
            .Where(assignment => assignment.IsActive)
            .Select(assignment =>
            {
                var infrastructureOperator = infrastructureOperators.FirstOrDefault(o =>
                    o.Id == assignment.InfrastructureOperatorId && o.IsActive);
                return (assignment, infrastructureOperator);
            })
            .Where(x => x.infrastructureOperator is not null)
            .Select(x => new WhoAmIInfrastructureOperator
            {
                Name = x.infrastructureOperator!.Name,
                RicsCode = x.infrastructureOperator.RicsCode,
                AllowedMessageTypesEvuToBroker = x.assignment.AllowedMessageTypesEvuToBroker,
                AllowedMessageTypesBrokerToEvu = x.assignment.AllowedMessageTypesBrokerToEvu,
            })
            .ToList();

        return new WhoAmIResponse
        {
            Name = railwayUndertaking.Name,
            RicsCodes = railwayUndertaking.RicsCodes,
            InfrastructureOperators = assignedOperators,
        };
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
