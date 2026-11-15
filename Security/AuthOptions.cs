namespace Renata_Doces.Security
{
    /// <summary>
    /// Opções de acesso. Tudo vem de configuração segura (variáveis de ambiente em produção,
    /// User Secrets em desenvolvimento) e nunca do repositório. As contas ficam no banco de dados;
    /// aqui só há ajustes de como elas podem ser criadas.
    /// </summary>
    public class AuthOptions
    {
        public const string SectionName = "Auth";

        /// <summary>Opcional: conta inicial criada na primeira inicialização (junto com <see cref="Senha"/>).</summary>
        public string Usuario { get; set; } = string.Empty;

        /// <summary>Opcional: senha da conta inicial (mínimo de 10 caracteres). Só é lida na criação da conta.</summary>
        public string Senha { get; set; } = string.Empty;

        /// <summary>
        /// Opcional, mas RECOMENDADO em produção: quando definido, o cadastro de novas contas exige este código.
        /// Quando vazio, só a PRIMEIRA conta pode ser criada pela tela de cadastro; depois disso o cadastro fecha.
        /// </summary>
        public string CodigoConvite { get; set; } = string.Empty;

        /// <summary>
        /// true = o cookie de sessão só é enviado por https (use em produção com https de ponta a ponta).
        /// Padrão (false) = "Secure" quando a requisição chegou por https, o que também funciona atrás de proxy
        /// e permite testar em http://localhost no Docker.
        /// </summary>
        public bool CookieSempreSeguro { get; set; }
    }
}
