using GerenciadorSalao.Models;

namespace GerenciadorSalao.Services;

public interface IGoogleCalendarService
{
    /// <summary>
    /// Verifica se o arquivo de credenciais do Google (client_secrets/credentials.json) existe no sistema.
    /// </summary>
    bool ArquivoCredenciaisExiste();

    /// <summary>
    /// Retorna o caminho onde o arquivo google_credentials.json deve ser colocado.
    /// </summary>
    string ObterCaminhoCredenciais();

    /// <summary>
    /// Verifica se já existe um token autenticado e válido para uso do Google Calendar.
    /// </summary>
    Task<bool> EstaConectadoAsync();

    /// <summary>
    /// Inicia o fluxo de autorização OAuth 2.0 (abrindo navegador se necessário para consentimento).
    /// </summary>
    Task<bool> InicializarOuAutenticarAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Cria um evento na agenda principal do Google com os dados do agendamento.
    /// </summary>
    Task<string?> CriarEventoAsync(Agendamento agendamento);

    /// <summary>
    /// Atualiza um evento existente no Google Agenda.
    /// </summary>
    Task<bool> AtualizarEventoAsync(Agendamento agendamento);

    /// <summary>
    /// Cancela/remove um evento no Google Agenda pelo identificador externo.
    /// </summary>
    Task<bool> CancelarEventoAsync(string googleEventId);

    /// <summary>
    /// Remove as credenciais e tokens locais desconectando da conta Google.
    /// </summary>
    Task DesconectarAsync();
}
