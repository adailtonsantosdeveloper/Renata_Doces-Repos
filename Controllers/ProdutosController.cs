using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Renata_Doces.Common;
using Renata_Doces.Data;
using Renata_Doces.DTOs;
using Renata_Doces.Models;

namespace Renata_Doces.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ProdutosController : ControllerBase
    {
        private readonly AppDbContext _context;

        public ProdutosController(AppDbContext context)
        {
            _context = context;
        }

        // GET: api/produtos
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Produto>>> GetProdutos(CancellationToken cancellationToken)
        {
            return await _context.Produtos
                .AsNoTracking()
                .OrderBy(p => p.Nome)
                .ToListAsync(cancellationToken);
        }

        // GET: api/produtos/5
        [HttpGet("{id:int}")]
        public async Task<ActionResult<Produto>> GetProduto(int id, CancellationToken cancellationToken)
        {
            var produto = await _context.Produtos
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

            if (produto == null)
            {
                return NotFound(new { mensagem = "Produto não encontrado." });
            }

            return produto;
        }

        // GET: api/produtos/proximo-codigo  (previsão para exibir no formulário)
        [HttpGet("proximo-codigo")]
        public async Task<IActionResult> ProximoCodigo(CancellationToken cancellationToken)
        {
            var id = await Codigos.ProximoIdAsync(_context, "Produtos", cancellationToken);
            return Ok(new { codigo = Codigos.Produto(id) });
        }

        // POST: api/produtos
        [HttpPost]
        public async Task<ActionResult<Produto>> PostProduto(ProdutoRequest dto, CancellationToken cancellationToken)
        {
            // O código (PRD-0001...) é gerado a partir do Id, que só existe depois de gravar:
            // grava com um código provisório e acerta na mesma transação.
            Produto produto = null!;
            var estrategia = _context.Database.CreateExecutionStrategy();
            await estrategia.ExecuteAsync(async () =>
            {
                _context.ChangeTracker.Clear();
                produto = new Produto();
                Aplicar(produto, dto);
                produto.Codigo = "TMP-" + Guid.NewGuid().ToString("N")[..20];

                await using var transacao = await _context.Database.BeginTransactionAsync(cancellationToken);
                _context.Produtos.Add(produto);
                await _context.SaveChangesAsync(cancellationToken);
                produto.Codigo = Codigos.Produto(produto.Id);
                await _context.SaveChangesAsync(cancellationToken);
                await transacao.CommitAsync(cancellationToken);
            });

            return CreatedAtAction(nameof(GetProduto), new { id = produto.Id }, produto);
        }

        // PUT: api/produtos/5
        [HttpPut("{id:int}")]
        public async Task<IActionResult> PutProduto(int id, ProdutoRequest dto, CancellationToken cancellationToken)
        {
            var produto = await _context.Produtos.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
            if (produto == null)
            {
                return NotFound(new { mensagem = "Produto não encontrado para atualização." });
            }

            // Pedidos antigos não mudam: cada item guarda o preço da época da venda (PrecoUnitario).
            Aplicar(produto, dto);
            await _context.SaveChangesAsync(cancellationToken);

            return NoContent();
        }

        // DELETE: api/produtos/5
        [HttpDelete("{id:int}")]
        public async Task<IActionResult> DeleteProduto(int id, CancellationToken cancellationToken)
        {
            // Mensagem amigável; o banco também bloqueia a exclusão (FK Restrict), mesmo numa corrida.
            if (await _context.PedidoItens.AnyAsync(i => i.ProdutoId == id, cancellationToken))
            {
                return Conflict(new { mensagem = "Este produto já foi vendido e não pode ser excluído." });
            }

            int linhasAfetadas;
            try
            {
                linhasAfetadas = await _context.Produtos
                    .Where(p => p.Id == id)
                    .ExecuteDeleteAsync(cancellationToken);
            }
            catch (Exception ex) when (ex is DbUpdateException or Microsoft.Data.SqlClient.SqlException)
            {
                return Conflict(new { mensagem = "Este produto já foi vendido e não pode ser excluído." });
            }

            if (linhasAfetadas == 0)
            {
                return NotFound(new { mensagem = "Produto não encontrado para exclusão." });
            }

            return NoContent();
        }

        private static void Aplicar(Produto produto, ProdutoRequest dto)
        {
            produto.Nome = Texto.Limpo(dto.Nome);
            produto.Sabor = Texto.Limpo(dto.Sabor);
            produto.Recheio = Texto.Limpo(dto.Recheio);
            produto.Peso = Texto.Limpo(dto.Peso);
            produto.PrecoVenda = Math.Round(dto.PrecoVenda, 2, MidpointRounding.AwayFromZero);
            produto.Ingredientes = Texto.Limpo(dto.Ingredientes);
        }
    }
}
