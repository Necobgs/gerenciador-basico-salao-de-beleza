namespace GerenciadorSalao.Models.DTOs;

/// <summary>
/// Representa os dados normalizados de um evento no calendário remoto do iCloud (iPhone / CalDAV).
/// </summary>
public class ICloudEventoRemoto
{
    /// <summary>
    /// Identificador único mundial (UID RFC 5545) do evento.
    /// </summary>
    public string Uid { get; set; } = string.Empty;

    /// <summary>
    /// ETag HTTP retornado pelo servidor CalDAV para controle de concorrência e detecção de alterações.
    /// </summary>
    public string? ETag { get; set; }

    /// <summary>
    /// Caminho/URL do recurso no servidor CalDAV (ex.: /123/calendars/home/xyz.ics).
    /// </summary>
    public string? Url { get; set; }

    /// <summary>
    /// Título / Resumo do evento (SUMMARY).
    /// </summary>
    public string Titulo { get; set; } = string.Empty;

    /// <summary>
    /// Descrição detalhada do evento (DESCRIPTION).
    /// </summary>
    public string? Descricao { get; set; }

    /// <summary>
    /// Data e hora de início (DTSTART).
    /// </summary>
    public DateTime DataHoraInicio { get; set; }

    /// <summary>
    /// Data e hora de término (DTEND).
    /// </summary>
    public DateTime DataHoraFim { get; set; }

    /// <summary>
    /// Duração calculada em minutos.
    /// </summary>
    public int DuracaoMinutos => (int)Math.Max(10, Math.Min(480, (DataHoraFim - DataHoraInicio).TotalMinutes));

    /// <summary>
    /// Nome do cliente inferido a partir do título ou descrição.
    /// </summary>
    public string? NomeCliente { get; set; }

    /// <summary>
    /// Telefone do cliente extraído da descrição, se presente.
    /// </summary>
    public string? TelefoneCliente { get; set; }

    /// <summary>
    /// Indica se o evento está com status cancelado no iCalendar (STATUS:CANCELLED).
    /// </summary>
    public bool IsCancelado { get; set; }

    /// <summary>
    /// Data da última modificação do evento no servidor (LAST-MODIFIED / DTSTAMP).
    /// </summary>
    public DateTime? UltimaModificacao { get; set; }

    /// <summary>
    /// Conteúdo bruto em formato iCalendar (.ics).
    /// </summary>
    public string? RawIcs { get; set; }
}
