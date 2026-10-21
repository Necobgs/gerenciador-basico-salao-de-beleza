namespace GerenciadorSalao.Helpers;

/// <summary>
/// Contrato unificado para ViewModels que gerenciam modais na interface.
/// Permite à MainWindow bloquear a sidebar e fechar modais ao clicar fora.
/// </summary>
public interface IModalHost
{
    /// <summary>
    /// Indica se algum modal está ativo/visível na tela atual.
    /// </summary>
    bool IsModalAberto { get; }

    /// <summary>
    /// Fecha o modal atualmente aberto.
    /// </summary>
    void FecharModalAtivo();
}
