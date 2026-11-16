using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Renata_Doces.Data;
using Renata_Doces.DTOs;
using Renata_Doces.Models;

namespace Renata_Doces.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class PedidosController : ControllerBase
    {
        private readonly AppDbContext _context;

        public PedidosController(AppDbContext context)
        {
            _context = context;
        }

        // GET: api/pedidos?limite=100  (mais recentes primeiro; máximo 500)
        [HttpGet]
        public async Task<ActionResult<IEnumerable<PedidoResponse>>> GetPedidos(
            [FromQuery] int limite = 100,
            CancellationToken cancellationToken = default)
        {
            limite = Math.Clamp(limite, 1, 500);

            // Ordena e limita na ENTIDADE (que o EF sabe traduzir) e só depois projeta para a resposta.
            var maisRecentes = _context.Pedidos
                .AsNoTracking()
                .OrderByDescending(p => p.DataPedido)
                .ThenByDescending(p => p.Id)
                .Take(limite);

            var pedidos = await Projetar(maisRecentes).ToListAsync(cancellationToken);

            return pedidos.Select(ComoUtc).ToList();
        }

        // GET: api/pedidos/5
        [HttpGet("{id:int}")]
        public async Task<ActionResult<PedidoResponse>> GetPedido(int id, CancellationToken cancellationToken)
        {
            var pedido = await Projetar(_context.Pedidos.AsNoTracking().Where(p => p.Id == id))
                .FirstOrDefaultAsync(cancellationToken);

            if (pedido == null)
            {
                return NotFound(new { mensagem = "Pedido não encontrado." });
            }

            return ComoUtc(pedido);
        }

        // GET: api/pedidos/proximo-codigo  (previsão para exibir no formulário)
        [HttpGet("proximo-codigo")]
        public async Task<IActionResult> ProximoCodigo(CancellationToken cancellationToken)
        {
            var id = await Common.Codigos.ProximoIdAsync(_context, "Pedidos", cancellationToken);
            return Ok(new { codigo = Common.Codigos.Pedido(id) });
        }

        // POST: api/pedidos
        [HttpPost]
        public async Task<ActionResult<PedidoResponse>> PostPedido(PedidoCreateDto dto, CancellationToken cancellationToken)
        {
            if (dto.Itens.Count == 0)
            {
                return BadRequest(new { mensagem = "O pedido deve ter pelo menos um item." });
            }

            var clienteExiste = await _context.Clientes.AnyAsync(c => c.Id == dto.ClienteId, cancellationToken);
            if (!clienteExiste)
            {
                return BadRequest(new { mensagem = "Cliente informado não existe." });
            }

            // Se o mesmo produto aparecer mais de uma vez, soma as quantidades em um único item.
            var itensAgrupados = dto.Itens
                .GroupBy(i => i.ProdutoId)
                .Select(g => new { ProdutoId = g.Key, Quantidade = g.Sum(i => i.Quantidade) })
                .ToList();

            if (itensAgrupados.Any(i => i.Quantidade > 10000))
            {
                return BadRequest(new { mensagem = "A quantidade total de um produto não pode passar de 10.000." });
            }

            // Uma única consulta para todos os produtos (evita 1 consulta por item).
            var idsProdutos = itensAgrupados.Select(i => i.ProdutoId).ToList();
            var produtos = await _context.Produtos
                .AsNoTracking()
                .Where(p => idsProdutos.Contains(p.Id))
                .ToDictionaryAsync(p => p.Id, cancellationToken);

            var naoEncontrados = idsProdutos.Where(id => !produtos.ContainsKey(id)).ToList();
            if (naoEncontrados.Count > 0)
            {
                return BadRequest(new { mensagem = $"Produto(s) não encontrado(s): {string.Join(", ", naoEncontrados)}." });
            }

            var pedido = new Pedido
            {
                ClienteId = dto.ClienteId,
                DataPedido = DateTime.UtcNow
            };

            foreach (var item in itensAgrupados)
            {
                var produto = produtos[item.ProdutoId];

                // O preço é SEMPRE o do cadastro (o cliente da API não manda valores),
                // e fica "congelado" no item: mudar o preço depois não altera pedidos antigos.
                pedido.Itens.Add(new PedidoItem
                {
                    ProdutoId = produto.Id,
                    Quantidade = item.Quantidade,
                    PrecoUnitario = produto.PrecoVenda
                });
            }

            pedido.ValorTotal = pedido.Itens.Sum(i => i.PrecoUnitario * i.Quantidade);

            _context.Pedidos.Add(pedido);
            try
            {
                await _context.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException)
            {
                // Cliente ou produto foi excluído por outra pessoa entre a conferência e a gravação (FK).
                return Conflict(new { mensagem = "Cliente ou produto foi alterado por outra pessoa. Atualize a tela e tente de novo." });
            }

            var resposta = await Projetar(_context.Pedidos.AsNoTracking().Where(p => p.Id == pedido.Id))
                .SingleAsync(cancellationToken);

            return CreatedAtAction(nameof(GetPedido), new { id = pedido.Id }, ComoUtc(resposta));
        }

        // DELETE: api/pedidos/5  (cancela um pedido lançado por engano; os itens saem junto, em cascata)
        [HttpDelete("{id:int}")]
        public async Task<IActionResult> DeletePedido(int id, CancellationToken cancellationToken)
        {
            var linhasAfetadas = await _context.Pedidos
                .Where(p => p.Id == id)
                .ExecuteDeleteAsync(cancellationToken);

            if (linhasAfetadas == 0)
            {
                return NotFound(new { mensagem = "Pedido não encontrado para exclusão." });
            }

            return NoContent();
        }

        // Projeção direto para o formato de resposta: uma consulta só, sem carregar entidades
        // inteiras e sem ciclos de navegação.
        private static IQueryable<PedidoResponse> Projetar(IQueryable<Pedido> consulta)
        {
            return consulta.Select(p => new PedidoResponse(
                p.Id,
                p.ClienteId,
                p.Cliente!.Nome,
                p.DataPedido,
                p.ValorTotal,
                p.Itens.Select(i => new PedidoItemResponse(
                    i.ProdutoId,
                    i.Produto!.Nome,
                    i.Quantidade,
                    i.PrecoUnitario,
                    i.PrecoUnitario * i.Quantidade)).ToList()));
        }

        // As datas são gravadas em UTC; o SQL Server devolve "sem fuso". Marcar como UTC faz o JSON
        // sair com "Z" no final, e o navegador do iPhone converte certo para o horário local.
        private static PedidoResponse ComoUtc(PedidoResponse pedido)
        {
            return pedido with { DataPedido = DateTime.SpecifyKind(pedido.DataPedido, DateTimeKind.Utc) };
        }
    }
}
