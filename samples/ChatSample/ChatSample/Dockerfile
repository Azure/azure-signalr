##################################################
# Build stage
##################################################

FROM mcr.microsoft.com/dotnet/sdk:8.0

COPY ./ /home/signalr-src

WORKDIR /home/signalr-src/samples/ChatSample/ChatSample 
RUN dotnet build && \
    dotnet publish -r linux-x64 -c Release -o /home/build/

##################################################
# Final stage
##################################################

FROM mcr.microsoft.com/dotnet/aspnet:8.0

COPY --from=0 /home/build/ /home/SignalR

WORKDIR /home/SignalR

EXPOSE 5050

CMD ["./ChatSample", "--urls", "http://0.0.0.0:5050"]
