using Microsoft.EntityFrameworkCore;
using Renata_Doces.Models;
/* using System.Collections.Generic; */

namespace Renata_Doces.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        // Definindo quais classes viram tabelas no SQL Server
        public DbSet<Produto> Produtos { get; set; }
        public DbSet<Cliente> Clientes { get; set; }
        public DbSet<Pedido> Pedidos { get; set; }
        public DbSet<PedidoItem> PedidoItens { get; set; }

        // === ADICIONE ESTE MÉTODO AQUI ===
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (!optionsBuilder.IsConfigured)
            {
                optionsBuilder.UseSqlServer("Server=(localdb)\\mssqllocaldb;Database=RenataDocesDb;Trusted_Connection=True;MultipleActiveResultSets=true");
            }
        }
        // ===================================

        // Configurando precisão para campos de dinheiro/preço
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Produto>()
                .Property(p => p.PrecoVenda)
                .HasColumnType("decimal(18,2)");

            modelBuilder.Entity<Pedido>()
                .Property(p => p.ValorTotal)
                .HasColumnType("decimal(18,2)");

            modelBuilder.Entity<PedidoItem>()
                .Property(pi => pi.PrecoUnitario)
                .HasColumnType("decimal(18,2)");
        }
    }
}