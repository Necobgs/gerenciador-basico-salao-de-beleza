using System.Globalization;

namespace GerenciadorSalao.Models.DTOs;

public class FaturamentoDetalhadoMesDto
{
    private static readonly CultureInfo PtBrCulture = new("pt-BR");

    public int Mes { get; set; }
    public int Ano { get; set; }
    public string NomeMes { get; set; } = string.Empty;
    public decimal TotalFaturado { get; set; }
    public int TotalAtendimentos { get; set; }
    public decimal TicketMedio => TotalAtendimentos > 0 ? Math.Round(TotalFaturado / TotalAtendimentos, 2) : 0m;

    public string TotalFaturadoTexto => TotalFaturado.ToString("C2", PtBrCulture);
    public string TicketMedioTexto => TicketMedio.ToString("C2", PtBrCulture);
}
