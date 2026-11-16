using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Renata_Doces.Data;
using Renata_Doces.Models;
using Renata_Doces.Security;

var builder = WebApplication.CreateBuilder(args);

// =====================================================================
// 1. Configuração obrigatória: o app falha cedo, com mensagem clara,
//    em vez de subir "meio quebrado". Nada de segredo fica no código.
// =====================================================================
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException(
        "Connection string não configurada. Defina 'ConnectionStrings:DefaultConnection' " +
        "(em produção, a variável de ambiente ConnectionStrings__DefaultConnection). Veja o README.");
}

// As contas de acesso ficam no banco (tabela Usuarios) e são criadas pela tela de cadastro.
// Auth:Usuario / Auth:Senha são OPCIONAIS: se informados, criam uma conta inicial na primeira
// inicialização. Informar só um dos dois é erro de configuração, e o app avisa logo ao subir.
var authOptions = builder.Configuration.GetSection(AuthOptions.SectionName).Get<AuthOptions>() ?? new AuthOptions();
var informouContaInicial = !string.IsNullOrWhiteSpace(authOptions.Usuario) || authOptions.Senha.Length > 0;
if (informouContaInicial && (string.IsNullOrWhiteSpace(authOptions.Usuario)
                             || authOptions.Usuario.Trim().Length > 60
                             || authOptions.Senha.Length < 10))
{
    throw new InvalidOperationException(
        "Conta inicial mal configurada. Defina 'Auth:Usuario' (até 60 caracteres) e 'Auth:Senha' (mínimo de 10 caracteres) " +
        "juntos, ou remova os dois e crie a conta pela tela de cadastro. Em produção use as variáveis de " +
        "ambiente Auth__Usuario e Auth__Senha; em desenvolvimento use 'Manage User Secrets'. Veja o README.");
}
builder.Services.Configure<AuthOptions>(builder.Configuration.GetSection(AuthOptions.SectionName));

// =====================================================================
// 2. Servidor web: sem cabeçalho "Server" e com limite de corpo da requisição
//    (a API só recebe JSON pequeno; o padrão de 30 MB facilita abuso).
// =====================================================================
builder.WebHost.ConfigureKestrel(options =>
{
    options.AddServerHeader = false;
    options.Limits.MaxRequestBodySize = 64 * 1024;
});

// Atrás do proxy do Render (ou de qualquer proxy reverso) o app precisa enxergar
// o IP real e o esquema https para que o rate limit e o cookie Secure funcionem.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

// =====================================================================
// 3. Banco de dados (SQL Server) com repetição automática em falhas transitórias
// =====================================================================
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(connectionString, sql =>
        sql.EnableRetryOnFailure(maxRetryCount: 5, maxRetryDelay: TimeSpan.FromSeconds(10), errorNumbersToAdd: null)));

// Hash de senhas (PBKDF2 com sal aleatório) do próprio ASP.NET Core; senhas nunca são guardadas em texto.
builder.Services.AddSingleton<IPasswordHasher<Usuario>, PasswordHasher<Usuario>>();

// =====================================================================
// 4. Autenticação por cookie (mesma origem do front-end => sem CORS e sem token em JS)
// =====================================================================
var dataProtection = builder.Services.AddDataProtection().SetApplicationName("RenataDoces");
var keysPath = builder.Configuration["DataProtection:KeysPath"];
if (!string.IsNullOrWhiteSpace(keysPath))
{
    // Opcional: mantém as sessões válidas entre reinícios se houver um disco persistente.
    dataProtection.PersistKeysToFileSystem(new DirectoryInfo(keysPath));
}

builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = "renata.auth";
        options.Cookie.HttpOnly = true;                           // JS não lê o cookie (mitiga roubo via XSS)
        options.Cookie.SameSite = SameSiteMode.Lax;               // mitiga CSRF
        // "Secure" quando a requisição chegou por https (atrás de proxy o X-Forwarded-Proto informa isso).
        // Assim também dá para testar em http://localhost no Docker: com "Always", o Safari descartaria o
        // cookie e o login pareceria funcionar mas a sessão cairia na hora. Em produção com https de ponta
        // a ponta, defina Auth__CookieSempreSeguro=true para forçar.
        options.Cookie.SecurePolicy = authOptions.CookieSempreSeguro
            ? CookieSecurePolicy.Always
            : CookieSecurePolicy.SameAsRequest;
        options.ExpireTimeSpan = TimeSpan.FromDays(7);
        options.SlidingExpiration = true;

        // API: responde 401/403 em vez de redirecionar para uma página de login.
        options.Events.OnRedirectToLogin = context =>
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return Task.CompletedTask;
        };
        options.Events.OnRedirectToAccessDenied = context =>
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return Task.CompletedTask;
        };
    });

// Política padrão: TUDO exige login, exceto o que for marcado com [AllowAnonymous].
builder.Services.AddAuthorizationBuilder()
    .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());

// =====================================================================
// 5. Limitação de taxa (força bruta no login e abuso geral por IP)
// =====================================================================
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.AddPolicy("login", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "desconhecido",
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 5,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0
        }));

    // Criação de contas: poucas por IP, para impedir tentativas em massa do código de convite.
    options.AddPolicy("cadastro", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "desconhecido",
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 5,
            Window = TimeSpan.FromMinutes(10),
            QueueLimit = 0
        }));

    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "desconhecido",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 300,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));
});

builder.Services.AddControllers();
builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();

var app = builder.Build();

// Em produção, sem conta inicial nem código de convite, o primeiro visitante do site criaria a conta principal.
if (!app.Environment.IsDevelopment()
    && string.IsNullOrWhiteSpace(authOptions.Usuario)
    && string.IsNullOrWhiteSpace(authOptions.CodigoConvite))
{
    app.Logger.LogWarning(
        "Produção sem Auth__Usuario/Auth__Senha e sem Auth__CodigoConvite: quem abrir o site primeiro poderá criar a conta principal.");
}

// =====================================================================
// 6. Banco: aplica migrations pendentes na inicialização
//    (desligue com Database:MigrateOnStartup=false se preferir rodar manualmente)
// =====================================================================
if (app.Configuration.GetValue("Database:MigrateOnStartup", true))
{
    // Com repetição: em Docker o app pode subir antes de o SQL Server estar pronto.
    await BancoInicializacao.AplicarMigrationsAsync(app.Services, app.Logger);
}

// Conta inicial (só faz algo se Auth:Usuario e Auth:Senha estiverem definidos). Fica fora do "if" acima
// para valer também com MigrateOnStartup=false; nesse caso o banco já precisa estar migrado.
await UsuarioInicial.GarantirAsync(app.Services, app.Logger);

// =====================================================================
// 7. Pipeline HTTP (a ordem importa)
// =====================================================================
app.UseForwardedHeaders();

if (app.Environment.IsDevelopment())
{
    // Documentação OpenAPI só em desenvolvimento (em produção expõe a estrutura da API).
    app.MapOpenApi().AllowAnonymous();
}
else
{
    app.UseExceptionHandler();   // respostas 500 sem stack trace (ProblemDetails)
    app.UseHsts();
}

app.Use(async (context, next) =>
{
    // OnStarting: os cabeçalhos são aplicados quando a resposta começa a ser enviada, inclusive nas
    // respostas 500 do handler de exceções (ele limpa os cabeçalhos que já tivessem sido definidos).
    context.Response.OnStarting(() =>
    {
        var headers = context.Response.Headers;
        headers["X-Content-Type-Options"] = "nosniff";
        headers["X-Frame-Options"] = "DENY";
        headers["Referrer-Policy"] = "no-referrer";
        headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=(), payment=()";
        headers["Content-Security-Policy"] =
            "default-src 'self'; img-src 'self' data:; frame-ancestors 'none'; base-uri 'self'; form-action 'self'";

        if (context.Request.Path.StartsWithSegments("/api"))
        {
            headers["Cache-Control"] = "no-store";   // dados de clientes não ficam em cache do navegador
        }

        return Task.CompletedTask;
    });

    await next();
});

app.UseDefaultFiles();   // "/" serve wwwroot/index.html
app.UseStaticFiles(new StaticFileOptions
{
    // Revalida sempre (ETag): o iPhone nunca fica preso a um app.js antigo.
    OnPrepareResponse = context => context.Context.Response.Headers.CacheControl = "no-cache"
});

app.UseRouting();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

// Verificação de saúde (usada pelo Render/Docker): confirma que o banco responde.
app.MapGet("/health", async (AppDbContext db, CancellationToken cancellationToken) =>
{
    using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
    cts.CancelAfter(TimeSpan.FromSeconds(5));

    try
    {
        return await db.Database.CanConnectAsync(cts.Token)
            ? Results.Ok(new { status = "ok" })
            : Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
    }
    catch (OperationCanceledException)
    {
        return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
    }
}).AllowAnonymous();

app.MapControllers();

app.Run();
