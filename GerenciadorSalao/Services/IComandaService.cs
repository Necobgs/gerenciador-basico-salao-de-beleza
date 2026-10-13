using GerenciadorSalao.Models;

namespace GerenciadorSalao.Services;

public interface IComandaService
{
    Task<List<Comanda>> ObterTodasAsync(StatusComanda? status = null);
    Task<List<Comanda>> ObterAbertasAsync();
    Task<Comanda?> ObterPorIdAsync(int id);
    Task<Comanda> CriarComandaAvulsaAsync(int clienteId, string? observacao = null);
    Task AdicionarItemServicoAsync(int comandaId, int servicoId, int quantidade = 1, decimal? precoUnitarioCustomizado = null);
    Task AdicionarItemProdutoAsync(int comandaId, int produtoId, int quantidade = 1, decimal? precoUnitarioCustomizado = null);
    Task EditarPrecoItemAsync(int itemId, decimal novoValorUnitario);
    Task EditarQuantidadeItemAsync(int itemId, int novaQuantidade);
    Task RemoverItemAsync(int itemId);
    Task AtualizarObservacaoAsync(int comandaId, string? observacao);
    Task FinalizarComandaAsync(int comandaId);
    Task ExcluirComandaAsync(int comandaId);
}
