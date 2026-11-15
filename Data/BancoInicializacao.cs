using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Renata_Doces.Data
{
    public static class BancoInicializacao
    {
        /// <summary>
        /// Aplica as migrations pendentes. Em Docker o app costuma subir antes de o SQL Server terminar
        /// de iniciar; por isso, falhas de CONEXÃO são repetidas por até ~1 minuto. Outros erros
        /// (por exemplo, modelo diferente das migrations) falham na hora, com a mensagem original.
        /// </summary>
        public static async Task AplicarMigrationsAsync(IServiceProvider services, ILogger logger,
            CancellationToken cancellationToken = default)
        {
            const int maximoDeTentativas = 12;
            var espera = TimeSpan.FromSeconds(5);

            for (var tentativa = 1; ; tentativa++)
            {
                try
                {
                    using var scope = services.CreateScope();
                    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                    await db.Database.MigrateAsync(cancellationToken);
                    logger.LogInformation("Banco de dados atualizado (migrations aplicadas).");
                    return;
                }
                catch (Exception ex) when (tentativa < maximoDeTentativas
                                           && !cancellationToken.IsCancellationRequested
                                           && (ex is DbException || ex is TimeoutException || ex is RetryLimitExceededException))
                {
                    logger.LogWarning(
                        "Banco de dados indisponível (tentativa {Tentativa} de {Maximo}): {Motivo}. Nova tentativa em {Espera} s.",
                        tentativa, maximoDeTentativas, ex.GetBaseException().Message, espera.TotalSeconds);

                    await Task.Delay(espera, cancellationToken);
                }
            }
        }
    }
}
