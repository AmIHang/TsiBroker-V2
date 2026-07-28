# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy solution and project files
COPY ["TsiBroker.sln", "./"]
COPY ["src/TsiBroker.Api/TsiBroker.Api.csproj", "src/TsiBroker.Api/"]


# Restore dependencies
RUN dotnet restore "TsiBroker.sln"

# Copy source code
COPY . .

# Build
WORKDIR "/src/src/TsiBroker.Api"
RUN dotnet build "TsiBroker.Api.csproj" -c Release -o /app/build

# Publish
FROM build AS publish
RUN dotnet publish "TsiBroker.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
EXPOSE 8081
EXPOSE 8082

COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "TsiBroker.Api.dll"]