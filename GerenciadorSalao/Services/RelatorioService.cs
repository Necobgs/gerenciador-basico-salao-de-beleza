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

    public async Task<List<TopClienteDto>> ObterTopClientesAsync(int top = 5, int? ano = null)
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
}
