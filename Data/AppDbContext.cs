using Microsoft.EntityFrameworkCore;
using Renata_Doces.Models;

namespace Renata_Doces.Data
{
    public class AppDbContext : DbContext
    {
        // A connection string NÃO fica mais aqui: ela é registrada no Program.cs (AddDbContext)
        // a partir da configuração. Isso permite usar bancos diferentes por ambiente sem recompilar.
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        // Definindo quais classes viram tabelas no SQL Server
        public DbSet<Produto> Produtos { get; set; }
        public DbSet<Cliente> Clientes { get; set; }
        public DbSet<Pedido> Pedidos { get; set; }
        public DbSet<PedidoItem> PedidoItens { get; set; }
        public DbSet<Usuario> Usuarios { get; set; }

        // Configurando precisão para campos de dinheiro/preço
        // (mantido idêntico ao model snapshot das migrations existentes).
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

            // Cliente e produto com pedidos não podem ser apagados (e nunca levam pedidos junto em cascata):
            // o histórico de vendas fica protegido também no banco, não só nos controllers.
            modelBuilder.Entity<Pedido>()
                .HasOne(p => p.Cliente)
                .WithMany()
                .HasForeignKey(p => p.ClienteId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<PedidoItem>()
                .HasOne(i => i.Produto)
                .WithMany()
                .HasForeignKey(i => i.ProdutoId)
                .OnDelete(DeleteBehavior.Restrict);

            // Contas de acesso: nome único (o SQL Server compara sem diferenciar maiúsculas/minúsculas).
            modelBuilder.Entity<Usuario>(entidade =>
            {
                entidade.Property(u => u.NomeUsuario).IsRequired().HasMaxLength(60);
                entidade.Property(u => u.SenhaHash).IsRequired().HasMaxLength(500);
                entidade.HasIndex(u => u.NomeUsuario).IsUnique();
            });
        }
    }
}
