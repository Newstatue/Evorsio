FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["Evorsio.BotService/Evorsio.BotService.csproj", "Evorsio.BotService/"]
COPY ["Evorsio.ServiceDefaults/Evorsio.ServiceDefaults.csproj", "Evorsio.ServiceDefaults/"]
RUN dotnet restore "Evorsio.BotService/Evorsio.BotService.csproj"

COPY . .
RUN dotnet publish "Evorsio.BotService/Evorsio.BotService.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "Evorsio.BotService.dll"]