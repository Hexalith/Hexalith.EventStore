FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build
WORKDIR /src

# Copy build configuration
COPY Directory.Build.props Directory.Packages.props ./

# Copy project files for restore layer caching
COPY src/Hexalith.EventStore.Contracts/Hexalith.EventStore.Contracts.csproj src/Hexalith.EventStore.Contracts/
COPY src/Hexalith.EventStore.Client/Hexalith.EventStore.Client.csproj src/Hexalith.EventStore.Client/
COPY src/Hexalith.EventStore.Admin.Abstractions/Hexalith.EventStore.Admin.Abstractions.csproj src/Hexalith.EventStore.Admin.Abstractions/
COPY src/Hexalith.EventStore.Server/Hexalith.EventStore.Server.csproj src/Hexalith.EventStore.Server/
COPY src/Hexalith.EventStore.ServiceDefaults/Hexalith.EventStore.ServiceDefaults.csproj src/Hexalith.EventStore.ServiceDefaults/
COPY src/Hexalith.EventStore/Hexalith.EventStore.csproj src/Hexalith.EventStore/

RUN dotnet restore src/Hexalith.EventStore/Hexalith.EventStore.csproj

# Copy source and publish
COPY src/ src/
RUN dotnet publish src/Hexalith.EventStore/Hexalith.EventStore.csproj \
    -c Release -o /app/publish --no-restore

# Runtime
FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS runtime
RUN addgroup -S appgroup && adduser -S appuser -G appgroup
WORKDIR /app
COPY --from=build /app/publish .
USER appuser
EXPOSE 8080
ENTRYPOINT ["dotnet", "Hexalith.EventStore.dll"]
