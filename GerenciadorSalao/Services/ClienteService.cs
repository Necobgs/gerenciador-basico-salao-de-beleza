using GerenciadorSalao.Data;
using GerenciadorSalao.Helpers;
using GerenciadorSalao.Models;
using Microsoft.EntityFrameworkCore;

namespace GerenciadorSalao.Services;

public class ClienteService : IClienteService
{
    private readonly AppDbContext _context;

    public ClienteService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<List<Cliente>> ObterTodosAsync(string? termoBusca = null)
    {
        var query = _context.Clientes.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(termoBusca))
        {
            var termo = termoBusca.Trim().ToLower();
            var termoDigitos = ValidationHelper.LimparApenasDigitos(termoBusca);

            query = query.Where(c => c.Nome.ToLower().Contains(termo) ||
                                     (c.Apelido != null && c.Apelido.ToLower().Contains(termo)) ||
                                     (c.Telefone != null && (c.Telefone.Contains(termo) || (!string.IsNullOrEmpty(termoDigitos) && c.Telefone.Contains(termoDigitos)))) ||
                                     (c.Email != null && c.Email.ToLower().Contains(termo)));
        }

        return await query.OrderBy(c => c.Nome).ToListAsync();
    }

    public async Task<Cliente?> ObterPorIdAsync(int id)
    {
        return await _context.Clientes
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == id);
    }

    public async Task<Cliente> AdicionarAsync(Cliente cliente)
    {
        if (!ValidationHelper.ValidarNome(cliente.Nome, out var erroNome))
            throw new ArgumentException(erroNome, nameof(cliente.Nome));

        if (!ValidationHelper.ValidarTelefone(cliente.Telefone, out var erroTelefone, obrigatorio: false))
            throw new ArgumentException(erroTelefone, nameof(cliente.Telefone));

        if (!ValidationHelper.ValidarEmail(cliente.Email, out var erroEmail, obrigatorio: false))
            throw new ArgumentException(erroEmail, nameof(cliente.Email));

        cliente.Nome = cliente.Nome.Trim();
        cliente.Apelido = string.IsNullOrWhiteSpace(cliente.Apelido) ? null : cliente.Apelido.Trim();
        cliente.Telefone = string.IsNullOrWhiteSpace(cliente.Telefone) ? null : ValidationHelper.FormatarTelefone(cliente.Telefone);
        cliente.Email = string.IsNullOrWhiteSpace(cliente.Email) ? null : cliente.Email.Trim();
        cliente.Observacao = string.IsNullOrWhiteSpace(cliente.Observacao) ? null : cliente.Observacao.Trim();
        cliente.DataCadastro = DateTime.Now;

        _context.Clientes.Add(cliente);
        await _context.SaveChangesAsync();
        return cliente;
    }

    public async Task AtualizarAsync(Cliente cliente)
    {
        if (!ValidationHelper.ValidarNome(cliente.Nome, out var erroNome))
            throw new ArgumentException(erroNome, nameof(cliente.Nome));

        if (!ValidationHelper.ValidarTelefone(cliente.Telefone, out var erroTelefone, obrigatorio: false))
            throw new ArgumentException(erroTelefone, nameof(cliente.Telefone));

        if (!ValidationHelper.ValidarEmail(cliente.Email, out var erroEmail, obrigatorio: false))
            throw new ArgumentException(erroEmail, nameof(cliente.Email));

        var existente = await _context.Clientes.FindAsync(cliente.Id);
        if (existente == null)
            throw new InvalidOperationException("Cliente não encontrado para atualização.");

        existente.Nome = cliente.Nome.Trim();
        existente.Apelido = string.IsNullOrWhiteSpace(cliente.Apelido) ? null : cliente.Apelido.Trim();
        existente.Telefone = string.IsNullOrWhiteSpace(cliente.Telefone) ? null : ValidationHelper.FormatarTelefone(cliente.Telefone);
        existente.Email = string.IsNullOrWhiteSpace(cliente.Email) ? null : cliente.Email.Trim();
        existente.Observacao = string.IsNullOrWhiteSpace(cliente.Observacao) ? null : cliente.Observacao.Trim();

        await _context.SaveChangesAsync();
    }

    public async Task ExcluirAsync(int id)
    {
        var cliente = await _context.Clientes.FindAsync(id);
        if (cliente == null)
            return;

        // Soft Delete: marca a data de exclusão
        cliente.DeletedAt = DateTime.Now;
        await _context.SaveChangesAsync();
    }

    public async Task<Cliente?> ObterHistoricoClienteAsync(int clienteId)
    {
        return await _context.Clientes
            .AsNoTracking()
            .Include(c => c.Agendamentos.OrderByDescending(a => a.DataHora))
                .ThenInclude(a => a.AgendamentoServicos)
                .ThenInclude(asv => asv.Servico)
            .Include(c => c.Comandas.OrderByDescending(co => co.DataAbertura))
                .ThenInclude(co => co.Itens)
            .FirstOrDefaultAsync(c => c.Id == clienteId);
    }
}
