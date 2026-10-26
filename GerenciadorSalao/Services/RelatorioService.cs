using GerenciadorSalao.Data;
using GerenciadorSalao.Models;
using GerenciadorSalao.Models.DTOs;
using Microsoft.EntityFrameworkCore;

namespace GerenciadorSalao.Services;

public class RelatorioService : IRelatorioService
{
    private readonly AppDbContext _context;

    public RelatorioService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<List<FaturamentoMensalDto>> ObterFaturamentoMensalPorAnoAsync(int ano)
    {
        var comandas = await _context.Comandas
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(c => c.Status == StatusComanda.Finalizada &&
                        (c.DataFechamento ?? c.DataAbertura).Year == ano)
            .Select(c => new
            {
                Data = c.DataFechamento ?? c.DataAbertura,
                c.ValorTotal
            })
            .ToListAsync();

        var resultado = new List<FaturamentoMensalDto>();

        for (int mes = 1; mes <= 12; mes++)
        {
            var totalMes = comandas
                .Where(c => c.Data.Month == mes)
                .Sum(c => c.ValorTotal);

            resultado.Add(new FaturamentoMensalDto
            {
                Ano = ano,
                Mes = mes,
                Total = totalMes
            });
        }

        return resultado;
    }

    public async Task<List<TopClienteDto>> ObterTopClientesAsync(int top = 10, int? ano = null)
    {
        var query = _context.Comandas
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Include(c => c.Cliente)
            .Where(c => c.Status == StatusComanda.Finalizada);

        if (ano.HasValue)
        {
            query = query.Where(c => (c.DataFechamento ?? c.DataAbertura).Year == ano.Value);
        }

        var agrupado = await query
            .GroupBy(c => new { c.ClienteId, c.Cliente.Nome, c.Cliente.Telefone })
            .Select(g => new TopClienteDto
            {
                ClienteId = g.Key.ClienteId,
                NomeCliente = g.Key.Nome,
                Telefone = g.Key.Telefone,
                TotalGasto = g.Sum(c => c.ValorTotal),
                QuantidadeComandas = g.Count()
            })
            .OrderByDescending(tc => tc.TotalGasto)
            .Take(top)
            .ToListAsync();

        return agrupado;
    }

    public async Task<decimal> ObterFaturamentoTotalPeriodoAsync(DateTime inicio, DateTime fim)
    {
        return await _context.Comandas
            .AsNoTracking()
            .Where(c => c.Status == StatusComanda.Finalizada &&
                        (c.DataFechamento ?? c.DataAbertura) >= inicio &&
                        (c.DataFechamento ?? c.DataAbertura) <= fim)
            .SumAsync(c => c.ValorTotal);
    }

    public async Task<int> ObterTotalAtendimentosPeriodoAsync(DateTime inicio, DateTime fim)
    {
        return await _context.Comandas
            .AsNoTracking()
            .CountAsync(c => c.Status == StatusComanda.Finalizada &&
                             (c.DataFechamento ?? c.DataAbertura) >= inicio &&
                             (c.DataFechamento ?? c.DataAbertura) <= fim);
    }

    public async Task<List<FaturamentoDetalhadoMesDto>> ObterFaturamentoDetalhadoMesesAsync(int ano)
    {
        var comandas = await _context.Comandas
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(c => c.Status == StatusComanda.Finalizada &&
                        (c.DataFechamento ?? c.DataAbertura).Year == ano)
            .Select(c => new
            {
                Data = c.DataFechamento ?? c.DataAbertura,
                c.ValorTotal
            })
            .ToListAsync();

        var resultado = new List<FaturamentoDetalhadoMesDto>();
        var culturaPtBr = new System.Globalization.CultureInfo("pt-BR");

        for (int mes = 1; mes <= 12; mes++)
        {
            var doMes = comandas.Where(c => c.Data.Month == mes).ToList();
            resultado.Add(new FaturamentoDetalhadoMesDto
            {
                Ano = ano,
                Mes = mes,
                NomeMes = culturaPtBr.DateTimeFormat.GetMonthName(mes),
                TotalFaturado = doMes.Sum(c => c.ValorTotal),
                TotalAtendimentos = doMes.Count
            });
        }

        return resultado;
    }

    public async Task<List<TopClienteDto>> ObterTopClientesFrequentesAsync(int top = 5)
    {
        var agrupado = await _context.Comandas
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Include(c => c.Cliente)
            .Where(c => c.Status == StatusComanda.Finalizada)
            .GroupBy(c => new { c.ClienteId, c.Cliente.Nome, c.Cliente.Telefone })
            .Select(g => new TopClienteDto
            {
                ClienteId = g.Key.ClienteId,
                NomeCliente = g.Key.Nome,
                Telefone = g.Key.Telefone,
                TotalGasto = g.Sum(c => c.ValorTotal),
                QuantidadeComandas = g.Count()
            })
            .OrderByDescending(tc => tc.QuantidadeComandas)
            .ThenByDescending(tc => tc.TotalGasto)
            .Take(top)
            .ToListAsync();

        return agrupado;
    }

    public async Task<List<TopServicoDto>> ObterServicosMaisPopularesAsync(int top = 5)
    {
        var itensServico = await _context.ComandaItens
            .AsNoTracking()
            .Where(i => i.Tipo == TipoItemComanda.Servico && i.ServicoId.HasValue)
            .Include(i => i.Servico)
            .GroupBy(i => new { i.ServicoId, i.Descricao, Duracao = i.Servico != null ? i.Servico.DuracaoMinutos : 0 })
            .Select(g => new
            {
                ServicoId = g.Key.ServicoId!.Value,
                Nome = g.Key.Descricao,
                Duracao = g.Key.Duracao,
                Qtd = g.Sum(i => i.Quantidade)
            })
            .OrderByDescending(x => x.Qtd)
            .Take(top)
            .ToListAsync();

        var total = itensServico.Sum(x => x.Qtd);
        if (total == 0) total = 1;

        return itensServico.Select(x => new TopServicoDto
        {
            ServicoId = x.ServicoId,
            NomeServico = x.Nome,
            QuantidadeRealizada = x.Qtd,
            DuracaoMediaMinutos = x.Duracao,
            Percentual = Math.Round((double)x.Qtd / total * 100, 1)
        }).ToList();
    }
}
