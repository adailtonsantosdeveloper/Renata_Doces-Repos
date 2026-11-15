using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Renata_Doces.Data;
using Renata_Doces.Models;

namespace Renata_Doces.Security
{
    /// <summary>
    /// Cria a conta definida em Auth:Usuario / Auth:Senha (se houver) na primeira inicialização.
    /// É opcional: sem essas variáveis, a primeira conta é criada pela tela de cadastro.
    /// Se a conta já existir, nada é alterado (mudar a senha na variável NÃO muda a senha gravada).
    /// </summary>
    public static class UsuarioInicial
    {
        public static async Task GarantirAsync(IServiceProvider services, ILogger logger,
            CancellationToken cancellationToken = default)
        {
            using var scope = services.CreateScope();
            var opcoes = scope.ServiceProvider.GetRequiredService<IOptions<AuthOptions>>().Value;

            var nome = opcoes.Usuario.Trim();
            if (nome.Length == 0)
            {
                return;   // nenhuma conta inicial configurada
            }

            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            if (await db.Usuarios.AnyAsync(u => u.NomeUsuario == nome, cancellationToken))
            {
                return;
            }

            var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher<Usuario>>();
            var usuario = new Usuario { NomeUsuario = nome, CriadoEm = DateTime.UtcNow };
            usuario.SenhaHash = hasher.HashPassword(usuario, opcoes.Senha);
            db.Usuarios.Add(usuario);

            try
            {
                await db.SaveChangesAsync(cancellationToken);
                logger.LogInformation("Conta inicial '{Usuario}' criada a partir da configuração.", nome);
            }
            catch (DbUpdateException)
            {
                // Duas instâncias subindo juntas: a outra criou primeiro (índice único). Está tudo certo.
                if (!await db.Usuarios.AsNoTracking().AnyAsync(u => u.NomeUsuario == nome, cancellationToken))
                {
                    throw;
                }
            }
        }
    }
}
