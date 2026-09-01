# Estágio de Build
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY ["Renata_Doces.csproj", "."]
RUN dotnet restore "Renata_Doces.csproj"
COPY . .
RUN dotnet publish -c Release -o /app/publish

# Estágio Final / Execução
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app
EXPOSE 8080
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "Renata_Doces.dll"]