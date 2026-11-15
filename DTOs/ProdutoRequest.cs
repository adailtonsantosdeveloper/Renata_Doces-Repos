using System.ComponentModel.DataAnnotations;

namespace Renata_Doces.DTOs
{
    /// <summary>Dados aceitos ao cadastrar/editar um produto (veja o comentário em ClienteRequest).</summary>
    public class ProdutoRequest
    {
        [Required(ErrorMessage = "O código é obrigatório.")]
        [StringLength(30, ErrorMessage = "O código deve ter no máximo 30 caracteres.")]
        public string Codigo { get; set; } = string.Empty;

        [Required(ErrorMessage = "O nome é obrigatório.")]
        [StringLength(120, MinimumLength = 2, ErrorMessage = "O nome deve ter entre 2 e 120 caracteres.")]
        public string Nome { get; set; } = string.Empty;

        [StringLength(80, ErrorMessage = "O sabor deve ter no máximo 80 caracteres.")]
        public string? Sabor { get; set; }

        [StringLength(80, ErrorMessage = "O recheio deve ter no máximo 80 caracteres.")]
        public string? Recheio { get; set; }

        [StringLength(30, ErrorMessage = "O peso deve ter no máximo 30 caracteres.")]
        public string? Peso { get; set; }

        [Range(0.01, 999999.99, ErrorMessage = "O preço deve estar entre 0,01 e 999.999,99.")]
        public decimal PrecoVenda { get; set; }

        [StringLength(1000, ErrorMessage = "Os ingredientes devem ter no máximo 1000 caracteres.")]
        public string? Ingredientes { get; set; }
    }
}
