namespace GerenciadorSalao.Models.DTOs;

public class TopServicoDto
{
    public int ServicoId { get; set; }
    public string NomeServico { get; set; } = string.Empty;
    public int QuantidadeRealizada { get; set; }
    public int DuracaoMediaMinutos { get; set; }
    public double Percentual { get; set; }
}
