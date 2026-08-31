namespace Renata_Doces.DTOs
{
    public class PedidoCreateDto
    {
        public int ClienteId { get; set; }
        public List<ItemPedidoDto> Itens { get; set; } = new();
    }

    public class ItemPedidoDto
    {
        public int ProdutoId { get; set; }
        public int Quantidade { get; set; }
    }
}
