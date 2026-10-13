using GerenciadorSalao.Models;

namespace GerenciadorSalao.Services;

public interface IServicoService
{
    Task<List<Servico>> ObterTodosAsync(bool apenasAtivos = true);
    Task<Servico?> ObterPorIdAsync(int id);
    Task<Servico> AdicionarAsync(Servico servico);
    Task AtualizarAsync(Servico servico);
    Task DesativarAsync(int id);
    Task ExcluirAsync(int id);
}
