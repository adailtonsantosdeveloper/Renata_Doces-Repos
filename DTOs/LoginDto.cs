using System.ComponentModel.DataAnnotations;

namespace Renata_Doces.DTOs
{
    public class LoginDto
    {
        [Required(ErrorMessage = "Informe o usuário.")]
        [StringLength(100, ErrorMessage = "Usuário muito longo.")]
        public string Usuario { get; set; } = string.Empty;

        [Required(ErrorMessage = "Informe a senha.")]
        [StringLength(200, ErrorMessage = "Senha muito longa.")]
        public string Senha { get; set; } = string.Empty;
    }
}
