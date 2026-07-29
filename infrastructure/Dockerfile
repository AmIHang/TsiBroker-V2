# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy solution and project files
COPY ["TsiBroker.slnx", "./"]
COPY ["src/TsiBroker.ApiService/TsiBroker.ApiService.csproj", "src/TsiBroker.ApiService/"]
COPY ["src/TsiBroker.Core/TsiBroker.Core.csproj", "src/TsiBroker.Core/"]

# Restore dependencies
RUN dotnet restore "src/TsiBroker.ApiService/TsiBroker.ApiService.csproj"

# Copy source code
COPY . .

# Build
WORKDIR "/src/src/TsiBroker.ApiService"
RUN dotnet build "TsiBroker.ApiService.csproj" -c Release -o /app/build

# Publish
FROM build AS publish
RUN dotnet publish "TsiBroker.ApiService.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
EXPOSE 8081
EXPOSE 8082

# Persistent data (e.g. infrastructure-operators.json) lives here — mount a volume
# at this path so it survives container recreation.
ENV InfrastructureOperators__DataDirectory=/app/data
VOLUME ["/app/data"]

COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "TsiBroker.ApiService.dll"]