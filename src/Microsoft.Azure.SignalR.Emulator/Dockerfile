# Make sure to check the SDK version required to build the latest version 
# of Emulator here: https://www.nuget.org/packages/Microsoft.Azure.SignalR.Emulator
FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build 

WORKDIR /emulator

RUN dotnet tool install -g Microsoft.Azure.SignalR.Emulator

COPY . .

ENV PATH="/root/.dotnet/tools:${PATH}"

ENTRYPOINT ["asrs-emulator", "start", "-i", "0.0.0.0"]
