FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY SourcingService/SourcingService/SourcingService.csproj SourcingService/SourcingService/
RUN dotnet restore SourcingService/SourcingService/SourcingService.csproj

COPY SourcingService/SourcingService/ SourcingService/SourcingService/
WORKDIR /src/SourcingService/SourcingService
RUN dotnet publish -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "SourcingService.dll"]
