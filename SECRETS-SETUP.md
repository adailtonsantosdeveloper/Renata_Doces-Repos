# Configuração de segredos e publicação (deploy)

> **Regra de ouro:** nunca escreva senhas, connection strings ou tokens em arquivos do repositório
> (ele é **público**). Segredos ficam só nas variáveis de ambiente do servidor, ou nos
> *GitHub Secrets* se você criar um workflow de deploy.

## O que o app lê

O app não conhece nomes como `AUTH_SENHA`: ele lê estas variáveis de ambiente.

| Variável de ambiente | Obrigatória | Para que serve |
|---|---|---|
| `ConnectionStrings__DefaultConnection` | **Sim** | Conexão com o SQL Server |
| `Auth__Usuario` + `Auth__Senha` | Recomendada | Cria a conta inicial na 1ª inicialização (senha com **mín. 10 caracteres**). Informe as duas ou nenhuma |
| `Auth__CodigoConvite` | Recomendada | Código (mín. 10 caracteres) exigido para criar novas contas pela tela "Cadastre-se" |
| `Auth__CookieSempreSeguro` | Opcional | `true` para forçar cookie Secure quando há https de ponta a ponta |
| `DataProtection__KeysPath` | Opcional | Pasta persistente (volume) para as sessões sobreviverem a reinícios |
| `Database__MigrateOnStartup` | Opcional | `false` para não aplicar migrations ao iniciar |

> ⚠️ **Em servidor público, defina `Auth__Usuario`/`Auth__Senha` (ou `Auth__CodigoConvite`) antes
> do primeiro acesso.** Sem isso, a primeira pessoa que abrir o site cria a conta de administrador.

Use valores **seus e únicos**: senha longa e aleatória gerada por um gerenciador de senhas.
Não copie exemplos deste guia.

## Opção A: Render (ou outro serviço que roda Docker)

Não precisa de GitHub Secrets. No painel do serviço, em **Environment**, cadastre as variáveis
da tabela acima (`ConnectionStrings__DefaultConnection`, `Auth__Usuario`, `Auth__Senha`,
`Auth__CodigoConvite`). O Render usa o `Dockerfile` do repositório e a porta da variável `PORT`
(o app já respeita). O endereço `/health` serve para o health check.

## Opção B: Azure Container Instances com deploy automático (GitHub Actions)

Hoje o repositório só tem o workflow `build.yml` (compila e valida o Dockerfile, sem secrets).
**Não existe workflow de deploy**: é preciso criá-lo. Se for criar, estes são os secrets
(menu **Settings → Secrets and variables → Actions → New repository secret**) e a que variável
do app cada um deve ser ligado dentro do workflow:

| Secret do GitHub | O que é | Vira, no container |
|---|---|---|
| `REGISTRY_URL`, `REGISTRY_USERNAME`, `REGISTRY_PASSWORD` | Azure Container Registry (`az acr show` / `az acr credential show`) | usado só para publicar a imagem |
| `AZURE_CREDENTIALS` | JSON do service principal (`az ad sp create-for-rbac ... --json-auth`) | usado só pelo login no Azure |
| `AZURE_RESOURCE_GROUP` | Nome do resource group | usado só pelo deploy |
| `DB_CONNECTION_STRING` | Connection string do Azure SQL | `ConnectionStrings__DefaultConnection` |
| `AUTH_USUARIO` | Usuário da conta inicial | `Auth__Usuario` |
| `AUTH_SENHA` | Senha da conta inicial (mín. 10 caracteres) | `Auth__Senha` |
| `AUTH_CODIGO_CONVITE` | Código de convite para novas contas | `Auth__CodigoConvite` |

Formato da connection string do Azure SQL (troque os trechos entre `<>`):

```
Server=<servidor>.database.windows.net,1433;Database=<banco>;User Id=<usuario>;Password=<senha>;Encrypt=true;Connection Timeout=30;
```

Para obter o JSON de `AZURE_CREDENTIALS`:

```bash
az ad sp create-for-rbac --name "RenataDoces-CI" --role contributor \
  --scopes /subscriptions/<id-da-assinatura>/resourceGroups/<resource-group> --json-auth
```

Cole o JSON inteiro como valor do secret e **não o guarde em nenhum arquivo**. Segredos nunca
devem aparecer em logs do workflow nem em commits.

## Teste local (Docker)

Veja o `README.md`: os exemplos de `docker run` já passam as variáveis acima.

## Se um segredo vazar

Troque-o imediatamente na origem (senha do SQL, credencial do Azure, código de convite) e
atualize o servidor ou o GitHub Secret. Apagar o commit não basta: o histórico é público.
