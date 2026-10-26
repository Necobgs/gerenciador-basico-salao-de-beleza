namespace GerenciadorSalao.Models;

/// <summary>
/// Tipo de operação realizada no agendamento para envio remoto.
/// </summary>
public enum TipoOperacaoSincronizacao
{
    Criar = 1,
    Atualizar = 2,
    Excluir = 3
}

/// <summary>
/// Estado de processamento da operação na fila offline SQLite.
/// </summary>
public enum EstadoProcessamentoOperacao
{
    Pendente = 0,
    Processando = 1,
    Concluido = 2,
    Falha = 3,
    Conflito = 4
}

/// <summary>
/// Representa uma operação pendente de sincronização persistida no SQLite (estratégia offline-first).
/// </summary>
public class OperacaoSincronizacao
{
    public int Id { get; set; }

    /// <summary>
    /// ID do agendamento local correspondente. Pode ser nulo se o agendamento já foi removido localmente.
    /// </summary>
    public int? AgendamentoId { get; set; }

    /// <summary>
    /// Identificador único remoto (UID CalDAV / iCloud) associado ao evento.
    /// </summary>
    public string? ICloudEventId { get; set; }

    /// <summary>
    /// Tipo de alteração (Criar, Atualizar, Excluir).
    /// </summary>
    public TipoOperacaoSincronizacao TipoOperacao { get; set; }

    /// <summary>
    /// Versão local do evento no momento em que a alteração ocorreu.
    /// </summary>
    public int VersaoLocal { get; set; } = 1;

    /// <summary>
    /// Data de criação do registro na fila de pendências.
    /// </summary>
    public DateTime CriadoEm { get; set; } = DateTime.Now;

    /// <summary>
    /// Data da última tentativa ou atualização da operação pendente.
    /// </summary>
    public DateTime? AtualizadoEm { get; set; }

    /// <summary>
    /// Estado atual de processamento na fila.
    /// </summary>
    public EstadoProcessamentoOperacao Estado { get; set; } = EstadoProcessamentoOperacao.Pendente;

    /// <summary>
    /// Quantidade de tentativas de envio executadas.
    /// </summary>
    public int Tentativas { get; set; }

    /// <summary>
    /// Mensagem do último erro ocorrido ao tentar sincronizar.
    /// </summary>
    public string? UltimoErro { get; set; }

    /// <summary>
    /// Snapshot serializado em JSON com os dados completos do evento (cliente, horários, serviços, observação).
    /// Permite consolidar com segurança e enviar mesmo que o registro local sofra novas alterações.
    /// </summary>
    public string? PayloadJson { get; set; }
}
