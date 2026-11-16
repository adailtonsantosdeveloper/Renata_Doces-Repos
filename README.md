# 🧁 Renata Doces

Sistema web para gerenciar uma micro doceria: cadastro de **clientes** e **produtos**, registro de **pedidos** (com total calculado pelo servidor) e histórico de vendas, com **contas de usuário** para entrar. Funciona no **iPhone**, em qualquer celular e no computador, direto pelo navegador, e pode ser instalado na Tela de Início como se fosse um aplicativo.

## Códigos automáticos

Cliente (`CLI-0001`), produto (`PRD-0001`) e pedido (`PED-0001`) recebem o código do próprio sistema, a partir do número sequencial do banco. Os campos de código nas telas são somente leitura. Antes de salvar, a tela mostra o código **previsto** (`GET /api/<recurso>/proximo-codigo`); o código definitivo é o devolvido ao salvar. Produtos antigos mantêm o código que já tinham.

## Tecnologias

| Camada | Tecnologia | Por quê |
|---|---|---|
| API | ASP.NET Core 9 (C#) | Estável, muito rápido e multiplataforma (Windows, Linux, Docker). Para o iPhone a linguagem do servidor não faz diferença: ele só precisa de um navegador. |
| Banco | SQL Server + Entity Framework Core 9 | Já usado nas migrations do projeto. |
| Front-end | HTML + CSS + JavaScript puro | Sem build e sem dependências: carrega rápido no Safari e não tem biblioteca de terceiros para ficar desatualizada. |
| Publicação | Docker (imagem oficial .NET, usuário sem privilégios) | Roda igual em qualquer nuvem. |

## Como rodar no Visual Studio

1. Requisitos: Visual Studio 2022 (17.12 ou mais novo, por causa do .NET 9) com a carga de trabalho **ASP.NET e desenvolvimento para a Web**. O **SQL Server LocalDB** já vem junto.
2. Pressione **F5**. As migrations são aplicadas sozinhas e o banco `RenataDocesDb` é criado no LocalDB.
3. Abra `http://localhost:5167`, toque em **Não tem uma conta? Cadastre-se** e crie a sua conta (veja [Contas de usuário](#contas-de-usuário-e-cadastro)).

Opcional: para já subir com uma conta criada, clique com o botão direito no projeto → **Gerenciar Segredos do Usuário** e cole (a senha precisa ter **no mínimo 10 caracteres**; esse arquivo fica fora do repositório):

```json
{
  "Auth": {
    "Usuario": "renata",
    "Senha": "troque-por-uma-senha-longa"
  }
}
```

## Contas de usuário e cadastro

- As contas ficam na tabela `Usuarios` do banco. **A senha nunca é guardada**: só o hash (PBKDF2 com sal aleatório, do próprio ASP.NET Core).
- Na tela de login há o botão **Não tem uma conta? Cadastre-se**. O formulário pede usuário (3 a 60 caracteres: letras sem acento, números, `.`, `-`, `_`, `@`), senha (10 a 128 caracteres) e confirmação da senha. Depois de criar a conta, a tela volta para o login.
- **Quem pode se cadastrar.** Como o sistema guarda dados pessoais de clientes, o cadastro não fica aberto para qualquer visitante:
  - **Com `Auth__CodigoConvite` definido:** só quem informar o código cria conta.
  - **Sem código:** só a **primeira** conta pode ser criada; depois disso o cadastro fecha.
- ⚠️ **Em um servidor público**, defina `Auth__CodigoConvite` **antes** de publicar (ou crie a primeira conta imediatamente). Sem isso, o primeiro visitante poderia criar a conta principal.
- **Esqueceu a senha?** Ainda não há "recuperar senha". Com acesso ao banco, apague a conta (`DELETE FROM Usuarios WHERE NomeUsuario = 'renata';`) e cadastre de novo (com o código de convite, se estiver configurado, ou direto se a tabela ficar vazia).
- `Auth__Usuario` e `Auth__Senha` são **opcionais**: se informados (os dois juntos), criam uma conta na primeira inicialização. Se a conta já existir, nada muda; trocar a senha na variável depois **não** altera a senha gravada.

## Configuração

Tudo vem de configuração, nada de segredo fica no código. Em produção use variáveis de ambiente (`__` no lugar de `:`).

| Variável | Obrigatória | Descrição |
|---|---|---|
| `ConnectionStrings__DefaultConnection` | Sim | Connection string do SQL Server. |
| `Auth__CodigoConvite` | Recomendada em produção | Código exigido para criar novas contas. Sem ele, só a primeira conta pode ser criada. |
| `Auth__Usuario` e `Auth__Senha` | Não | Conta inicial criada na primeira inicialização (informe as duas; senha com no mínimo 10 caracteres). |
| `Auth__CookieSempreSeguro` | Não | `true` envia o cookie de sessão só por https. Use em produção com https de ponta a ponta. Padrão: o cookie é `Secure` quando a requisição chega por https (também funciona atrás de proxy e em `http://localhost`). |
| `AllowedHosts` | Recomendada | Seu domínio (ex.: `renatadoces.onrender.com`). O padrão aceita qualquer host. |
| `Database__MigrateOnStartup` | Não | `false` para não aplicar migrations ao iniciar (padrão: `true`). |
| `DataProtection__KeysPath` | Não | Pasta persistente para manter o login ativo entre reinícios. |

## Rodando com Docker

Dentro do container o ambiente é **Production**, então o `appsettings.Development.json` **não** é lido: a connection string precisa vir de variável de ambiente.

```bash
# 1) SQL Server em container (para testes locais)
docker network create renata
docker run -d --name renata-sql --network renata -p 1433:1433 -e ACCEPT_EULA=Y -e MSSQL_SA_PASSWORD="Troque-Esta-Senha-123" mcr.microsoft.com/mssql/server:2022-latest

# 2) Imagem e container do app
docker build -t renata-doces .
docker run -d --name renata-doces --network renata -p 8080:8080 -e "ConnectionStrings__DefaultConnection=Server=renata-sql;Database=RenataDocesDb;User Id=sa;Password=Troque-Esta-Senha-123;TrustServerCertificate=True" renata-doces
```

Abra `http://localhost:8080`, toque em **Cadastre-se** e crie a primeira conta. Para exigir convite desde o início, acrescente `-e "Auth__CodigoConvite=um-codigo-so-seu"` ao `docker run` do app. Cada comando acima vai em uma linha só (no PowerShell e no cmd, não use `\`).

- **As migrations rodam sozinhas** quando o app inicia (`Database__MigrateOnStartup`, padrão `true`), inclusive a que cria a tabela `Usuarios`. Se o SQL Server ainda estiver subindo, o app tenta de novo por cerca de 1 minuto antes de desistir (veja os avisos em `docker logs renata-doces`).
- **Atualizando uma versão já instalada:** reconstrua a imagem e recrie o container (`docker build` + `docker rm -f renata-doces` + `docker run`). Os dados ficam no SQL Server, e a migration nova só acrescenta a tabela `Usuarios`.
- **Aplicar as migrations manualmente** (com `Database__MigrateOnStartup=false`): na pasta do projeto, `dotnet ef database update` com a variável `ConnectionStrings__DefaultConnection` apontando para o banco (por exemplo `Server=localhost,1433;Database=RenataDocesDb;User Id=sa;Password=...;TrustServerCertificate=True`). Se `Auth__Usuario` e `Auth__Senha` estiverem definidos, a conta inicial continua sendo criada ao iniciar, então rode as migrations antes de subir o app.
- `TrustServerCertificate=True` é só para o SQL Server de teste em container. Em produção use um banco com certificado válido e `Encrypt=True`.

## Servidor próprio (VPS)

Para hospedar tudo (site, app e banco) num VPS pequeno, com HTTPS automático, limites de memória e backup, use o `docker-compose.yml` e siga o guia [`deploy/LEIA-ME.md`](deploy/LEIA-ME.md).

## Publicando (Docker / Render)

- O `Dockerfile` respeita a variável `PORT` da plataforma (padrão 8080).
- Configure o **Health Check Path** como `/health`: ele devolve 200 só se o app **e o banco** estiverem respondendo.
- O Render **não oferece SQL Server**. Use um SQL Server externo (por exemplo, Azure SQL Database) e informe a connection string na variável acima, **com `Encrypt=True`** e **sem** `TrustServerCertificate=True`. Se preferir hospedar tudo junto, a alternativa é migrar para PostgreSQL (exige gerar novas migrations).
- Defina `Auth__CodigoConvite` e, com https de ponta a ponta, `Auth__CookieSempreSeguro=true`.
- Sem disco persistente, a sessão de login é encerrada a cada novo deploy (é só entrar de novo).
- O app confia nos cabeçalhos `X-Forwarded-*` do proxy da plataforma (necessário para o limite de tentativas por IP e para o cookie `Secure`). Se colocar **mais de um proxy** na frente (ex.: Cloudflare + Render), aumente `ForwardLimit` em `ForwardedHeadersOptions` no `Program.cs`; e nunca exponha a porta do container diretamente na internet.

## No iPhone

Abra o endereço no **Safari** → botão **Compartilhar** → **Adicionar à Tela de Início**. O app abre em tela cheia, com ícone próprio. A interface usa navegação na parte de baixo da tela, campos que não dão zoom automático, teclado numérico no preço e link de ligação direta no telefone do cliente.

## Segurança: o que foi corrigido

| Problema encontrado | Correção |
|---|---|
| API aberta: qualquer pessoa com o link lia e apagava tudo | Login obrigatório (cookie `HttpOnly`, `SameSite=Lax`, `Secure` em https). Toda rota exige sessão, exceto login, cadastro e `/health`. |
| Credencial única na configuração, sem como criar contas | Contas no banco, com senha só como hash (PBKDF2 + sal), usuário único e cadastro fechado depois da primeira conta (ou protegido por código de convite). |
| Força bruta na senha e no código de convite | Limite de 5 tentativas/min por IP no login, 5 cadastros/10 min por IP e 300 requisições/min no geral; o login gasta o mesmo tempo com usuário existente ou não, e o código de convite é comparado em tempo constante. |
| CORS liberado para qualquer site | CORS removido: o front-end é servido pela própria API (mesma origem). |
| XSS: o front-end montava HTML com nomes digitados pelo usuário | Tudo é inserido como texto puro (`textContent`) e há uma Content-Security-Policy sem scripts/estilos inline. |
| Overposting (cliente da API podia enviar `id` e outros campos) | Entradas em DTOs com validação (tamanhos, e-mail, telefone, preço, quantidade). |
| Strings de conexão fixa no código | Vem de configuração; o app falha cedo e com mensagem clara se faltar. |
| Documentação OpenAPI pública em produção | Só em desenvolvimento. |
| Erros 500 com detalhes internos | Resposta padronizada (`ProblemDetails`) sem stack trace em produção. |
| Cabeçalhos de segurança ausentes | `X-Content-Type-Options`, `X-Frame-Options`, `Referrer-Policy`, `Permissions-Policy`, `HSTS`, `Cache-Control: no-store` na API. |
| Corpo de requisição de até 30 MB | Limite de 64 KB e sem o cabeçalho `Server`. |
| Container rodando como administrador | Executa como usuário sem privilégios. |

SQL Injection não era um risco: todas as consultas passam pelo Entity Framework (parametrizadas).

## Bugs de dados corrigidos

- **Exclusão em cascata**: apagar um cliente apagava todos os pedidos dele, e apagar um produto removia os itens de pedidos antigos. Agora a API responde `409` nesses casos.
- **Referência circular**: `GET /api/pedidos` tentava serializar `Pedido → Itens → Pedido`. Agora devolve um formato próprio, em uma única consulta.
- **Datas**: passam a ser gravadas em UTC e exibidas no horário do aparelho.
- **Preço histórico**: cada item guarda o preço da época da venda; mudar o preço do produto não altera pedidos antigos.
- **Desempenho**: consultas só de leitura sem rastreamento, produtos do pedido buscados em uma consulta (antes era uma por item), conexão com repetição automática em falhas transitórias.

## Como verificar a integração com o banco

1. **`GET /health`** devolve `200` somente se o app consegue falar com o banco.
2. Rode o roteiro do arquivo **`Renata_Doces.http`** (Visual Studio 2022 17.8+). Ele cobre cadastro de usuário, login, validações, conflitos (`409`) e o cálculo do total.
3. No **Gerenciador de Objetos do SQL Server**, confira as tabelas `Clientes`, `Produtos`, `Pedidos`, `PedidoItens`, `Usuarios` e `__EFMigrationsHistory` (4 migrations; a última é `AdicionarUsuarios`). Na coluna `SenhaHash` deve haver um texto longo e ilegível, nunca a senha.
4. `dotnet ef migrations has-pending-model-changes` não deve apontar diferenças.
5. Antes de publicar: `dotnet list package --vulnerable` para checar dependências com falhas conhecidas.

## Próximos passos recomendados

- **Recuperar senha e trocar senha** pela própria tela, e uma lista de usuários para quem administra.
- **Travar a exclusão também no banco.** Hoje a proteção contra apagar clientes/produtos com pedidos está na API. Para blindar no próprio banco, adicione em `AppDbContext.OnModelCreating`:

  ```csharp
  modelBuilder.Entity<Pedido>().HasOne(p => p.Cliente).WithMany()
      .HasForeignKey(p => p.ClienteId).OnDelete(DeleteBehavior.Restrict);
  modelBuilder.Entity<PedidoItem>().HasOne(i => i.Produto).WithMany()
      .HasForeignKey(i => i.ProdutoId).OnDelete(DeleteBehavior.Restrict);
  ```

  e rode `Add-Migration RestringirExclusoes` e `Update-Database` no Console do Gerenciador de Pacotes.
- Controle de **insumos, receitas e compras** (previstos na descrição do projeto) e relatórios de vendas.
- **Backup** automático do banco.
- Testes automatizados de integração.

## Licença

MIT. Veja o arquivo `LICENSE`.
