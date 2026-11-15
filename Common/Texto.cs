namespace Renata_Doces.Common
{
    /// <summary>Pequenos auxiliares para normalizar textos vindos da API.</summary>
    public static class Texto
    {
        /// <summary>Remove espaços nas pontas e devolve null se o texto ficar vazio.</summary>
        public static string? NuloSeVazio(string? valor)
            => string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();

        /// <summary>Remove espaços nas pontas e devolve "" no lugar de null (colunas NOT NULL).</summary>
        public static string Limpo(string? valor)
            => valor?.Trim() ?? string.Empty;
    }
}
