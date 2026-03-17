FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build
WORKDIR /src

# Copy build configuration
COPY Directory.Build.props Directory.Packages.props ./

# Copy project files for restore layer caching
COPY src/Hexalith.EventStore.Contracts/Hexalith.EventStore.Contracts.csproj src/Hexalith.EventStore.Contracts/
COPY src/Hexalith.EventStore.Client/Hexalith.EventStore.Client.csproj src/Hexalith.EventStore.Client/
COPY src/Hexalith.EventStore.ServiceDefaults/Hexalith.EventStore.ServiceDefaults.csproj src/Hexalith.EventStore.ServiceDefaults/
COPY samples/Hexalith.EventStore.Sample/Hexalith.EventStore.Sample.csproj samples/Hexalith.EventStore.Sample/

RUN dotnet restore samples/Hexalith.EventStore.Sample/Hexalith.EventStore.Sample.csproj

# Copy source and publish
COPY src/ src/
COPY samples/ samples/
RUN dotnet publish samples/Hexalith.EventStore.Sample/Hexalith.EventStore.Sample.csproj \
    -c Release -o /app/publish --no-restore /p:MinVerSkip=true

# Runtime
FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS runtime
RUN addgroup -S appgroup && adduser -S appuser -G appgroup
WORKDIR /app
COPY --from=build /app/publish .
USER appuser
EXPOSE 8080
ENTRYPOINT ["dotnet", "Hexalith.EventStore.Sample.dll"]
