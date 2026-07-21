FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY PantryService/PantryService/PantryService.csproj PantryService/PantryService/
RUN dotnet restore PantryService/PantryService/PantryService.csproj

COPY PantryService/PantryService/ PantryService/PantryService/
WORKDIR /src/PantryService/PantryService
RUN dotnet publish -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "PantryService.dll"]
