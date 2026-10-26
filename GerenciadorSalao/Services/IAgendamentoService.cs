using GerenciadorSalao.Models;
using GerenciadorSalao.Models.DTOs;

namespace GerenciadorSalao.Services;

public class ItemServicoAgendamentoDto
{
    public int ServicoId { get; set; }
    public decimal Valor { get; set; }
    public int DuracaoMinutos { get; set; }
}

public interface IAgendamentoService
{
    Task<List<Agendamento>> ObterPorPeriodoAsync(DateTime inicio, DateTime fim);
    Task<List<Agendamento>> ObterAgendasPendentesDoDiaAsync(DateTime data);
    Task<Agendamento?> ObterPorIdAsync(int id);
    Task<Agendamento> CriarAgendamentoAsync(int clienteId, DateTime dataHora, List<int> servicoIds, string? observacao = null, List<ItemServicoAgendamentoDto>? itensCustomizados = null);
    Task<Agendamento> AtualizarAgendamentoAsync(int agendamentoId, int clienteId, DateTime dataHora, List<int> servicoIds, string? observacao = null, List<ItemServicoAgendamentoDto>? itensCustomizados = null);
    Task AtualizarStatusAsync(int agendamentoId, StatusAgendamento status);
    Task CancelarAgendamentoAsync(int agendamentoId);
    Task ExcluirAgendamentoAsync(int agendamentoId);
}
