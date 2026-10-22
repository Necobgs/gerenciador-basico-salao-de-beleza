using GerenciadorSalao.Models;

namespace GerenciadorSalao.Services;

public interface IAgendamentoService
{
    Task<List<Agendamento>> ObterPorPeriodoAsync(DateTime inicio, DateTime fim);
    Task<List<Agendamento>> ObterAgendasPendentesDoDiaAsync(DateTime data);
    Task<Agendamento?> ObterPorIdAsync(int id);
    Task<Agendamento> CriarAgendamentoAsync(int clienteId, DateTime dataHora, List<int> servicoIds, string? observacao = null);
    Task<Agendamento> AtualizarAgendamentoAsync(int agendamentoId, int clienteId, DateTime dataHora, List<int> servicoIds, string? observacao = null);
    Task AtualizarStatusAsync(int agendamentoId, StatusAgendamento status);
    Task CancelarAgendamentoAsync(int agendamentoId);
    Task ExcluirAgendamentoAsync(int agendamentoId);
}
