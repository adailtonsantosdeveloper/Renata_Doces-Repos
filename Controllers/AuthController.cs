using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using Renata_Doces.DTOs;
using Renata_Doces.Security;

namespace Renata_Doces.Controllers
{
    [ApiController]
    [Route("api/auth")]
    public class AuthController : ControllerBase
    {
        private readonly AuthOptions _options;

        public AuthController(IOptions<AuthOptions> options)
        {
            _options = options.Value;
        }

        // POST: api/auth/login  (máx. 5 tentativas por minuto por IP)
        [HttpPost("login")]
        [AllowAnonymous]
        [EnableRateLimiting("login")]
        public async Task<IActionResult> Login(LoginDto dto)
        {
            // Compara usuário E senha sempre (sem "curto-circuito") e em tempo constante,
            // para não revelar por tempo de resposta qual dos dois estava errado.
            var usuarioConfere = SecureEquals(dto.Usuario, _options.Usuario);
            var senhaConfere = SecureEquals(dto.Senha, _options.Senha);

            if (!(usuarioConfere & senhaConfere))
            {
                return Unauthorized(new { mensagem = "Usuário ou senha inválidos." });
            }

            var identity = new ClaimsIdentity(
                new[] { new Claim(ClaimTypes.Name, _options.Usuario) },
                CookieAuthenticationDefaults.AuthenticationScheme);

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(identity),
                new AuthenticationProperties { IsPersistent = true });

            return Ok(new { usuario = _options.Usuario });
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

        private static bool SecureEquals(string informado, string esperado)
        {
            var hashInformado = SHA256.HashData(Encoding.UTF8.GetBytes(informado));
            var hashEsperado = SHA256.HashData(Encoding.UTF8.GetBytes(esperado));
            return CryptographicOperations.FixedTimeEquals(hashInformado, hashEsperado);
        }
    }
}
