namespace GerenciadorSalao.Models.DTOs;

public class FaturamentoMensalDto
{
    public int Mes { get; set; }
    public int Ano { get; set; }
    public decimal Total { get; set; }

    public string NomeMes => new DateTime(Ano, Mes, 1).ToString("MMMM");
}
