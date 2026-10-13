namespace GerenciadorSalao.Models;

public class ComandaItem
{
    public int Id { get; set; }

    public int ComandaId { get; set; }
    public Comanda Comanda { get; set; } = null!;

    public TipoItemComanda Tipo { get; set; }

    public int? ServicoId { get; set; }
    public Servico? Servico { get; set; }

    public int? ProdutoId { get; set; }
    public Produto? Produto { get; set; }

    public string Descricao { get; set; } = string.Empty;

    public int Quantidade { get; set; } = 1;

    public decimal ValorUnitario { get; set; }

    public decimal Subtotal => Quantidade * ValorUnitario;
}
