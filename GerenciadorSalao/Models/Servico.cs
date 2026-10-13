namespace GerenciadorSalao.Models;

public class Servico
{
    public int Id { get; set; }

    public string Descricao { get; set; } = string.Empty;

    public decimal Valor { get; set; }

    public int DuracaoMinutos { get; set; } = 30;

    public bool Ativo { get; set; } = true;

    public DateTime? DeletedAt { get; set; }

    public ICollection<AgendamentoServico> AgendamentoServicos { get; set; } = new List<AgendamentoServico>();
}
