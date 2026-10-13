namespace GerenciadorSalao.Models;

public class Cliente
{
    public int Id { get; set; }

    public string Nome { get; set; } = string.Empty;

    public string? Apelido { get; set; }

    public string? Telefone { get; set; }

    public string? Email { get; set; }

    public string? Observacao { get; set; }

    public DateTime DataCadastro { get; set; } = DateTime.Now;

    public DateTime? DeletedAt { get; set; }

    public ICollection<Agendamento> Agendamentos { get; set; } = new List<Agendamento>();

    public ICollection<Comanda> Comandas { get; set; } = new List<Comanda>();

    public string NomeExibicao => string.IsNullOrWhiteSpace(Apelido) ? Nome : $"{Nome} ({Apelido})";
}
