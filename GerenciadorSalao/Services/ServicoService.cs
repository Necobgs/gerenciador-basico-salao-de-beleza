using GerenciadorSalao.Data;
using GerenciadorSalao.Helpers;
using GerenciadorSalao.Models;
using Microsoft.EntityFrameworkCore;

namespace GerenciadorSalao.Services;

public class ServicoService : IServicoService
{
    private readonly AppDbContext _context;

    public ServicoService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<List<Servico>> ObterTodosAsync(string? termoBusca = null, bool apenasAtivos = true)
    {
        var query = _context.Servicos.AsNoTracking().AsQueryable();

        if (apenasAtivos)
            query = query.Where(s => s.Ativo && s.DeletedAt == null);

        if (!string.IsNullOrWhiteSpace(termoBusca))
        {
            var termo = termoBusca.Trim().ToLower();
            query = query.Where(s => s.Descricao.ToLower().Contains(termo));
        }

        return await query.OrderBy(s => s.Descricao).ToListAsync();
    }

    public async Task<Servico?> ObterPorIdAsync(int id)
    {
        return await _context.Servicos
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == id && s.DeletedAt == null);
    }

    public async Task<Servico> AdicionarAsync(Servico servico)
    {
        if (!ValidationHelper.ValidarDescricaoServico(servico.Descricao, out var erroDescricao))
            throw new ArgumentException(erroDescricao, nameof(servico.Descricao));

        if (servico.Valor < 0)
            throw new ArgumentException("O valor do serviço não pode ser negativo.", nameof(servico.Valor));

        if (servico.DuracaoMinutos <= 0)
            servico.DuracaoMinutos = 30;

        servico.Descricao = servico.Descricao.Trim();

        _context.Servicos.Add(servico);
        await _context.SaveChangesAsync();
        return servico;
    }

    public async Task AtualizarAsync(Servico servico)
    {
        if (!ValidationHelper.ValidarDescricaoServico(servico.Descricao, out var erroDescricao))
            throw new ArgumentException(erroDescricao, nameof(servico.Descricao));

        if (servico.Valor < 0)
            throw new ArgumentException("O valor do serviço não pode ser negativo.", nameof(servico.Valor));

        var existente = await _context.Servicos.FindAsync(servico.Id);
        if (existente == null || existente.DeletedAt != null)
            throw new InvalidOperationException("Serviço não encontrado para atualização.");

        existente.Descricao = servico.Descricao.Trim();
        existente.Valor = servico.Valor;
        existente.DuracaoMinutos = servico.DuracaoMinutos > 0 ? servico.DuracaoMinutos : 30;
        existente.Ativo = servico.Ativo;

        await _context.SaveChangesAsync();
    }

    public async Task DesativarAsync(int id)
    {
        var servico = await _context.Servicos.FindAsync(id);
        if (servico != null)
        {
            servico.Ativo = false;
            await _context.SaveChangesAsync();
        }
    }

    public async Task ExcluirAsync(int id)
    {
        var servico = await _context.Servicos.FindAsync(id);
        if (servico == null)
            return;

        // Soft Delete: marca a data de exclusão e inativa
        servico.DeletedAt = DateTime.Now;
        servico.Ativo = false;
        await _context.SaveChangesAsync();
    }
}
