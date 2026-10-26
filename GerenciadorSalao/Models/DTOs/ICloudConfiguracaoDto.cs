namespace GerenciadorSalao.Models.DTOs;

/// <summary>
/// Informações de configuração e credenciais para conexão ao iCloud Calendar (iPhone).
/// </summary>
public class ICloudConfiguracaoDto
{
    /// <summary>
    /// E-mail do Apple ID utilizado no iPhone (ex.: salao@icloud.com).
    /// </summary>
    public string AppleIdEmail { get; set; } = string.Empty;

    /// <summary>
    /// Senha específica de app gerada no site appleid.apple.com (formato xxxx-xxxx-xxxx-xxxx).
    /// </summary>
    public string SenhaApp { get; set; } = string.Empty;

    /// <summary>
    /// Indica se a integração com o iPhone / iCloud está ativada.
    /// </summary>
    public bool EstaAtivo { get; set; }

    /// <summary>
    /// URL do calendário selecionado no iCloud CalDAV.
    /// </summary>
    public string? UrlCalendario { get; set; }

    /// <summary>
    /// Nome de exibição do calendário no iCloud (ex.: "Salão", "Principal").
    /// </summary>
    public string? NomeCalendario { get; set; }

    /// <summary>
    /// Data e hora da última sincronização bem-sucedida.
    /// </summary>
    public DateTime? UltimaSincronizacaoSucesso { get; set; }

    /// <summary>
    /// Estado atual da sincronização com o serviço remoto.
    /// </summary>
    public EstadoSincronizacao Estado { get; set; } = EstadoSincronizacao.Offline;

    /// <summary>
    /// Mensagem do último erro ou status detalhado.
    /// </summary>
    public string? UltimoErro { get; set; }

    /// <summary>
    /// Quantidade de alterações pendentes de sincronização no SQLite.
    /// </summary>
    public int OperacoesPendentesCount { get; set; }

    /// <summary>
    /// Quantidade de conflitos aguardando resolução.
    /// </summary>
    public int ConflitosCount { get; set; }
}
