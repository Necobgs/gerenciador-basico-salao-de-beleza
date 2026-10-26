using System.Diagnostics;
using GerenciadorSalao.Data;
using GerenciadorSalao.Models;
using GerenciadorSalao.Models.DTOs;
using Microsoft.EntityFrameworkCore;

namespace GerenciadorSalao.Services;

public class AgendamentoService : IAgendamentoService
{
    private readonly AppDbContext _context;
    private readonly IICloudSyncService? _iCloudSyncService;

    public AgendamentoService(AppDbContext context, IICloudSyncService? iCloudSyncService = null)
    {
        _context = context;
        _iCloudSyncService = iCloudSyncService;
    }

    public async Task<List<Agendamento>> ObterPorPeriodoAsync(DateTime inicio, DateTime fim)
    {
        return await _context.Agendamentos
            .AsNoTracking()
            .Include(a => a.Cliente)
            .Include(a => a.Comanda)
            .Include(a => a.AgendamentoServicos)
                .ThenInclude(asv => asv.Servico)
            .Where(a => a.DataHora >= inicio && a.DataHora <= fim && a.Status != StatusAgendamento.Cancelado)
            .OrderBy(a => a.DataHora)
            .ToListAsync();
    }

    public async Task<List<Agendamento>> ObterAgendasPendentesDoDiaAsync(DateTime data)
    {
        var inicioDia = data.Date;
        var fimDia = data.Date.AddDays(1).AddTicks(-1);

        return await _context.Agendamentos
            .AsNoTracking()
            .Include(a => a.Cliente)
            .Include(a => a.Comanda)
                .ThenInclude(c => c!.Itens)
            .Include(a => a.AgendamentoServicos)
                .ThenInclude(asv => asv.Servico)
            .Where(a => a.DataHora >= inicioDia && 
                        a.DataHora <= fimDia && 
                        a.Status == StatusAgendamento.Pendente)
            .OrderBy(a => a.DataHora)
            .ToListAsync();
    }

    public async Task<Agendamento?> ObterPorIdAsync(int id)
    {
        return await _context.Agendamentos
            .AsNoTracking()
            .Include(a => a.Cliente)
            .Include(a => a.Comanda)
                .ThenInclude(c => c!.Itens)
            .Include(a => a.AgendamentoServicos)
                .ThenInclude(asv => asv.Servico)
            .FirstOrDefaultAsync(a => a.Id == id);
    }

    public async Task<Agendamento> CriarAgendamentoAsync(int clienteId, DateTime dataHora, List<int> servicoIds, string? observacao = null, List<ItemServicoAgendamentoDto>? itensCustomizados = null)
    {
        var cliente = await _context.Clientes.FindAsync(clienteId);
        if (cliente == null)
            throw new InvalidOperationException("Cliente não encontrado.");

        if (servicoIds == null || servicoIds.Count == 0)
            throw new ArgumentException("É necessário selecionar pelo menos um serviço para o agendamento.", nameof(servicoIds));

        var servicos = await _context.Servicos
            .Where(s => servicoIds.Contains(s.Id))
            .ToListAsync();

        if (servicos.Count == 0)
            throw new ArgumentException("Nenhum serviço válido foi localizado com os IDs informados.");

        var tempoTotalMinutos = itensCustomizados != null && itensCustomizados.Count > 0
            ? itensCustomizados.Sum(i => i.DuracaoMinutos)
            : servicos.Sum(s => s.DuracaoMinutos);

        var agendamento = new Agendamento
        {
            ClienteId = clienteId,
            Cliente = cliente,
            DataHora = dataHora,
            TempoTotalPrevistoMinutos = tempoTotalMinutos,
            Status = StatusAgendamento.Pendente,
            Observacao = observacao?.Trim()
        };

        foreach (var servico in servicos)
        {
            agendamento.AgendamentoServicos.Add(new AgendamentoServico
            {
                Agendamento = agendamento,
                ServicoId = servico.Id,
                Servico = servico
            });
        }

        // Regra de negócio: Cria automaticamente uma comanda vinculada ao agendamento
        var comanda = new Comanda
        {
            ClienteId = clienteId,
            DataAbertura = dataHora,
            Status = StatusComanda.Aberta,
            Observacao = observacao?.Trim(),
            Agendamento = agendamento
        };

        foreach (var servico in servicos)
        {
            var custom = itensCustomizados?.FirstOrDefault(i => i.ServicoId == servico.Id);
            var valorUnitario = custom != null ? custom.Valor : servico.Valor;

            comanda.Itens.Add(new ComandaItem
            {
                Comanda = comanda,
                Tipo = TipoItemComanda.Servico,
                ServicoId = servico.Id,
                Descricao = servico.NomeExibicao,
                Quantidade = 1,
                ValorUnitario = valorUnitario
            });
        }

        comanda.ValorTotal = comanda.CalcularTotalItens();
        agendamento.Comanda = comanda;

        _context.Agendamentos.Add(agendamento);
        _context.Comandas.Add(comanda);

        await _context.SaveChangesAsync();

        // Integração offline-first com o Calendário do iPhone (iCloud)
        if (_iCloudSyncService != null)
        {
            try
            {
                await _iCloudSyncService.EnfileirarOperacaoAsync(agendamento.Id, agendamento.ICloudEventId, TipoOperacaoSincronizacao.Criar, agendamento);
                _iCloudSyncService.DispararSincronizacaoEmSegundoPlano();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AgendamentoService] Falha ao enfileirar para iPhone / iCloud: {ex.Message}");
            }
        }

        return agendamento;
    }

    public async Task<Agendamento> AtualizarAgendamentoAsync(int agendamentoId, int clienteId, DateTime dataHora, List<int> servicoIds, string? observacao = null, List<ItemServicoAgendamentoDto>? itensCustomizados = null)
    {
        var agendamento = await _context.Agendamentos
            .Include(a => a.Cliente)
            .Include(a => a.AgendamentoServicos)
                .ThenInclude(asv => asv.Servico)
            .Include(a => a.Comanda)
                .ThenInclude(c => c!.Itens)
            .FirstOrDefaultAsync(a => a.Id == agendamentoId);

        if (agendamento == null)
            throw new InvalidOperationException("Agendamento não encontrado.");

        var cliente = await _context.Clientes.FindAsync(clienteId);
        if (cliente == null)
            throw new InvalidOperationException("Cliente não encontrado.");

        if (servicoIds == null || servicoIds.Count == 0)
            throw new ArgumentException("É necessário selecionar pelo menos um serviço para o agendamento.", nameof(servicoIds));

        var servicos = await _context.Servicos
            .Where(s => servicoIds.Contains(s.Id))
            .ToListAsync();

        if (servicos.Count == 0)
            throw new ArgumentException("Nenhum serviço válido foi localizado com os IDs informados.");

        var tempoTotalMinutos = itensCustomizados != null && itensCustomizados.Count > 0
            ? itensCustomizados.Sum(i => i.DuracaoMinutos)
            : servicos.Sum(s => s.DuracaoMinutos);

        agendamento.ClienteId = clienteId;
        agendamento.Cliente = cliente;
        agendamento.DataHora = dataHora;
        agendamento.TempoTotalPrevistoMinutos = tempoTotalMinutos;
        agendamento.Observacao = observacao?.Trim();

        // Atualiza os serviços associados ao agendamento
        agendamento.AgendamentoServicos.Clear();
        foreach (var servico in servicos)
        {
            agendamento.AgendamentoServicos.Add(new AgendamentoServico
            {
                AgendamentoId = agendamento.Id,
                Agendamento = agendamento,
                ServicoId = servico.Id,
                Servico = servico
            });
        }

        // Se houver comanda vinculada e ela ainda estiver aberta, sincroniza os serviços e horário
        if (agendamento.Comanda != null && agendamento.Comanda.Status == StatusComanda.Aberta)
        {
            agendamento.Comanda.ClienteId = clienteId;
            agendamento.Comanda.DataAbertura = dataHora;
            agendamento.Comanda.Observacao = observacao?.Trim();

            // Remove itens de serviços anteriores mantendo itens de produto
            var servicosAnteriores = agendamento.Comanda.Itens
                .Where(i => i.Tipo == TipoItemComanda.Servico)
                .ToList();

            foreach (var item in servicosAnteriores)
            {
                agendamento.Comanda.Itens.Remove(item);
            }

            foreach (var servico in servicos)
            {
                var custom = itensCustomizados?.FirstOrDefault(i => i.ServicoId == servico.Id);
                var valorUnitario = custom != null ? custom.Valor : servico.Valor;

                agendamento.Comanda.Itens.Add(new ComandaItem
                {
                    Comanda = agendamento.Comanda,
                    Tipo = TipoItemComanda.Servico,
                    ServicoId = servico.Id,
                    Descricao = servico.NomeExibicao,
                    Quantidade = 1,
                    ValorUnitario = valorUnitario
                });
            }

            agendamento.Comanda.ValorTotal = agendamento.Comanda.CalcularTotalItens();
        }

        agendamento.VersaoLocal++;
        agendamento.SincronizadoComICloud = false;

        await _context.SaveChangesAsync();

        // Integração offline-first com o Calendário do iPhone (iCloud)
        if (_iCloudSyncService != null)
        {
            try
            {
                await _iCloudSyncService.EnfileirarOperacaoAsync(agendamento.Id, agendamento.ICloudEventId, TipoOperacaoSincronizacao.Atualizar, agendamento);
                _iCloudSyncService.DispararSincronizacaoEmSegundoPlano();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AgendamentoService] Falha ao enfileirar atualização para iPhone / iCloud: {ex.Message}");
            }
        }

        return agendamento;
    }

    public async Task AtualizarStatusAsync(int agendamentoId, StatusAgendamento status)
    {
        var agendamento = await _context.Agendamentos.FindAsync(agendamentoId);
        if (agendamento == null)
            throw new InvalidOperationException("Agendamento não encontrado.");

        agendamento.Status = status;
        await _context.SaveChangesAsync();
    }

    public async Task CancelarAgendamentoAsync(int agendamentoId)
    {
        var agendamento = await _context.Agendamentos
            .Include(a => a.Comanda)
            .FirstOrDefaultAsync(a => a.Id == agendamentoId);

        if (agendamento == null)
            throw new InvalidOperationException("Agendamento não encontrado.");

        agendamento.Status = StatusAgendamento.Cancelado;
        agendamento.VersaoLocal++;
        agendamento.SincronizadoComICloud = false;

        // Se a comanda ainda estiver aberta, cancela a comanda também
        if (agendamento.Comanda != null && agendamento.Comanda.Status == StatusComanda.Aberta)
        {
            agendamento.Comanda.Status = StatusComanda.Cancelada;
        }

        await _context.SaveChangesAsync();

        // Enfileira atualização de cancelamento para o Calendário do iPhone (iCloud)
        if (_iCloudSyncService != null)
        {
            try
            {
                await _iCloudSyncService.EnfileirarOperacaoAsync(agendamento.Id, agendamento.ICloudEventId, TipoOperacaoSincronizacao.Atualizar, agendamento);
                _iCloudSyncService.DispararSincronizacaoEmSegundoPlano();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AgendamentoService] Falha ao enfileirar cancelamento no iCloud: {ex.Message}");
            }
        }
    }

    public async Task ExcluirAgendamentoAsync(int agendamentoId)
    {
        var agendamento = await _context.Agendamentos
            .Include(a => a.Comanda)
            .FirstOrDefaultAsync(a => a.Id == agendamentoId);

        if (agendamento == null)
            return;

        var iCloudEventId = agendamento.ICloudEventId;

        // Se a comanda vinculada estiver aberta, remove a comanda também (hard delete).
        // Se já estiver finalizada, desvincula o agendamento para permitir o hard delete do agendamento mantendo a comanda contábil.
        if (agendamento.Comanda != null)
        {
            if (agendamento.Comanda.Status == StatusComanda.Aberta)
            {
                _context.Comandas.Remove(agendamento.Comanda);
            }
            else
            {
                agendamento.Comanda.AgendamentoId = null;
                agendamento.Comanda.Agendamento = null;
            }
        }

        // Hard Delete definitivo do Agendamento
        _context.Agendamentos.Remove(agendamento);
        await _context.SaveChangesAsync();

        // Enfileira exclusão para o Calendário do iPhone (iCloud)
        if (_iCloudSyncService != null && !string.IsNullOrEmpty(iCloudEventId))
        {
            try
            {
                await _iCloudSyncService.EnfileirarOperacaoAsync(agendamentoId, iCloudEventId, TipoOperacaoSincronizacao.Excluir, null);
                _iCloudSyncService.DispararSincronizacaoEmSegundoPlano();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AgendamentoService] Falha ao enfileirar exclusão no iCloud: {ex.Message}");
            }
        }
    }
}
