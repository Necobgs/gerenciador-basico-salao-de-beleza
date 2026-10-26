using GerenciadorSalao.Models;
using GerenciadorSalao.Models.DTOs;

namespace GerenciadorSalao.Services;

/// <summary>
/// Serviço de alto nível de sincronização bidirecional offline-first entre o SQLite e o iPhone / iCloud Calendar.
/// </summary>
public interface IICloudSyncService
{
    EstadoSincronizacao EstadoAtual { get; }
    DateTime? UltimaSincronizacaoSucesso { get; }
    int OperacoesPendentesCount { get; }
    int ConflitosCount { get; }
    string? UltimoErro { get; }

    /// <summary>
    /// Evento disparado quando o estado de sincronização ou contagem de pendências muda.
    /// </summary>
    event EventHandler<EstadoSincronizacao>? EstadoMudou;

    /// <summary>
    /// Retorna as configurações atuais de integração.
    /// </summary>
    Task<ICloudConfiguracaoDto> ObterConfiguracaoAsync();

    /// <summary>
    /// Salva as credenciais do Apple ID e senha de app.
    /// </summary>
    Task SalvarConfiguracaoAsync(string email, string senhaApp, bool ativar, bool dispararSincronizacao = true);

    /// <summary>
    /// Desconecta a conta e limpa as credenciais salvas.
    /// </summary>
    Task DesconectarAsync();

    /// <summary>
    /// Enfileira ou consolida de forma segura uma operação pendente de sincronização no SQLite.
    /// Executa imediatamente sem bloquear a interface com chamadas de rede.
    /// </summary>
    Task EnfileirarOperacaoAsync(int? agendamentoId, string? iCloudEventId, TipoOperacaoSincronizacao tipo, Agendamento? agendamentoSnapshot);

    /// <summary>
    /// Executa o ciclo completo de sincronização e recuperação (sequência de 10 passos).
    /// </summary>
    Task<ICloudSyncResultadoDto> SincronizarAgoraAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Dispara uma tentativa de sincronização assíncrona em segundo plano com controle de concorrência e backoff.
    /// </summary>
    void DispararSincronizacaoEmSegundoPlano();

    /// <summary>
    /// Retorna as operações atualmente pendentes na fila SQLite.
    /// </summary>
    Task<List<OperacaoSincronizacao>> ObterOperacoesPendentesAsync();

    /// <summary>
    /// Retorna os conflitos de sincronização identificados e não resolvidos.
    /// </summary>
    Task<List<ConflitoSincronizacao>> ObterConflitosNaoResolvidosAsync();

    /// <summary>
    /// Aplica a decisão do usuário em um conflito ("ManterLocal", "ManterRemoto", "ManterAmbos").
    /// </summary>
    Task ResolverConflitoAsync(int conflitoId, string decisao);

    /// <summary>
    /// Permite substituir o provedor em tempo de execução (ex.: alternar para SimulatedICloudCalendarProvider em testes).
    /// </summary>
    void SetProvider(IICloudCalendarProvider provider);

    /// <summary>
    /// Retorna o provedor de calendário atualmente em uso.
    /// </summary>
    IICloudCalendarProvider ObterProviderAtual();

    /// <summary>
    /// Inicia o monitoramento de rede e timer periódico com backoff e jitter.
    /// </summary>
    void IniciarMonitoramento();

    /// <summary>
    /// Interrompe o monitoramento de fundo.
    /// </summary>
    void PararMonitoramento();
}
