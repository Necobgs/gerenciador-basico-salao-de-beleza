namespace GerenciadorSalao.Models;

/// <summary>
/// Registra um conflito detectado durante a sincronização entre a versão local e a versão remota do iCloud / iPhone.
/// Nenhuma versão é descartada silenciosamente.
/// </summary>
public class ConflitoSincronizacao
{
    public int Id { get; set; }

    /// <summary>
    /// ID do agendamento local em conflito, se existir.
    /// </summary>
    public int? AgendamentoId { get; set; }

    /// <summary>
    /// Identificador único remoto (UID CalDAV) do evento em conflito.
    /// </summary>
    public string? ICloudEventId { get; set; }

    /// <summary>
    /// Título descritivo do conflito (ex.: "Alteração concorrente de horário", "Excluído no iPhone mas alterado no Salão").
    /// </summary>
    public string Titulo { get; set; } = string.Empty;

    /// <summary>
    /// Detalhes explicativos da divergência encontrada.
    /// </summary>
    public string Descricao { get; set; } = string.Empty;

    /// <summary>
    /// Dados locais serializados em JSON no momento do conflito.
    /// </summary>
    public string DadosLocaisJson { get; set; } = string.Empty;

    /// <summary>
    /// Dados remotos serializados em JSON retornados pelo servidor iCloud.
    /// </summary>
    public string DadosRemotosJson { get; set; } = string.Empty;

    /// <summary>
    /// Data e hora da detecção do conflito.
    /// </summary>
    public DateTime CriadoEm { get; set; } = DateTime.Now;

    /// <summary>
    /// Indica se o conflito foi resolvido pelo usuário ou pelo sistema.
    /// </summary>
    public bool Resolvido { get; set; }

    /// <summary>
    /// Data e hora da resolução.
    /// </summary>
    public DateTime? ResolvidoEm { get; set; }

    /// <summary>
    /// Resolução aplicada: "ManterLocal", "ManterRemoto" ou "ManterAmbos".
    /// </summary>
    public string? ResolucaoEscolhida { get; set; }
}
