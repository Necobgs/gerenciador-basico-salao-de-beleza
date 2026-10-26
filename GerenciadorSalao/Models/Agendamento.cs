namespace GerenciadorSalao.Models;

public class Agendamento
{
    public int Id { get; set; }

    public int ClienteId { get; set; }
    public Cliente Cliente { get; set; } = null!;

    public DateTime DataHora { get; set; }

    public int TempoTotalPrevistoMinutos { get; set; }

    public StatusAgendamento Status { get; set; } = StatusAgendamento.Pendente;

    public string? Observacao { get; set; }

    public string? GoogleEventId { get; set; }

    public string? ICloudEventId { get; set; }
    public string? ICloudETag { get; set; }
    public string? ICloudUrl { get; set; }
    public int VersaoLocal { get; set; } = 1;
    public DateTime? UltimaSincronizacaoICloud { get; set; }
    public bool SincronizadoComICloud { get; set; }

    public int? ComandaId { get; set; }
    public Comanda? Comanda { get; set; }

    public ICollection<AgendamentoServico> AgendamentoServicos { get; set; } = new List<AgendamentoServico>();
}
