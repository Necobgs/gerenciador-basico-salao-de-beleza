using GerenciadorSalao.Data;
using GerenciadorSalao.Helpers;
using GerenciadorSalao.Models;
using Microsoft.EntityFrameworkCore;

namespace GerenciadorSalao.Services;

public class ProdutoService : IProdutoService
{
    private readonly AppDbContext _context;

    public ProdutoService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<List<Produto>> ObterTodosAsync(string? termoBusca = null, bool apenasAtivos = true)
    {
        var query = _context.Produtos.AsNoTracking().AsQueryable();

        if (apenasAtivos)
            query = query.Where(p => p.Ativo);

        if (!string.IsNullOrWhiteSpace(termoBusca))
        {
            var termo = termoBusca.Trim().ToLower();
            query = query.Where(p => p.Descricao.ToLower().Contains(termo));
        }

        return await query.OrderBy(p => p.Descricao).ToListAsync();
    }

    public async Task<Produto?> ObterPorIdAsync(int id)
    {
        return await _context.Produtos
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == id);
    }

    public async Task<Produto> AdicionarAsync(Produto produto)
    {
        if (!ValidationHelper.ValidarDescricaoProduto(produto.Descricao, out var erroDesc))
            throw new ArgumentException(erroDesc, nameof(produto.Descricao));

        if (produto.Valor < 0)
            throw new ArgumentException("O valor do produto não pode ser negativo.", nameof(produto.Valor));

        produto.Descricao = produto.Descricao.Trim();
        _context.Produtos.Add(produto);
        await _context.SaveChangesAsync();
        return produto;
    }

    public async Task AtualizarAsync(Produto produto)
    {
        if (!ValidationHelper.ValidarDescricaoProduto(produto.Descricao, out var erroDesc))
            throw new ArgumentException(erroDesc, nameof(produto.Descricao));

        if (produto.Valor < 0)
            throw new ArgumentException("O valor do produto não pode ser negativo.", nameof(produto.Valor));

        var existente = await _context.Produtos.FindAsync(produto.Id);
        if (existente == null)
            throw new InvalidOperationException("Produto não encontrado para atualização.");

        existente.Descricao = produto.Descricao.Trim();
        existente.Valor = produto.Valor;
        existente.Estoque = produto.Estoque;
        existente.Ativo = produto.Ativo;

        await _context.SaveChangesAsync();
    }

    public async Task DesativarAsync(int id)
    {
        var produto = await _context.Produtos.FindAsync(id);
        if (produto != null)
        {
            produto.Ativo = false;
            await _context.SaveChangesAsync();
        }
    }

    public async Task ExcluirAsync(int id)
    {
        var produto = await _context.Produtos.FindAsync(id);
        if (produto == null)
            return;

        // Soft Delete: marca a data de exclusão e inativa
        produto.DeletedAt = DateTime.Now;
        produto.Ativo = false;
        await _context.SaveChangesAsync();
    }
}
