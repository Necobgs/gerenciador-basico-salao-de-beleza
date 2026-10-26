namespace GerenciadorSalao.Models.DTOs;

/// <summary>
/// Resultado detalhado de um ciclo completo de sincronização com o iPhone / iCloud.
/// </summary>
public class ICloudSyncResultadoDto
{
    public bool Sucesso { get; set; }
    public EstadoSincronizacao EstadoFinal { get; set; } = EstadoSincronizacao.Offline;
    public int EventosEnviados { get; set; }
    public int EventosImportados { get; set; }
    public int EventosAtualizados { get; set; }
    public int EventosCancelados { get; set; }
    public int ConflitosIdentificados { get; set; }
    public int OperacoesPendentesRestantes { get; set; }
    public string Mensagem { get; set; } = string.Empty;
}
