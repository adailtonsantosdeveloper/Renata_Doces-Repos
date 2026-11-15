using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Renata_Doces.Data;
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

var authOptions = builder.Configuration.GetSection(AuthOptions.SectionName).Get<AuthOptions>() ?? new AuthOptions();
if (string.IsNullOrWhiteSpace(authOptions.Usuario) || authOptions.Senha.Length < 10)
{
    throw new InvalidOperationException(
        "Credenciais de acesso não configuradas. Defina 'Auth:Usuario' e 'Auth:Senha' " +
        "(a senha precisa ter pelo menos 10 caracteres). Em produção use as variáveis de ambiente " +
        "Auth__Usuario e Auth__Senha; em desenvolvimento use 'Manage User Secrets'. Veja o README.");
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
        // Em produção o cookie só trafega por https (o proxy informa o esquema via X-Forwarded-Proto).
        options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
            ? CookieSecurePolicy.SameAsRequest
            : CookieSecurePolicy.Always;
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

// =====================================================================
// 6. Banco: aplica migrations pendentes na inicialização
//    (desligue com Database:MigrateOnStartup=false se preferir rodar manualmente)
// =====================================================================
if (app.Configuration.GetValue("Database:MigrateOnStartup", true))
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
}

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
