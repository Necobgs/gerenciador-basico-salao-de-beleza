using GerenciadorSalao.Data;
using GerenciadorSalao.Models;
using GerenciadorSalao.Models.DTOs;
using Microsoft.EntityFrameworkCore;

namespace GerenciadorSalao.Services;

public class ComandaService : IComandaService
{
    private readonly AppDbContext _context;

    public ComandaService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<List<Comanda>> ObterTodasAsync(StatusComanda? status = null)
    {
        var query = _context.Comandas
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Include(c => c.Cliente)
            .Include(c => c.Itens)
            .Include(c => c.Agendamento)
            .AsQueryable();

        if (status.HasValue)
            query = query.Where(c => c.Status == status.Value);

        return await query.OrderByDescending(c => c.DataAbertura).ToListAsync();
    }

    public async Task<List<Comanda>> ObterAbertasAsync()
    {
        return await ObterTodasAsync(StatusComanda.Aberta);
    }

    public async Task<Comanda?> ObterPorIdAsync(int id)
    {
        return await _context.Comandas
            .IgnoreQueryFilters()
            .Include(c => c.Cliente)
            .Include(c => c.Agendamento)
            .Include(c => c.Itens)
                .ThenInclude(i => i.Servico)
            .Include(c => c.Itens)
                .ThenInclude(i => i.Produto)
            .FirstOrDefaultAsync(c => c.Id == id);
    }

    public async Task<Comanda> CriarComandaAvulsaAsync(int clienteId, string? observacao = null)
    {
        var cliente = await _context.Clientes.FindAsync(clienteId);
        if (cliente == null)
            throw new InvalidOperationException("Cliente não encontrado.");

        var comanda = new Comanda
        {
            ClienteId = clienteId,
            DataAbertura = DateTime.Now,
            Status = StatusComanda.Aberta,
            Observacao = observacao?.Trim(),
            ValorTotal = 0m
        };

        _context.Comandas.Add(comanda);
        await _context.SaveChangesAsync();
        return comanda;
    }

    public async Task AdicionarItemServicoAsync(int comandaId, int servicoId, int quantidade = 1, decimal? precoUnitarioCustomizado = null)
    {
        var comanda = await ValidarComandaAbertaParaEdicaoAsync(comandaId);
        var servico = await _context.Servicos.FindAsync(servicoId);
        if (servico == null)
            throw new InvalidOperationException("Serviço não encontrado.");

        var valorFinal = precoUnitarioCustomizado ?? servico.Valor;
        if (valorFinal < 0)
            throw new ArgumentException("O valor unitário não pode ser negativo.");

        var item = new ComandaItem
        {
            ComandaId = comandaId,
            Tipo = TipoItemComanda.Servico,
            ServicoId = servico.Id,
            Descricao = servico.Descricao,
            Quantidade = quantidade > 0 ? quantidade : 1,
            ValorUnitario = valorFinal
        };

        comanda.Itens.Add(item);
        comanda.ValorTotal = comanda.CalcularTotalItens();

        await _context.SaveChangesAsync();
    }

    public async Task AdicionarItemProdutoAsync(int comandaId, int produtoId, int quantidade = 1, decimal? precoUnitarioCustomizado = null)
    {
        var comanda = await ValidarComandaAbertaParaEdicaoAsync(comandaId);
        var produto = await _context.Produtos.FindAsync(produtoId);
        if (produto == null)
            throw new InvalidOperationException("Produto não encontrado.");

        var valorFinal = precoUnitarioCustomizado ?? produto.Valor;
        if (valorFinal < 0)
            throw new ArgumentException("O valor unitário não pode ser negativo.");

        var item = new ComandaItem
        {
            ComandaId = comandaId,
            Tipo = TipoItemComanda.Produto,
            ProdutoId = produto.Id,
            Descricao = produto.Descricao,
            Quantidade = quantidade > 0 ? quantidade : 1,
            ValorUnitario = valorFinal
        };

        comanda.Itens.Add(item);
        comanda.ValorTotal = comanda.CalcularTotalItens();

        await _context.SaveChangesAsync();
    }

    public async Task EditarPrecoItemAsync(int itemId, decimal novoValorUnitario)
    {
        if (novoValorUnitario < 0)
            throw new ArgumentException("O valor unitário não pode ser negativo.");

        var item = await _context.ComandaItens
            .Include(i => i.Comanda)
                .ThenInclude(c => c.Itens)
            .FirstOrDefaultAsync(i => i.Id == itemId);

        if (item == null)
            throw new InvalidOperationException("Item de comanda não encontrado.");

        if (item.Comanda.Status != StatusComanda.Aberta)
            throw new InvalidOperationException("Não é permitido editar itens de uma comanda já fechada ou cancelada.");

        item.ValorUnitario = novoValorUnitario;
        item.Comanda.ValorTotal = item.Comanda.CalcularTotalItens();

        await _context.SaveChangesAsync();
    }

    public async Task EditarQuantidadeItemAsync(int itemId, int novaQuantidade)
    {
        if (novaQuantidade <= 0)
            throw new ArgumentException("A quantidade deve ser maior que zero.");

        var item = await _context.ComandaItens
            .Include(i => i.Comanda)
                .ThenInclude(c => c.Itens)
            .FirstOrDefaultAsync(i => i.Id == itemId);

        if (item == null)
            throw new InvalidOperationException("Item de comanda não encontrado.");

        if (item.Comanda.Status != StatusComanda.Aberta)
            throw new InvalidOperationException("Não é permitido editar itens de uma comanda já fechada ou cancelada.");

        item.Quantidade = novaQuantidade;
        item.Comanda.ValorTotal = item.Comanda.CalcularTotalItens();

        await _context.SaveChangesAsync();
    }

    public async Task RemoverItemAsync(int itemId)
    {
        var item = await _context.ComandaItens
            .Include(i => i.Comanda)
                .ThenInclude(c => c.Itens)
            .FirstOrDefaultAsync(i => i.Id == itemId);

        if (item == null)
            return;

        if (item.Comanda.Status != StatusComanda.Aberta)
            throw new InvalidOperationException("Não é permitido remover itens de uma comanda já fechada ou cancelada.");

        var comanda = item.Comanda;
        _context.ComandaItens.Remove(item);
        comanda.Itens.Remove(item);
        comanda.ValorTotal = comanda.CalcularTotalItens();

        await _context.SaveChangesAsync();
    }

    public async Task AtualizarObservacaoAsync(int comandaId, string? observacao)
    {
        var comanda = await ValidarComandaAbertaParaEdicaoAsync(comandaId);
        comanda.Observacao = observacao?.Trim();
        await _context.SaveChangesAsync();
    }

    public async Task FinalizarComandaAsync(int comandaId)
    {
        var comanda = await _context.Comandas
            .Include(c => c.Itens)
            .Include(c => c.Agendamento)
            .FirstOrDefaultAsync(c => c.Id == comandaId);

        if (comanda == null)
            throw new InvalidOperationException("Comanda não encontrada.");

        if (comanda.Status != StatusComanda.Aberta)
            throw new InvalidOperationException("A comanda já se encontra fechada ou cancelada.");

        comanda.ValorTotal = comanda.CalcularTotalItens();
        comanda.Status = StatusComanda.Finalizada;
        comanda.DataFechamento = DateTime.Now;

        // Se estiver vinculada a um agendamento, marca o agendamento como Concluído
        if (comanda.Agendamento != null)
        {
            comanda.Agendamento.Status = StatusAgendamento.Concluido;
        }

        await _context.SaveChangesAsync();
    }

    public async Task ExcluirComandaAsync(int comandaId)
    {
        var comanda = await _context.Comandas
            .Include(c => c.Agendamento)
            .Include(c => c.Itens)
            .FirstOrDefaultAsync(c => c.Id == comandaId);

        if (comanda == null)
            return;

        // Se houver agendamento vinculado, desassocia para não violar integridade
        if (comanda.Agendamento != null)
        {
            comanda.Agendamento.Comanda = null;
            comanda.Agendamento.ComandaId = null;
        }

        _context.Comandas.Remove(comanda);
        await _context.SaveChangesAsync();
    }

    public async Task<Comanda> SalvarComandaCompletaAsync(
        int? comandaId, 
        int clienteId, 
        string? observacao, 
        List<ComandaItemDto> itens, 
        bool finalizar = false)
    {
        if (clienteId <= 0)
            throw new ArgumentException("O cliente da comanda é obrigatório.", nameof(clienteId));

        if (itens == null || itens.Count == 0)
            throw new InvalidOperationException("A comanda precisa ter pelo menos um item (serviço ou produto).");

        foreach (var it in itens)
        {
            if (it.ValorUnitario < 0)
                throw new ArgumentException($"O valor unitário do item '{it.Descricao}' não pode ser negativo.");
            if (it.Quantidade <= 0)
                throw new ArgumentException($"A quantidade do item '{it.Descricao}' deve ser maior que zero.");
        }

        Comanda comanda;

        if (comandaId.HasValue && comandaId.Value > 0)
        {
            comanda = await _context.Comandas
                .Include(c => c.Itens)
                .Include(c => c.Agendamento)
                .FirstOrDefaultAsync(c => c.Id == comandaId.Value)
                ?? throw new InvalidOperationException("Comanda não encontrada.");

            if (comanda.Status != StatusComanda.Aberta)
                throw new InvalidOperationException("Não é permitido alterar uma comanda já fechada ou cancelada.");

            comanda.ClienteId = clienteId;
            comanda.Observacao = observacao?.Trim();

            // Substitui itens
            _context.ComandaItens.RemoveRange(comanda.Itens);
            comanda.Itens.Clear();
        }
        else
        {
            comanda = new Comanda
            {
                ClienteId = clienteId,
                DataAbertura = DateTime.Now,
                Status = StatusComanda.Aberta,
                Observacao = observacao?.Trim()
            };
            _context.Comandas.Add(comanda);
        }

        // Adiciona todos os itens atualizados
        foreach (var it in itens)
        {
            comanda.Itens.Add(new ComandaItem
            {
                Comanda = comanda,
                Tipo = it.Tipo,
                ServicoId = it.Tipo == TipoItemComanda.Servico ? it.ReferenciaId : null,
                ProdutoId = it.Tipo == TipoItemComanda.Produto ? it.ReferenciaId : null,
                Descricao = it.Descricao,
                Quantidade = it.Quantidade,
                ValorUnitario = it.ValorUnitario
            });
        }

        comanda.ValorTotal = comanda.CalcularTotalItens();

        if (finalizar)
        {
            comanda.Status = StatusComanda.Finalizada;
            comanda.DataFechamento = DateTime.Now;

            if (comanda.Agendamento != null)
            {
                comanda.Agendamento.Status = StatusAgendamento.Concluido;
            }
        }

        await _context.SaveChangesAsync();
        return comanda;
    }

    private async Task<Comanda> ValidarComandaAbertaParaEdicaoAsync(int comandaId)
    {
        var comanda = await _context.Comandas
            .Include(c => c.Itens)
            .FirstOrDefaultAsync(c => c.Id == comandaId);

        if (comanda == null)
            throw new InvalidOperationException("Comanda não encontrada.");

        if (comanda.Status != StatusComanda.Aberta)
            throw new InvalidOperationException("Não é permitido alterar nada após fechar a comanda.");

        return comanda;
    }
}
