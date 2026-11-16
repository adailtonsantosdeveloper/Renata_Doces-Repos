#!/usr/bin/env bash
# Backup diário do banco: gera um .bak compactado, copia para o servidor, mantém os últimos N
# e usa o modo de recuperação SIMPLE (o log de transações não cresce sem limite).
# Agende (como root):  0 3 * * * /opt/renata-doces/deploy/backup.sh >> /var/log/renata-backup.log 2>&1
set -euo pipefail

PASTA_PROJETO="$(cd "$(dirname "$0")/.." && pwd)"
DESTINO="${BACKUP_DIR:-/opt/renata-backups}"
MANTER="${BACKUP_KEEP:-7}"
BANCO="RenataDocesDb"
NOME="${BANCO}_$(date +%Y%m%d_%H%M%S).bak"

cd "$PASTA_PROJETO"
mkdir -p "$DESTINO"
chmod 700 "$DESTINO"

# Lê SA_PASSWORD do .env sem exportá-la para o ambiente do sistema.
SA_PASSWORD="$(grep -E '^SA_PASSWORD=' .env | head -n1 | cut -d= -f2-)"
[ -n "$SA_PASSWORD" ] || { echo "SA_PASSWORD não encontrada em .env" >&2; exit 1; }

SQLCMD=(docker compose exec -T -e "SQLCMDPASSWORD=$SA_PASSWORD" sql /opt/mssql-tools18/bin/sqlcmd -C -S localhost -U sa -b)

"${SQLCMD[@]}" -Q "ALTER DATABASE [$BANCO] SET RECOVERY SIMPLE;"
"${SQLCMD[@]}" -Q "BACKUP DATABASE [$BANCO] TO DISK = N'/var/opt/mssql/data/$NOME' WITH COMPRESSION, CHECKSUM, INIT;"

docker compose cp "sql:/var/opt/mssql/data/$NOME" "$DESTINO/$NOME"
docker compose exec -T sql rm -f "/var/opt/mssql/data/$NOME"
chmod 600 "$DESTINO/$NOME"

# Mantém só os últimos N backups.
ls -1t "$DESTINO"/${BANCO}_*.bak 2>/dev/null | tail -n +$((MANTER + 1)) | xargs -r rm -f --

echo "$(date -Is) backup ok: $DESTINO/$NOME"
