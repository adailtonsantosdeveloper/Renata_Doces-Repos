namespace Renata_Doces.Models
{
    /// <summary>
    /// Conta de acesso ao sistema. A senha NUNCA é guardada: só o hash (PBKDF2 com sal aleatório,
    /// gerado pelo PasswordHasher do ASP.NET Core). Tamanhos e índice único ficam em AppDbContext.
    /// </summary>
    public class Usuario
    {
        public int Id { get; set; }

        public string NomeUsuario { get; set; } = string.Empty;

        public string SenhaHash { get; set; } = string.Empty;

        public DateTime CriadoEm { get; set; }
    }
}
