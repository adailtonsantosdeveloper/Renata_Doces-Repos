using Microsoft.EntityFrameworkCore;
using Renata_Doces.Data;

namespace Renata_Doces.Common
{
    /// <summary>
    /// Códigos de cliente, produto e pedido: gerados pelo sistema a partir do número sequencial (Id)
    /// do banco, então nunca se repetem e ninguém precisa digitá-los.
    /// </summary>
    public static class Codigos
    {
        public static string Cliente(int id) => $"CLI-{id:D4}";
        public static string Produto(int id) => $"PRD-{id:D4}";
        public static string Pedido(int id) => $"PED-{id:D4}";

        /// <summary>
        /// Previsão do próximo Id de uma tabela (só para mostrar na tela antes de salvar).
        /// O código definitivo é o devolvido pela gravação.
        /// </summary>
        public static async Task<int> ProximoIdAsync(AppDbContext contexto, string tabela, CancellationToken cancellationToken)
        {
            // Nome da tabela vem sempre de uma constante do código (lista fechada), nunca do usuário.
            if (tabela is not ("Clientes" or "Produtos" or "Pedidos"))
            {
                throw new ArgumentOutOfRangeException(nameof(tabela));
            }

            try
            {
                var sql = "SELECT CAST(ISNULL(CAST(last_value AS bigint), 0) + 1 AS bigint) AS [Value] " +
                          "FROM sys.identity_columns WHERE object_id = OBJECT_ID(N'dbo." + tabela + "')";
                var proximo = await contexto.Database.SqlQueryRaw<long>(sql).FirstAsync(cancellationToken);
                return (int)Math.Min(proximo, int.MaxValue);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Sem permissão para ler o catálogo do banco: usa o maior Id + 1.
                return tabela switch
                {
                    "Clientes" => (await contexto.Clientes.MaxAsync(c => (int?)c.Id, cancellationToken) ?? 0) + 1,
                    "Produtos" => (await contexto.Produtos.MaxAsync(p => (int?)p.Id, cancellationToken) ?? 0) + 1,
                    _ => (await contexto.Pedidos.MaxAsync(p => (int?)p.Id, cancellationToken) ?? 0) + 1
                };
            }
        }
    }
}
