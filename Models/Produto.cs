namespace Renata_Doces.Models
{
    public class Produto
    {
        public int Id { get; set; }
        public string Codigo { get; set; } = string.Empty;
        public string Nome { get; set; } = string.Empty;
        public string Sabor { get; set; } = string.Empty;
        public string Recheio { get; set; } = string.Empty;
        public string Peso { get; set; } = string.Empty;
        public decimal PrecoVenda { get; set; }
        public string Ingredientes { get; set; } = string.Empty;
    }
}
