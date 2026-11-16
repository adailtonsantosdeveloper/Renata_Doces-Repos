# ---------- Estágio de build ----------
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copia só o .csproj primeiro: o "restore" fica em cache enquanto as dependências não mudarem.
COPY ["Renata_Doces.csproj", "./"]
RUN dotnet restore "Renata_Doces.csproj"

COPY . .
RUN dotnet publish "Renata_Doces.csproj" -c Release -o /app/publish --no-restore /p:UseAppHost=false

# ---------- Estágio final / execução ----------
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

# Pasta das chaves de sessão (volume persistente): criada aqui para o usuário "app" poder gravar nela.
RUN mkdir -p /keys && chown $APP_UID /keys

# Menos memória: sem GC de servidor/concorrente, sem PGO e sem o canal de diagnóstico do .NET.
ENV DOTNET_gcServer=0 \
    DOTNET_gcConcurrent=0 \
    DOTNET_TieredPGO=0 \
    DOTNET_GCConserveMemory=5 \
    DOTNET_EnableDiagnostics=0

# Executa sem privilégios de administrador (usuário "app" já existe na imagem oficial).
USER $APP_UID

EXPOSE 8080

# O Render informa a porta pela variável PORT; localmente usa 8080.
# "exec" faz o .NET receber os sinais de parada corretamente (desligamento limpo).
ENTRYPOINT ["sh", "-c", "ASPNETCORE_URLS=http://+:${PORT:-8080} exec dotnet Renata_Doces.dll"]
