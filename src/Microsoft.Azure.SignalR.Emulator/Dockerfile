FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build

ARG BUILD_NUMBER=t000
ARG VERSION_SUFFIX=preview1

WORKDIR /src

COPY Directory.Build.props version.props microsoft.png ./
COPY build/ build/
COPY src/Common/MemoryBufferWriter.cs src/Common/MemoryBufferWriter.cs
COPY src/Microsoft.Azure.SignalR.Emulator/ src/Microsoft.Azure.SignalR.Emulator/

RUN dotnet publish src/Microsoft.Azure.SignalR.Emulator/Microsoft.Azure.SignalR.Emulator.csproj \
    --configuration Release \
    --output /emulator \
    /p:BuildNumber="${BUILD_NUMBER}" \
    /p:IsFinalBuild=true \
    /p:VersionSuffix="${VERSION_SUFFIX}"

FROM mcr.microsoft.com/dotnet/aspnet:8.0

ARG SOURCE_REVISION=unknown
ARG VERSION=dev

LABEL org.opencontainers.image.description="Local emulator for Azure SignalR Service serverless scenarios" \
      org.opencontainers.image.revision="${SOURCE_REVISION}" \
      org.opencontainers.image.source="https://github.com/Azure/azure-signalr" \
      org.opencontainers.image.title="Azure SignalR Emulator" \
      org.opencontainers.image.version="${VERSION}"

WORKDIR /emulator
COPY --from=build /emulator .

EXPOSE 8888

ENTRYPOINT ["dotnet", "Microsoft.Azure.SignalR.Emulator.dll", "start", "-i", "0.0.0.0"]
