# Implantação no Turbo Cloud VPS (Docker)

Guia para subir o sistema inteiro (site, app e banco) num VPS Linux de 5 GB de RAM e 15 GB de disco.
Só o **Caddy** fica aberto na internet (portas 80 e 443): ele cuida do HTTPS. O app e o SQL Server ficam numa
rede interna do Docker, sem porta publicada.

> Estes arquivos foram escritos sem poder testá-los num servidor real. Faça o primeiro deploy com calma e,
> se algo falhar, copie a saída de `docker compose logs` e peça ajuda.

## 1. Antes de começar
1. **Domínio:** crie um registro DNS tipo `A` apontando para o IP do VPS (ex.: `doces.seudominio.com.br`).
   Sem domínio próprio, dá para usar `IP-COM-HIFENS.sslip.io` (ex.: `203-0-113-10.sslip.io`), que o Let's Encrypt aceita.
2. **VPS:** Ubuntu 22.04/24.04 LTS, acesso SSH.

## 2. Preparar o servidor (uma vez, como root)
```bash
# Atualizações de segurança automáticas, firewall e proteção contra tentativas de senha no SSH
apt update && apt -y upgrade
apt -y install ufw fail2ban unattended-upgrades ca-certificates curl git
ufw default deny incoming && ufw default allow outgoing
ufw allow 22/tcp && ufw allow 80/tcp && ufw allow 443/tcp && ufw allow 443/udp
ufw --force enable

# Docker (repositório oficial): siga https://docs.docker.com/engine/install/ubuntu/
```
**SSH só com chave:** depois de testar o login por chave, em `/etc/ssh/sshd_config` deixe
`PasswordAuthentication no` e `PermitRootLogin prohibit-password`, e rode `systemctl restart ssh`.

> O Docker publica portas passando por cima do `ufw`. Por isso o `docker-compose.yml` só publica 80 e 443;
> não adicione `ports:` ao SQL Server nem ao app.

## 3. Baixar e configurar
```bash
git clone https://github.com/adailtonsantosdeveloper/Renata_Doces-Repos.git /opt/renata-doces
cd /opt/renata-doces
cp .env.example .env
chmod 600 .env
nano .env        # preencha DOMAIN, SA_PASSWORD, AUTH_USUARIO, AUTH_SENHA, AUTH_CODIGO_CONVITE
```
Valores sugeridos (gere senhas únicas, nunca reutilize):
```bash
echo "SA_PASSWORD=Aa1-$(openssl rand -hex 16)"
echo "AUTH_SENHA=Aa1-$(openssl rand -hex 12)"
echo "AUTH_CODIGO_CONVITE=$(openssl rand -hex 8)"
```
Evite `; = " ' $` e espaços nos valores.

### Sobre as variáveis de login e cadastro
| Variável | Para que serve |
|---|---|
| `AUTH_USUARIO` / `AUTH_SENHA` | Criam a conta principal na **primeira** inicialização. A senha é gravada só como hash; trocar o valor depois **não** muda a senha já criada. Mínimo de 10 caracteres. |
| `AUTH_CODIGO_CONVITE` | Sem ele, ninguém além de quem já está logado cria contas. Passe-o só a quem for ter acesso. |
| `Auth__CookieSempreSeguro` | Já definido como `true` no compose (o acesso é sempre HTTPS). |

O compose **recusa subir** se `SA_PASSWORD`, `AUTH_USUARIO`, `AUTH_SENHA` ou `AUTH_CODIGO_CONVITE` estiverem vazios.
Isso impede o servidor de ir ao ar com a tela de cadastro aberta para o primeiro visitante.
O arquivo `.env` fica só no servidor (permissão 600), nunca no GitHub.

## 4. Subir
```bash
docker compose up -d --build
docker compose ps                 # sql deve ficar "healthy"
docker compose logs -f app        # deve mostrar "Banco de dados atualizado" e "Application started"
```
Abra `https://SEU-DOMINIO` e entre com `AUTH_USUARIO` / `AUTH_SENHA`.

## 5. Backup diário (essencial)
O script `deploy/backup.sh` gera o backup compactado, mantém os 7 últimos em `/opt/renata-backups` e coloca o banco
em modo de recuperação SIMPLE (o log de transações não cresce sem limite).
```bash
crontab -e    # como root
0 3 * * * /opt/renata-doces/deploy/backup.sh >> /var/log/renata-backup.log 2>&1
```
**Copie os backups para fora do servidor** (seu computador, Google Drive/OneDrive via `rclone`, ou outro servidor com
`scp`). Backup só no mesmo disco não protege contra perda do VPS.

Restaurar (exemplo): copie o `.bak` para `/var/opt/mssql/data/` do container `sql` e rode `RESTORE DATABASE` com `sqlcmd`.

## 6. Memória e disco (5 GB de RAM / 15 GB de disco)
| Serviço | Limite | Observação |
|---|---|---|
| SQL Server Express | 2 GB (`MSSQL_MEMORY_LIMIT_MB=1280`) | edição gratuita; o limite impede que ele tome toda a RAM |
| App .NET | 384 MB | coleta de lixo simples, sem PGO nem diagnóstico |
| Caddy | 96 MB | |

Sobram cerca de 2 GB para o sistema operacional. Para conferir: `docker stats --no-stream` e `free -h`.

Para o disco não encher:
- Os logs do Docker já têm rotação (3 × 10 MB por serviço).
- O SQL Server não gera arquivos de falha (`core: 0`).
- Depois de cada atualização, limpe imagens antigas: `docker image prune -f`.
- Confira de vez em quando: `df -h /` e `docker system df`.

## 7. Atualizar o sistema
```bash
cd /opt/renata-doces
git pull origin main
docker compose up -d --build
docker image prune -f
```
As migrations do banco são aplicadas sozinhas ao iniciar.

## 8. Problemas comuns
| Sintoma | O que verificar |
|---|---|
| Caddy não obtém certificado | DNS apontando para o IP certo; portas 80 e 443 liberadas no `ufw` e no painel do provedor |
| `app` reinicia sem parar | `docker compose logs app`: senha do SQL, conta inicial mal formada ou erro de permissão em `/keys` |
| Erro de "read-only file system" no app | Remova a linha `read_only: true` do serviço `app` e suba de novo |
| `sql` não fica `healthy` | RAM insuficiente (`free -h`) ou `SA_PASSWORD` fora da regra de complexidade |
