namespace GerenciadorSalao.Models;

/// <summary>
/// Representa os estados possíveis de sincronização com o calendário remoto (ex.: iCloud / iPhone).
/// </summary>
public enum EstadoSincronizacao
{
    /// <summary>
    /// Todas as alterações relevantes foram confirmadas pelo servidor remoto.
    /// </summary>
    Sincronizado = 0,

    /// <summary>
    /// O serviço remoto está inacessível ou não há conectividade.
    /// </summary>
    Offline = 1,

    /// <summary>
    /// Existem alterações locais aguardando envio na fila SQLite.
    /// </summary>
    Pendente = 2,

    /// <summary>
    /// Há operações de sincronização em andamento no momento.
    /// </summary>
    Sincronizando = 3,

    /// <summary>
    /// Existem alterações incompatíveis (conflito entre local e remoto) que precisam de resolução.
    /// </summary>
    Conflito = 4,

    /// <summary>
    /// A conexão existe, mas as credenciais (Apple ID ou Senha de App) precisam ser informadas ou corrigidas.
    /// </summary>
    AutenticacaoNecessaria = 5,

    /// <summary>
    /// Uma operação falhou e precisa de nova tentativa ou intervenção técnica.
    /// </summary>
    ErroSincronizacao = 6
}
