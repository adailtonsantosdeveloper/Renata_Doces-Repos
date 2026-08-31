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

        // GET: api/pedidos
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Pedido>>> GetPedidos()
        {
            return await _context.Pedidos
                .Include(p => p.Cliente)
                .Include(p => p.Itens)
                .ThenInclude(i => i.Produto)
                .ToListAsync();
        }

        // GET: api/pedidos/5
        [HttpGet("{id}")]
        public async Task<ActionResult<Pedido>> GetPedido(int id)
        {
            var pedido = await _context.Pedidos
                .Include(p => p.Cliente)
                .Include(p => p.Itens)
                .ThenInclude(i => i.Produto)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (pedido == null)
            {
                return NotFound(new { mensagem = "Pedido não encontrado." });
            }

            return pedido;
        }

        // POST: api/pedidos
        [HttpPost]
        public async Task<ActionResult<Pedido>> PostPedido(PedidoCreateDto dto)
        {
            // Valida se o cliente existe
            var cliente = await _context.Clientes.FindAsync(dto.ClienteId);
            if (cliente == null)
            {
                return BadRequest(new { mensagem = "Cliente informado não existe." });
            }

            var pedido = new Pedido
            {
                ClienteId = dto.ClienteId,
                DataPedido = DateTime.Now,
                Itens = new List<PedidoItem>()
            };

            decimal valorTotalCalculado = 0;

            foreach (var itemDto in dto.Itens)
            {
                var produto = await _context.Produtos.FindAsync(itemDto.ProdutoId);
                if (produto == null)
                {
                    return BadRequest(new { mensagem = $"Produto com ID {itemDto.ProdutoId} não foi encontrado." });
                }

                var itemPedido = new PedidoItem
                {
                    ProdutoId = produto.Id,
                    Quantidade = itemDto.Quantidade,
                    PrecoUnitario = (decimal)produto.PrecoVenda
                };

                valorTotalCalculado += itemPedido.PrecoUnitario * itemPedido.Quantidade;
                pedido.Itens.Add(itemPedido);
            }

            pedido.ValorTotal = valorTotalCalculado;

            _context.Pedidos.Add(pedido);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetPedido), new { id = pedido.Id }, pedido);
        }
    }
}