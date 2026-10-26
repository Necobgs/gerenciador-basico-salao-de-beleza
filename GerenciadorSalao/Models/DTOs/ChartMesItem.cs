namespace GerenciadorSalao.Models.DTOs;

public class ChartMesItem
{
    public int Mes { get; set; }
    public string MesNome { get; set; } = string.Empty;
    public string MesNomeCompletoAno { get; set; } = string.Empty;
    public decimal Valor { get; set; }
    public string ValorFormatado { get; set; } = string.Empty;
    public string TooltipTexto { get; set; } = string.Empty;
    public int TotalAtendimentos { get; set; }
    public string TotalAtendimentosTexto => TotalAtendimentos == 1 ? "1 atendimento" : $"{TotalAtendimentos} atendimentos";
    public decimal TicketMedio { get; set; }
    public string TicketMedioFormatado { get; set; } = string.Empty;
    public bool TemAtendimentos => TotalAtendimentos > 0;
    public bool TemValor => Valor > 0;

    // Coordenadas absolutas na tela 900x200
    public double X { get; set; }
    public double Y { get; set; }
    public double ColumnLeft { get; set; }
    public double ColumnWidth { get; set; }

    // Coordenadas locais dentro da coluna do mês
    public double LocalCenterX => ColumnWidth / 2.0;
    public double LocalPointLeft => (ColumnWidth / 2.0) - 4.0;
    public double LocalPointTop => Y - 4.0;
}
