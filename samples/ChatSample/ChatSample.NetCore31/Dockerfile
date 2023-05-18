##################################################
# Build stage
##################################################

FROM mcr.microsoft.com/dotnet/core/sdk:3.0

COPY ./ /home/signalr-src

WORKDIR /home/signalr-src/samples/ChatSample/ChatSample 
RUN dotnet build && \
    dotnet publish -r ubuntu.16.04-x64 -c Release -o /home/build/

##################################################
# Final stage
##################################################

FROM mcr.microsoft.com/dotnet/core/runtime:3.0

COPY --from=0 /home/build/ /home/SignalR

WORKDIR /home/SignalR

EXPOSE 5050

CMD ["./ChatSample"]
