namespace GerenciadorSalao.Models.DTOs;

public class ComandaItemDto
{
    public int? Id { get; set; }
    public TipoItemComanda Tipo { get; set; }
    public int ReferenciaId { get; set; } // ServicoId ou ProdutoId
    public string Descricao { get; set; } = string.Empty;
    public int Quantidade { get; set; } = 1;
    public decimal ValorUnitario { get; set; }
}
