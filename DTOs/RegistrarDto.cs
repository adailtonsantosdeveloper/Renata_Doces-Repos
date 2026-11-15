using System.ComponentModel.DataAnnotations;

namespace Renata_Doces.DTOs
{
    /// <summary>
    /// Dados do cadastro de uma nova conta. As regras abaixo também são conferidas no navegador,
    /// mas o servidor é quem manda: nada aqui depende do JavaScript.
    /// </summary>
    public class RegistrarDto
    {
        [Required(ErrorMessage = "Informe o usuário.")]
        [StringLength(60, MinimumLength = 3, ErrorMessage = "O usuário deve ter de 3 a 60 caracteres.")]
        [RegularExpression(@"^[A-Za-z0-9._@\-]+$",
            ErrorMessage = "O usuário deve usar apenas letras sem acento, números, ponto, hífen, sublinhado ou @.")]
        public string Usuario { get; set; } = string.Empty;

        // Máximo de 128: evita que alguém envie senhas gigantes só para gastar CPU no cálculo do hash.
        [Required(ErrorMessage = "Informe a senha.")]
        [StringLength(128, MinimumLength = 10, ErrorMessage = "A senha deve ter entre 10 e 128 caracteres.")]
        public string Senha { get; set; } = string.Empty;

        [Required(ErrorMessage = "Confirme a senha.")]
        [Compare(nameof(Senha), ErrorMessage = "A confirmação da senha não confere.")]
        public string ConfirmarSenha { get; set; } = string.Empty;

        [StringLength(200, ErrorMessage = "Código de convite muito longo.")]
        public string? CodigoConvite { get; set; }
    }
}
