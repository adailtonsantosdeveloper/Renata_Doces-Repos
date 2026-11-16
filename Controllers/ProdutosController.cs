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

        // POST: api/produtos
        [HttpPost]
        public async Task<ActionResult<Produto>> PostProduto(ProdutoRequest dto, CancellationToken cancellationToken)
        {
            var codigo = Texto.Limpo(dto.Codigo);
            if (await _context.Produtos.AnyAsync(p => p.Codigo == codigo, cancellationToken))
            {
                return Conflict(new { mensagem = $"Já existe um produto com o código '{codigo}'." });
            }

            var produto = new Produto();
            Aplicar(produto, dto);

            _context.Produtos.Add(produto);
            await _context.SaveChangesAsync(cancellationToken);

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

            var codigo = Texto.Limpo(dto.Codigo);
            if (await _context.Produtos.AnyAsync(p => p.Codigo == codigo && p.Id != id, cancellationToken))
            {
                return Conflict(new { mensagem = $"Já existe outro produto com o código '{codigo}'." });
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
            produto.Codigo = Texto.Limpo(dto.Codigo);
            produto.Nome = Texto.Limpo(dto.Nome);
            produto.Sabor = Texto.Limpo(dto.Sabor);
            produto.Recheio = Texto.Limpo(dto.Recheio);
            produto.Peso = Texto.Limpo(dto.Peso);
            produto.PrecoVenda = Math.Round(dto.PrecoVenda, 2, MidpointRounding.AwayFromZero);
            produto.Ingredientes = Texto.Limpo(dto.Ingredientes);
        }
    }
}
