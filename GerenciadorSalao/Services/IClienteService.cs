using GerenciadorSalao.Models;

namespace GerenciadorSalao.Services;

public interface IClienteService
{
    Task<List<Cliente>> ObterTodosAsync(string? termoBusca = null);
    Task<Cliente?> ObterPorIdAsync(int id);
    Task<Cliente> AdicionarAsync(Cliente cliente);
    Task AtualizarAsync(Cliente cliente);
    Task ExcluirAsync(int id);
    Task<Cliente?> ObterHistoricoClienteAsync(int clienteId);
}
