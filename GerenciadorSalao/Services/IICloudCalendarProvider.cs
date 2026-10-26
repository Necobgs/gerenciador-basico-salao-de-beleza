using GerenciadorSalao.Models.DTOs;

namespace GerenciadorSalao.Services;

/// <summary>
/// Provedor de integração com o servidor de calendário do iPhone / iCloud (via CalDAV ou Simulado).
/// </summary>
public interface IICloudCalendarProvider
{
    /// <summary>
    /// Configura as credenciais de autenticação (Apple ID e Senha de App).
    /// </summary>
    void ConfigurarCredenciais(string email, string senhaApp);

    /// <summary>
    /// Verifica se o serviço remoto do iCloud está disponível e respondendo, sem depender de teste genérico de internet.
    /// </summary>
    Task<bool> VerificarDisponibilidadeAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Valida se as credenciais fornecidas são válidas e possuem acesso aos calendários da conta Apple.
    /// </summary>
    Task<bool> ValidarCredenciaisAsync(string email, string senhaApp, CancellationToken cancellationToken = default);

    /// <summary>
    /// Obtém todos os eventos do calendário no intervalo especificado.
    /// </summary>
    Task<List<ICloudEventoRemoto>> ObterEventosAsync(DateTime inicio, DateTime fim, CancellationToken cancellationToken = default);

    /// <summary>
    /// Consulta um evento remoto específico pelo UID único.
    /// Essencial para garantir idempotência após perda de conexão durante gravação.
    /// </summary>
    Task<ICloudEventoRemoto?> ObterEventoPorUidAsync(string uid, CancellationToken cancellationToken = default);

    /// <summary>
    /// Cria um novo evento no calendário remoto.
    /// </summary>
    Task<ICloudRespostaOperacao> CriarEventoAsync(ICloudEventoRemoto evento, CancellationToken cancellationToken = default);

    /// <summary>
    /// Atualiza um evento existente no calendário remoto respeitando o ETag anterior para controle de concorrência.
    /// </summary>
    Task<ICloudRespostaOperacao> AtualizarEventoAsync(ICloudEventoRemoto evento, string? ifMatchEtag = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Remove um evento do calendário remoto pelo UID.
    /// </summary>
    Task<ICloudRespostaOperacao> ExcluirEventoAsync(string uid, string? ifMatchEtag = null, CancellationToken cancellationToken = default);
}
