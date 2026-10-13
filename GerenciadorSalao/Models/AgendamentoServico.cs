namespace GerenciadorSalao.Models;

public class AgendamentoServico
{
    public int Id { get; set; }

    public int AgendamentoId { get; set; }
    public Agendamento Agendamento { get; set; } = null!;

    public int ServicoId { get; set; }
    public Servico Servico { get; set; } = null!;
}
