using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Renata_Doces.Models
{
    public class Pedido
    {
        [Key]
        public int Id { get; set; }

        [Required(ErrorMessage = "O cliente é obrigatório.")]
        public int ClienteId { get; set; }

        [ForeignKey("ClienteId")]
        public Cliente? Cliente { get; set; }

        public DateTime DataPedido { get; set; } = DateTime.Now;

        [Column(TypeName = "decimal(18,2)")]
        public decimal ValorTotal { get; set; }

        // Lista de itens que compõem este pedido
        public List<PedidoItem> Itens { get; set; } = new();
    }
}
