namespace GerenciadorSalao.Models.DTOs;

public class CalendarDayItem
{
    public DateTime Data { get; set; }
    public int Dia => Data.Day;
    public bool IsMesAtual { get; set; }
    public bool IsSelecionado { get; set; }
    public bool IsHoje { get; set; }
}
