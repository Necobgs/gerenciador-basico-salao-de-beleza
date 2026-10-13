namespace GerenciadorSalao.Models;

public class Produto
{
    public int Id { get; set; }

    public string Descricao { get; set; } = string.Empty;

    public decimal Valor { get; set; }

    public int Estoque { get; set; } = 0;

    public bool Ativo { get; set; } = true;

    public DateTime? DeletedAt { get; set; }
}
