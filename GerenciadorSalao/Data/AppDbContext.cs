using System.IO;
using GerenciadorSalao.Models;
using Microsoft.EntityFrameworkCore;

namespace GerenciadorSalao.Data;

public class AppDbContext : DbContext
{
    public DbSet<Cliente> Clientes => Set<Cliente>();
    public DbSet<Servico> Servicos => Set<Servico>();
    public DbSet<Produto> Produtos => Set<Produto>();
    public DbSet<Agendamento> Agendamentos => Set<Agendamento>();
    public DbSet<AgendamentoServico> AgendamentoServicos => Set<AgendamentoServico>();
    public DbSet<Comanda> Comandas => Set<Comanda>();
    public DbSet<ComandaItem> ComandaItens => Set<ComandaItem>();

    public AppDbContext()
    {
    }

    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public static string ObterCaminhoBanco()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var pastaApp = Path.Combine(localAppData, "GerenciadorSalao");

        if (!Directory.Exists(pastaApp))
        {
            Directory.CreateDirectory(pastaApp);
        }

        return Path.Combine(pastaApp, "salao.db");
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (!optionsBuilder.IsConfigured)
        {
            var dbPath = ObterCaminhoBanco();
            optionsBuilder.UseSqlite($"Data Source={dbPath}");
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Cliente
        modelBuilder.Entity<Cliente>(entity =>
        {
            entity.HasKey(c => c.Id);
            entity.Property(c => c.Nome).IsRequired().HasMaxLength(150);
            entity.Property(c => c.Apelido).HasMaxLength(100);
            entity.Property(c => c.Telefone).HasMaxLength(30);
            entity.Property(c => c.Email).HasMaxLength(150);
            entity.Property(c => c.Observacao).HasMaxLength(1000);

            entity.HasQueryFilter(c => c.DeletedAt == null);
        });

        // Servico
        modelBuilder.Entity<Servico>(entity =>
        {
            entity.HasKey(s => s.Id);
            entity.Property(s => s.Descricao).IsRequired().HasMaxLength(150);
            entity.Property(s => s.Especificacao).HasMaxLength(100);
            entity.Property(s => s.DuracaoFormatada).HasMaxLength(20);
            entity.Property(s => s.Valor).HasPrecision(18, 2);

            entity.HasQueryFilter(s => s.DeletedAt == null);
        });

        // Produto
        modelBuilder.Entity<Produto>(entity =>
        {
            entity.HasKey(p => p.Id);
            entity.Property(p => p.Descricao).IsRequired().HasMaxLength(150);
            entity.Property(p => p.Valor).HasPrecision(18, 2);

            entity.HasQueryFilter(p => p.DeletedAt == null);
        });

        // Agendamento
        modelBuilder.Entity<Agendamento>(entity =>
        {
            entity.HasKey(a => a.Id);
            entity.Property(a => a.Observacao).HasMaxLength(1000);
            entity.Property(a => a.GoogleEventId).HasMaxLength(255);

            entity.HasOne(a => a.Cliente)
                  .WithMany(c => c.Agendamentos)
                  .HasForeignKey(a => a.ClienteId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // AgendamentoServico (N:N)
        modelBuilder.Entity<AgendamentoServico>(entity =>
        {
            entity.HasKey(asv => asv.Id);

            entity.HasOne(asv => asv.Agendamento)
                  .WithMany(a => a.AgendamentoServicos)
                  .HasForeignKey(asv => asv.AgendamentoId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(asv => asv.Servico)
                  .WithMany(s => s.AgendamentoServicos)
                  .HasForeignKey(asv => asv.ServicoId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // Comanda
        modelBuilder.Entity<Comanda>(entity =>
        {
            entity.HasKey(c => c.Id);
            entity.Property(c => c.ValorTotal).HasPrecision(18, 2);
            entity.Property(c => c.Observacao).HasMaxLength(2000);

            entity.HasOne(c => c.Cliente)
                  .WithMany(cl => cl.Comandas)
                  .HasForeignKey(c => c.ClienteId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(c => c.Agendamento)
                  .WithOne(a => a.Comanda)
                  .HasForeignKey<Comanda>(c => c.AgendamentoId)
                  .OnDelete(DeleteBehavior.SetNull);
        });

        // ComandaItem
        modelBuilder.Entity<ComandaItem>(entity =>
        {
            entity.HasKey(ci => ci.Id);
            entity.Property(ci => ci.Descricao).IsRequired().HasMaxLength(150);
            entity.Property(ci => ci.ValorUnitario).HasPrecision(18, 2);

            entity.HasOne(ci => ci.Comanda)
                  .WithMany(c => c.Itens)
                  .HasForeignKey(ci => ci.ComandaId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(ci => ci.Servico)
                  .WithMany()
                  .HasForeignKey(ci => ci.ServicoId)
                  .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(ci => ci.Produto)
                  .WithMany()
                  .HasForeignKey(ci => ci.ProdutoId)
                  .OnDelete(DeleteBehavior.SetNull);
        });
    }
}
