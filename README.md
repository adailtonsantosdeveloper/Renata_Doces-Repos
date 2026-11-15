# 🧁 Renata Doces

Sistema web para gerenciar uma micro doceria: cadastro de **clientes** e **produtos**, registro de **pedidos** (com total calculado pelo servidor) e histórico de vendas. Funciona no **iPhone**, em qualquer celular e no computador, direto pelo navegador, e pode ser instalado na Tela de Início como se fosse um aplicativo.

## Tecnologias

| Camada | Tecnologia | Por quê |
|---|---|---|
| API | ASP.NET Core 9 (C#) | Estável, muito rápido e multiplataforma (Windows, Linux, Docker). Para o iPhone a linguagem do servidor não faz diferença: ele só precisa de um navegador. |
| Banco | SQL Server + Entity Framework Core 9 | Já usado nas migrations do projeto. |
| Front-end | HTML + CSS + JavaScript puro | Sem build e sem dependências: carrega rápido no Safari e não tem biblioteca de terceiros para ficar desatualizada. |
| Publicação | Docker (imagem oficial .NET, usuário sem privilégios) | Roda igual em qualquer nuvem. |

## Como rodar no Visual Studio

1. Requisitos: Visual Studio 2022 (17.12 ou mais novo, por causa do .NET 9) com a carga de trabalho **ASP.NET e desenvolvimento para a Web**. O **SQL Server LocalDB** já vem junto.
2. **Defina seu usuário e senha** (o app se recusa a iniciar sem isso, de propósito). Clique com o botão direito no projeto → **Gerenciar Segredos do Usuário** e cole:

   ```json
   {
     "Auth": {
       "Usuario": "renata",
       "Senha": "troque-por-uma-senha-longa"
     }
   }
   ```

   A senha precisa ter **no mínimo 10 caracteres**. Esse arquivo fica fora do repositório (na sua conta do Windows).
3. Pressione **F5**. As migrations são aplicadas sozinhas e o banco `RenataDocesDb` é criado no LocalDB.
4. Abra `http://localhost:5167` e entre com o usuário e a senha definidos.

## Configuração

Tudo vem de configuração, nada de segredo fica no código. Em produção use variáveis de ambiente (`__` no lugar de `:`).

| Variável | Obrigatória | Descrição |
|---|---|---|
| `ConnectionStrings__DefaultConnection` | Sim | Connection string do SQL Server. |
| `Auth__Usuario` | Sim | Usuário de acesso. |
| `Auth__Senha` | Sim | Senha de acesso (mínimo de 10 caracteres). |
| `AllowedHosts` | Recomendada | Seu domínio (ex.: `renatadoces.onrender.com`). O padrão aceita qualquer host. |
| `Database__MigrateOnStartup` | Não | `false` para não aplicar migrations ao iniciar (padrão: `true`). |
| `DataProtection__KeysPath` | Não | Pasta persistente para manter o login ativo entre reinícios. |

## Publicando (Docker / Render)

- O `Dockerfile` respeita a variável `PORT` da plataforma (padrão 8080).
- Configure o **Health Check Path** como `/health`: ele devolve 200 só se o app **e o banco** estiverem respondendo.
- O Render **não oferece SQL Server**. Use um SQL Server externo (por exemplo, Azure SQL Database) e informe a connection string na variável acima, **com `Encrypt=True`** e **sem** `TrustServerCertificate=True`. Se preferir hospedar tudo junto, a alternativa é migrar para PostgreSQL (exige gerar novas migrations).
- Sem disco persistente, a sessão de login é encerrada a cada novo deploy (é só entrar de novo).
- O app confia nos cabeçalhos `X-Forwarded-*` do proxy da plataforma (necessário para o limite de tentativas de login por IP e para o cookie `Secure`). Se colocar **mais de um proxy** na frente (ex.: Cloudflare + Render), aumente `ForwardLimit` em `ForwardedHeadersOptions` no `Program.cs`; e nunca exponha a porta do container diretamente na internet.

## No iPhone

Abra o endereço no **Safari** → botão **Compartilhar** → **Adicionar à Tela de Início**. O app abre em tela cheia, com ícone próprio. A interface usa navegação na parte de baixo da tela, campos que não dão zoom automático, teclado numérico no preço e link de ligação direta no telefone do cliente.

## Segurança: o que foi corrigido

| Problema encontrado | Correção |
|---|---|
| API aberta: qualquer pessoa com o link lia e apagava tudo | Login obrigatório (cookie `HttpOnly`, `SameSite=Lax`, `Secure` em https). Toda rota exige sessão, exceto login e `/health`. |
| Força bruta na senha | Limite de 5 tentativas/min por IP no login e 300 requisições/min por IP no geral; comparação em tempo constante. |
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
2. Rode o roteiro do arquivo **`Renata_Doces.http`** (Visual Studio 2022 17.8+). Ele cobre login, validações, conflitos (`409`) e o cálculo do total.
3. No **Gerenciador de Objetos do SQL Server**, confira as tabelas `Clientes`, `Produtos`, `Pedidos`, `PedidoItens` e `__EFMigrationsHistory` (3 migrations).
4. `dotnet ef migrations has-pending-model-changes` não deve apontar diferenças (as entidades não foram alteradas).
5. Antes de publicar: `dotnet list package --vulnerable` para checar dependências com falhas conhecidas.

## Próximos passos recomendados

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

MIT. Veja o arquivo `LICENSE.txt`.
