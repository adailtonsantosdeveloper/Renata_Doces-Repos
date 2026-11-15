namespace Renata_Doces.Security
{
    /// <summary>
    /// Credenciais do(a) dono(a) do sistema. Vêm SEMPRE de configuração segura
    /// (variáveis de ambiente em produção, User Secrets em desenvolvimento) e nunca do repositório.
    /// </summary>
    public class AuthOptions
    {
        public const string SectionName = "Auth";

        public string Usuario { get; set; } = string.Empty;

        public string Senha { get; set; } = string.Empty;
    }
}
