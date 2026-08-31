using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Renata_Doces.Data;
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

        // GET: api/produtos (Lista todos para a busca na TextBoxList)
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Produto>>> GetProdutos()
        {
            return await _context.Produtos.ToListAsync();
        }

        // GET: api/produtos/5 (Busca um produto específico pelo ID)
        [HttpGet("{id}")]
        public async Task<ActionResult<Produto>> GetProduto(int id)
        {
            var produto = await _context.Produtos.FindAsync(id);

            if (produto == null)
            {
                return NotFound(new { mensagem = "Produto não encontrado." });
            }

            return produto;
        }

        // POST: api/produtos (Cadastra um novo produto)
        [HttpPost]
        public async Task<ActionResult<Produto>> PostProduto(Produto produto)
        {
            _context.Produtos.Add(produto);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetProduto), new { id = produto.Id }, produto);
        }

        // PUT: api/produtos/5 (Atualiza um produto existente)
        [HttpPut("{id}")]
        public async Task<IActionResult> PutProduto(int id, Produto produto)
        {
            if (id != produto.Id)
            {
                return BadRequest(new { mensagem = "O ID do produto não confere." });
            }

            _context.Entry(produto).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!_context.Produtos.Any(e => e.Id == id))
                {
                    return NotFound(new { mensagem = "Produto não encontrado para atualização." });
                }
                else
                {
                    throw;
                }
            }

            return NoContent();
        }

        // DELETE: api/produtos/5 (Remove o produto do banco)
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteProduto(int id)
        {
            var produto = await _context.Produtos.FindAsync(id);
            if (produto == null)
            {
                return NotFound(new { mensagem = "Produto não encontrado para exclusão." });
            }

            _context.Produtos.Remove(produto);
            await _context.SaveChangesAsync();

            return NoContent();
        }
    }
}