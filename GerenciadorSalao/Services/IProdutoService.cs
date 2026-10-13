using GerenciadorSalao.Models;

namespace GerenciadorSalao.Services;

public interface IProdutoService
{
    Task<List<Produto>> ObterTodosAsync(bool apenasAtivos = true);
    Task<Produto?> ObterPorIdAsync(int id);
    Task<Produto> AdicionarAsync(Produto produto);
    Task AtualizarAsync(Produto produto);
    Task DesativarAsync(int id);
    Task ExcluirAsync(int id);
}
