using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Renata_Doces.Common;

namespace Renata_Doces.Models
{
    public class Cliente
    {
        [Key]
        public int Id { get; set; }

        [Required(ErrorMessage = "O nome é obrigatório.")]
        public string Nome { get; set; } = string.Empty;

        public string? Telefone { get; set; }

        public string? Email { get; set; }

        public string? Endereco { get; set; }

        /// <summary>Código gerado pelo sistema (somente leitura; não é uma coluna do banco).</summary>
        [NotMapped]
        public string Codigo => Codigos.Cliente(Id);
    }
}