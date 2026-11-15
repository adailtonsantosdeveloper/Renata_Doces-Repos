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
    public class ClientesController : ControllerBase
    {
        private readonly AppDbContext _context;

        public ClientesController(AppDbContext context)
        {
            _context = context;
        }

        // GET: api/clientes
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Cliente>>> GetClientes(CancellationToken cancellationToken)
        {
            return await _context.Clientes
                .AsNoTracking()
                .OrderBy(c => c.Nome)
                .ToListAsync(cancellationToken);
        }

        // GET: api/clientes/5
        [HttpGet("{id:int}")]
        public async Task<ActionResult<Cliente>> GetCliente(int id, CancellationToken cancellationToken)
        {
            var cliente = await _context.Clientes
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

            if (cliente == null)
            {
                return NotFound(new { mensagem = "Cliente não encontrado." });
            }

            return cliente;
        }

        // POST: api/clientes
        [HttpPost]
        public async Task<ActionResult<Cliente>> PostCliente(ClienteRequest dto, CancellationToken cancellationToken)
        {
            var cliente = new Cliente();
            Aplicar(cliente, dto);

            _context.Clientes.Add(cliente);
            await _context.SaveChangesAsync(cancellationToken);

            return CreatedAtAction(nameof(GetCliente), new { id = cliente.Id }, cliente);
        }

        // PUT: api/clientes/5
        [HttpPut("{id:int}")]
        public async Task<IActionResult> PutCliente(int id, ClienteRequest dto, CancellationToken cancellationToken)
        {
            var cliente = await _context.Clientes.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
            if (cliente == null)
            {
                return NotFound(new { mensagem = "Cliente não encontrado para atualização." });
            }

            Aplicar(cliente, dto);
            await _context.SaveChangesAsync(cancellationToken);

            return NoContent();
        }

        // DELETE: api/clientes/5
        [HttpDelete("{id:int}")]
        public async Task<IActionResult> DeleteCliente(int id, CancellationToken cancellationToken)
        {
            // O banco está configurado com exclusão em cascata: sem esta checagem,
            // apagar o cliente apagaria também todo o histórico de pedidos dele.
            if (await _context.Pedidos.AnyAsync(p => p.ClienteId == id, cancellationToken))
            {
                return Conflict(new { mensagem = "Este cliente possui pedidos e não pode ser excluído." });
            }

            var linhasAfetadas = await _context.Clientes
                .Where(c => c.Id == id)
                .ExecuteDeleteAsync(cancellationToken);

            if (linhasAfetadas == 0)
            {
                return NotFound(new { mensagem = "Cliente não encontrado para exclusão." });
            }

            return NoContent();
        }

        private static void Aplicar(Cliente cliente, ClienteRequest dto)
        {
            cliente.Nome = Texto.Limpo(dto.Nome);
            cliente.Telefone = Texto.NuloSeVazio(dto.Telefone);
            cliente.Email = Texto.NuloSeVazio(dto.Email);
            cliente.Endereco = Texto.NuloSeVazio(dto.Endereco);
        }
    }
}
