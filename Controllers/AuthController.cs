using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Renata_Doces.Data;
using Renata_Doces.DTOs;
using Renata_Doces.Models;
using Renata_Doces.Security;

namespace Renata_Doces.Controllers
{
    [ApiController]
    [Route("api/auth")]
    public class AuthController : ControllerBase
    {
        // Hash "de mentira", usado quando o usuário não existe: o login gasta o mesmo tempo de CPU
        // com usuário certo ou errado, e assim a resposta não revela quais nomes existem.
        private static readonly string HashFalso =
            new PasswordHasher<Usuario>().HashPassword(new Usuario(), Guid.NewGuid().ToString("N"));

        // Garante que "só a primeira conta" valha mesmo com dois cadastros simultâneos: sem isto, dois
        // pedidos ao mesmo tempo poderiam ver o banco vazio e criar duas contas (vale para uma instância).
        private static readonly SemaphoreSlim TravaCadastro = new(1, 1);

        private readonly AppDbContext _context;
        private readonly IPasswordHasher<Usuario> _hasher;
        private readonly AuthOptions _options;
        private readonly ILogger<AuthController> _logger;

        public AuthController(
            AppDbContext context,
            IPasswordHasher<Usuario> hasher,
            IOptions<AuthOptions> options,
            ILogger<AuthController> logger)
        {
            _context = context;
            _hasher = hasher;
            _options = options.Value;
            _logger = logger;
        }

        // GET: api/auth/cadastro  (a tela de cadastro usa para saber se pede o código de convite)
        [HttpGet("cadastro")]
        [AllowAnonymous]
        public async Task<IActionResult> StatusCadastro(CancellationToken cancellationToken)
        {
            var exigeCodigo = ExigeCodigoConvite();
            var primeiroAcesso = !await _context.Usuarios.AnyAsync(cancellationToken);

            return Ok(new
            {
                aberto = exigeCodigo || primeiroAcesso,
                exigeCodigo,
                primeiroAcesso
            });
        }

        // POST: api/auth/registrar  (máx. 5 por 10 minutos por IP)
        //
        // Regra de abertura do cadastro (o sistema guarda dados pessoais de clientes, então não pode ficar
        // aberto para qualquer visitante):
        //  - Com Auth:CodigoConvite definido: só quem informar o código cria conta.
        //  - Sem código: só a PRIMEIRA conta pode ser criada; depois disso o cadastro fecha.
        [HttpPost("registrar")]
        [AllowAnonymous]
        [EnableRateLimiting("cadastro")]
        public async Task<IActionResult> Registrar(RegistrarDto dto, CancellationToken cancellationToken)
        {
            await TravaCadastro.WaitAsync(cancellationToken);
            try
            {
                return await CriarConta(dto, cancellationToken);
            }
            finally
            {
                TravaCadastro.Release();
            }
        }

        private async Task<IActionResult> CriarConta(RegistrarDto dto, CancellationToken cancellationToken)
        {
            if (ExigeCodigoConvite())
            {
                var informado = dto.CodigoConvite?.Trim() ?? string.Empty;
                if (!SecureEquals(informado, _options.CodigoConvite.Trim()))
                {
                    return StatusCode(StatusCodes.Status403Forbidden, new { mensagem = "Código de convite inválido." });
                }
            }
            else if (await _context.Usuarios.AnyAsync(cancellationToken))
            {
                return StatusCode(StatusCodes.Status403Forbidden, new
                {
                    mensagem = "O cadastro está fechado. Peça ao administrador o código de convite."
                });
            }

            var nome = dto.Usuario.Trim();

            if (string.Equals(nome, dto.Senha, StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest(new { mensagem = "A senha não pode ser igual ao nome de usuário." });
            }

            if (await _context.Usuarios.AnyAsync(u => u.NomeUsuario == nome, cancellationToken))
            {
                return Conflict(new { mensagem = "Este nome de usuário já está em uso." });
            }

            var usuario = new Usuario { NomeUsuario = nome, CriadoEm = DateTime.UtcNow };
            usuario.SenhaHash = _hasher.HashPassword(usuario, dto.Senha);   // só o hash é gravado
            _context.Usuarios.Add(usuario);

            try
            {
                await _context.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException)
            {
                // Corrida: outro cadastro com o mesmo nome entrou entre a checagem e o INSERT (índice único).
                if (await _context.Usuarios.AsNoTracking().AnyAsync(u => u.NomeUsuario == nome, cancellationToken))
                {
                    return Conflict(new { mensagem = "Este nome de usuário já está em uso." });
                }

                throw;
            }

            _logger.LogInformation("Nova conta criada: {Usuario}", nome);
            return StatusCode(StatusCodes.Status201Created, new { usuario = nome });
        }

        // POST: api/auth/login  (máx. 5 tentativas por minuto por IP)
        [HttpPost("login")]
        [AllowAnonymous]
        [EnableRateLimiting("login")]
        public async Task<IActionResult> Login(LoginDto dto, CancellationToken cancellationToken)
        {
            var nome = dto.Usuario.Trim();
            var usuario = await _context.Usuarios.FirstOrDefaultAsync(u => u.NomeUsuario == nome, cancellationToken);

            // Sempre confere um hash (o falso, se o usuário não existe) para o tempo de resposta ser o mesmo.
            var resultado = usuario is null
                ? _hasher.VerifyHashedPassword(new Usuario(), HashFalso, dto.Senha)
                : _hasher.VerifyHashedPassword(usuario, usuario.SenhaHash, dto.Senha);

            if (usuario is null || resultado == PasswordVerificationResult.Failed)
            {
                return Unauthorized(new { mensagem = "Usuário ou senha inválidos." });
            }

            if (resultado == PasswordVerificationResult.SuccessRehashNeeded)
            {
                // O algoritmo de hash foi atualizado: regrava a senha no formato novo, de forma transparente.
                usuario.SenhaHash = _hasher.HashPassword(usuario, dto.Senha);
                await _context.SaveChangesAsync(cancellationToken);
            }

            var identity = new ClaimsIdentity(
                new[]
                {
                    new Claim(ClaimTypes.NameIdentifier, usuario.Id.ToString()),
                    new Claim(ClaimTypes.Name, usuario.NomeUsuario)
                },
                CookieAuthenticationDefaults.AuthenticationScheme);

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(identity),
                new AuthenticationProperties { IsPersistent = true });

            return Ok(new { usuario = usuario.NomeUsuario });
        }

        // POST: api/auth/logout
        [HttpPost("logout")]
        [AllowAnonymous]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return NoContent();
        }

        // GET: api/auth/me  (401 se não estiver logado; usado pelo front-end ao abrir o app)
        [HttpGet("me")]
        public IActionResult Me()
        {
            return Ok(new { usuario = User.Identity?.Name });
        }

        private bool ExigeCodigoConvite() => !string.IsNullOrWhiteSpace(_options.CodigoConvite);

        // Comparação em tempo constante (não revela, pelo tempo de resposta, quantos caracteres batem).
        private static bool SecureEquals(string informado, string esperado)
        {
            var hashInformado = SHA256.HashData(Encoding.UTF8.GetBytes(informado));
            var hashEsperado = SHA256.HashData(Encoding.UTF8.GetBytes(esperado));
            return CryptographicOperations.FixedTimeEquals(hashInformado, hashEsperado);
        }
    }
}
