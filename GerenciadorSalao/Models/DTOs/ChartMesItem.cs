namespace GerenciadorSalao.Models.DTOs;

public class ChartMesItem
{
    public int Mes { get; set; }
    public string MesNome { get; set; } = string.Empty;
    public decimal Valor { get; set; }
    public string ValorFormatado { get; set; } = string.Empty;
    public string TooltipTexto { get; set; } = string.Empty;
    public double X { get; set; }
    public double Y { get; set; }
    public bool TemValor => Valor > 0;
}
