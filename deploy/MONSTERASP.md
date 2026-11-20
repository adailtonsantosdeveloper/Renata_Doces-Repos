# Hospedagem na MonsterASP.NET

A MonsterASP.NET roda ASP.NET Core em servidor **Windows com IIS**, sem Docker. Por isso o `docker-compose.yml` e o
`deploy/LEIA-ME.md` (VPS) **não se aplicam** aqui. O app e o banco (SQL Server) ficam no mesmo provedor.

> Escrito a partir da [documentação da MonsterASP.NET](https://help.monsterasp.net/) e do [site de planos](https://www.monsterasp.net/Pricing/).
> Planos e limites mudam: confira antes de contratar. Este roteiro não foi testado no servidor real.

## Planos (conferidos em 20/11/2026)

| | Grátis | Premium Single |
| --- | --- | --- |
| Preço | US$ 0 | US$ 1,95/mês no 1º ano, depois US$ 2,50 |
| Memória do site | 256 MB, não aumenta | 512 MB (aumenta, US$ 1 por 512 MB) |
| Banco SQL Server | 1 banco de 1 GB | 2 bancos de 2 GB |
| Domínio próprio | Não (só subdomínio gratuito) | Sim |
| Datacenter | Só Europa (Alemanha) | Europa ou EUA (Salt Lake City) |
| App sempre ligado | Não: para após 3 h sem acessos e o primeiro acesso depois disso demora | Sob pedido (ticket) |

O site e o banco precisam estar **no mesmo plano** (Grátis com Grátis, Premium com Premium).
Para a doceria, o plano grátis serve para testar. Para uso diário, o Premium evita a espera do primeiro acesso e
os limites de memória.

## 1. Criar o site e o banco (painel)

1. Em [admin.monsterasp.net](https://admin.monsterasp.net), crie o **site** (Websites → Add).
2. Crie um banco **MSSQL** e abra a página dele. Copie a **connection string local** (a que o site usa por dentro do
   servidor). A string remota (`dbXXXX.public.databaseasp.net`) é só para acesso de fora, como o SSMS.

## 2. Variáveis de ambiente (painel: site → Scripting → Environment variables)

Clique em **Add** para cada uma e depois em **Restart application**.

| Nome | Valor | Obrigatória |
| --- | --- | --- |
| `ConnectionStrings__DefaultConnection` | a connection string **local** do banco | Sim |
| `Auth__Usuario` | nome da conta principal | Recomendada |
| `Auth__Senha` | senha dela (mínimo 10 caracteres) | Recomendada |
| `Auth__CodigoConvite` | código (mínimo 10 caracteres) para criar novas contas | Recomendada |
| `Hospedagem__ForcarHttps` | `true` (só depois de ativar o HTTPS, passo 4) | Opcional |

Use valores seus, gerados na hora. **Nunca** escreva senhas no repositório. Sem `Auth__Usuario`/`Auth__Senha` e sem
`Auth__CodigoConvite`, quem abrir o site primeiro cria a conta principal.

As migrations do banco são aplicadas sozinhas quando o app inicia. O usuário do banco da MonsterASP.NET precisa poder
criar tabelas (é o normal; se aparecer erro de permissão no log, abra um ticket).

## 3. Publicar

### Opção A: Visual Studio (Web Deploy), a mais simples

1. No painel: site → **Deploy** → ative **WebDeploy** no cartão "Visual Studio access" e baixe o arquivo
   `.publishSettings`. **Não coloque esse arquivo no Git** (ele tem a senha de publicação; o `.gitignore` já o ignora).
2. No Visual Studio: botão direito no projeto → **Publish** → **Import profile** → escolha o arquivo.
3. Em **Show all settings**, deixe **Database** e **Entity Framework Migrations** em branco: a string de conexão vem das
   variáveis de ambiente e as migrations rodam no início do app.
4. Clique em **Publish**.

### Opção B: pacote pronto pelo GitHub + FTP

1. No GitHub: aba **Actions** → workflow **pacote-monsterasp** → **Run workflow**.
2. Baixe o artefato `renata-doces-monsterasp` (zip) e descompacte.
3. Envie **todo o conteúdo** para a pasta `wwwroot` do site por FTP/SFTP ou pelo gerenciador de arquivos do painel
   (o arquivo `Renata_Doces.dll` e o `web.config` devem ficar direto dentro de `wwwroot`).
4. No painel, clique em **Restart application**.

## 4. HTTPS

No painel: site → **HTTPS** → ative o **Let's Encrypt** (um clique). Depois, acrescente a variável
`Hospedagem__ForcarHttps=true` e reinicie, para o app redirecionar http para https. Confirme que o seu plano
oferece HTTPS antes de ligar essa variável: sem HTTPS, o redirecionamento deixaria o site inacessível.

O app já marca o cookie de sessão como `Secure` quando a conexão é HTTPS, então não é preciso `Auth__CookieSempreSeguro`.

## O que o app faz sozinho no IIS

- **Sessões entre reinícios:** guarda as chaves em `private/keys` (pasta ao lado de `wwwroot`). Sem isso, todos seriam
  deslogados a cada reinício. Só depois da primeira publicação o pessoal precisa entrar de novo.
- **IP do visitante:** no IIS não há proxy na frente, então o app **não** confia no cabeçalho `X-Forwarded-For`
  (senão qualquer um poderia forjar o IP e burlar o limite de tentativas de login). Em Docker atrás do Caddy continua confiando.
- **Limite do corpo da requisição:** 64 KB, também no IIS.
- **Memória:** modo InProcess, coleta de lixo simples e `ConserveMemory`, para caber nos 256 MB do plano grátis.

## Problemas comuns

| Sintoma | O que verificar |
| --- | --- |
| Erro 500.30 ao abrir | Painel → site → **Logs** (Application Error) e a captura de tela na página **Overview**. Causas comuns: string de conexão errada, pasta publicada incompleta, arquivos fora de `wwwroot` |
| Erro 500 só depois de um tempo parado | Banco indisponível no momento da partida: reinicie o app |
| Todos deslogados após publicar | Esperado uma vez (chaves novas). Se repetir a cada reinício, confira se `private/keys` está sendo criada |
| Site reinicia sozinho ou responde 503 | Limite de memória (256 MB no grátis). Veja o uso na página **Resource**; no Premium dá para aumentar |
| Primeiro acesso do dia lento (plano grátis) | O app para depois de 3 h sem acessos e precisa iniciar de novo |
| Latência alta no plano grátis | Datacenter só na Europa |

## Backup

A MonsterASP.NET informa backups diários externos, mas não confie só nisso: de vez em quando exporte o banco
pelo SSMS (botão direito no banco → Tasks → Export Data-tier Application) e guarde o arquivo fora do servidor.
Para acessar de fora, ative o **acesso remoto** do banco no painel e use a connection string remota.
