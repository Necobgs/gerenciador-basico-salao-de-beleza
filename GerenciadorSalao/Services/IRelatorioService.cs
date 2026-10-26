using GerenciadorSalao.Models.DTOs;

namespace GerenciadorSalao.Services;

public interface IRelatorioService
{
    Task<List<FaturamentoMensalDto>> ObterFaturamentoMensalPorAnoAsync(int ano);
    Task<List<FaturamentoDetalhadoMesDto>> ObterFaturamentoDetalhadoMesesAsync(int ano);
    Task<List<TopClienteDto>> ObterTopClientesAsync(int top = 10, int? ano = null);
    Task<List<TopClienteDto>> ObterTopClientesFrequentesAsync(int top = 5);
    Task<List<TopServicoDto>> ObterServicosMaisPopularesAsync(int top = 5);
    Task<decimal> ObterFaturamentoTotalPeriodoAsync(DateTime inicio, DateTime fim);
    Task<int> ObterTotalAtendimentosPeriodoAsync(DateTime inicio, DateTime fim);
}
