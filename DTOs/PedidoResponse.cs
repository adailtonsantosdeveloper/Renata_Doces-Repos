namespace Renata_Doces.DTOs
{
    // Respostas "achatadas": evitam o ciclo Pedido -> Itens -> Pedido (que quebra a serialização JSON)
    // e entregam só o que a tela precisa.
    public record PedidoItemResponse(
        int ProdutoId,
        string ProdutoNome,
        int Quantidade,
        decimal PrecoUnitario,
        decimal Subtotal);

    public record PedidoResponse(
        int Id,
        int ClienteId,
        string ClienteNome,
        DateTime DataPedido,
        decimal ValorTotal,
        List<PedidoItemResponse> Itens)
    {
        /// <summary>Código gerado pelo sistema (somente leitura).</summary>
        public string Codigo => Renata_Doces.Common.Codigos.Pedido(Id);
    }
}
