namespace GerenciadorSalao.Models.DTOs;

public class TopClienteDto
{
    public int ClienteId { get; set; }
    public string NomeCliente { get; set; } = string.Empty;
    public string? Telefone { get; set; }
    public decimal TotalGasto { get; set; }
    public int QuantidadeComandas { get; set; }
}
