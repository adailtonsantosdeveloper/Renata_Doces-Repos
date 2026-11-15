using System.ComponentModel.DataAnnotations;

namespace Renata_Doces.DTOs
{
    /// <summary>
    /// Dados aceitos ao cadastrar/editar um cliente. Usar um DTO (e não a entidade) impede
    /// "overposting": o chamador não consegue definir campos que não deveria (ex.: Id).
    /// As validações ficam aqui, e não na entidade, para não alterar o esquema do banco.
    /// </summary>
    public class ClienteRequest
    {
        [Required(ErrorMessage = "O nome é obrigatório.")]
        [StringLength(120, MinimumLength = 2, ErrorMessage = "O nome deve ter entre 2 e 120 caracteres.")]
        public string Nome { get; set; } = string.Empty;

        [StringLength(30, ErrorMessage = "O telefone deve ter no máximo 30 caracteres.")]
        [RegularExpression(@"^[0-9+()\-\s]*$", ErrorMessage = "O telefone deve conter apenas números, espaços, + ( ) e -.")]
        public string? Telefone { get; set; }

        [StringLength(160, ErrorMessage = "O e-mail deve ter no máximo 160 caracteres.")]
        // Aceita vazio (o controller converte "" em null); o [EmailAddress] rejeitaria a string vazia.
        [RegularExpression(@"^$|^[^@\s]+@[^@\s]+\.[^@\s]+$", ErrorMessage = "E-mail inválido.")]
        public string? Email { get; set; }

        [StringLength(250, ErrorMessage = "O endereço deve ter no máximo 250 caracteres.")]
        public string? Endereco { get; set; }
    }
}
