namespace GerenciadorSalao.Models;

public class Comanda
{
    public int Id { get; set; }

    public int ClienteId { get; set; }
    public Cliente Cliente { get; set; } = null!;

    public int? AgendamentoId { get; set; }
    public Agendamento? Agendamento { get; set; }

    public DateTime DataAbertura { get; set; } = DateTime.Now;

    public DateTime? DataFechamento { get; set; }

    public StatusComanda Status { get; set; } = StatusComanda.Aberta;

    public string? Observacao { get; set; }

    public decimal ValorTotal { get; set; }

    public ICollection<ComandaItem> Itens { get; set; } = new List<ComandaItem>();

    public decimal CalcularTotalItens() => Itens.Sum(i => i.Subtotal);
}
