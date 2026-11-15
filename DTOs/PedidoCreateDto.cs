using System.ComponentModel.DataAnnotations;

namespace Renata_Doces.DTOs
{
    public class PedidoCreateDto
    {
        [Range(1, int.MaxValue, ErrorMessage = "Informe o cliente.")]
        public int ClienteId { get; set; }

        [Required(ErrorMessage = "Informe os itens do pedido.")]
        [MinLength(1, ErrorMessage = "O pedido deve ter pelo menos um item.")]
        [MaxLength(100, ErrorMessage = "O pedido pode ter no máximo 100 itens.")]
        public List<ItemPedidoDto> Itens { get; set; } = new();
    }

    public class ItemPedidoDto
    {
        [Range(1, int.MaxValue, ErrorMessage = "Produto inválido.")]
        public int ProdutoId { get; set; }

        [Range(1, 10000, ErrorMessage = "A quantidade deve estar entre 1 e 10.000.")]
        public int Quantidade { get; set; }
    }
}
