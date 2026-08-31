using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Renata_Doces.Models
{
    public class PedidoItem
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int PedidoId { get; set; }

        // Propriedade de navegação (opcional para o EF Core)
        [ForeignKey("PedidoId")]
        public Pedido? Pedido { get; set; }

        [Required]
        public int ProdutoId { get; set; }

        [ForeignKey("ProdutoId")]
        public Produto? Produto { get; set; }

        [Required]
        public int Quantidade { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal PrecoUnitario { get; set; }
    }
}