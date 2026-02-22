FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["Evorsio.UserService/Evorsio.UserService.csproj", "Evorsio.UserService/"]
COPY ["Evorsio.ServiceDefaults/Evorsio.ServiceDefaults.csproj", "Evorsio.ServiceDefaults/"]
RUN dotnet restore "Evorsio.UserService/Evorsio.UserService.csproj"

COPY . .
RUN dotnet publish "Evorsio.UserService/Evorsio.UserService.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "Evorsio.UserService.dll"]