ARG EMULATOR_FEED=https://api.nuget.org/v3/index.json
ARG EMULATOR_VERSION

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS installer

ARG EMULATOR_FEED
ARG EMULATOR_VERSION

RUN test -n "${EMULATOR_VERSION}" \
    && dotnet tool install --global Microsoft.Azure.SignalR.Emulator \
        --version "${EMULATOR_VERSION}" \
        --add-source "${EMULATOR_FEED}" \
        --no-cache

FROM mcr.microsoft.com/dotnet/aspnet:8.0

ARG EMULATOR_VERSION

LABEL org.opencontainers.image.description="Local emulator for Azure SignalR Service serverless scenarios" \
      org.opencontainers.image.source="https://github.com/Azure/azure-signalr" \
      org.opencontainers.image.title="Azure SignalR Emulator" \
      org.opencontainers.image.version="${EMULATOR_VERSION}"

WORKDIR /emulator
COPY --from=installer /root/.dotnet/tools /root/.dotnet/tools

ENV PATH="/root/.dotnet/tools:${PATH}"

EXPOSE 8888

ENTRYPOINT ["asrs-emulator", "start", "-i", "0.0.0.0"]
