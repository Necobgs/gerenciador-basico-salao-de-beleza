using GerenciadorSalao.Models.DTOs;

namespace GerenciadorSalao.Services;

public interface IRelatorioService
{
    Task<List<FaturamentoMensalDto>> ObterFaturamentoMensalPorAnoAsync(int ano);
    Task<List<TopClienteDto>> ObterTopClientesAsync(int top = 5, int? ano = null);
    Task<decimal> ObterFaturamentoTotalPeriodoAsync(DateTime inicio, DateTime fim);
    Task<int> ObterTotalAtendimentosPeriodoAsync(DateTime inicio, DateTime fim);
}
