namespace GerenciadorSalao.Models.DTOs;

/// <summary>
/// Resposta de uma operação individual de escrita (Criar, Atualizar, Excluir) no provedor iCloud.
/// </summary>
public class ICloudRespostaOperacao
{
    public bool Sucesso { get; set; }
    public string? Uid { get; set; }
    public string? ETag { get; set; }
    public string? Url { get; set; }
    public int CodigoStatusHttp { get; set; }
    public string? MensagemErro { get; set; }

    /// <summary>
    /// Indica se o servidor confirmou a gravação do registro antes da perda de conexão.
    /// Útil para testes de idempotência e simulação de resposta perdida.
    /// </summary>
    public bool ServidorGravouComSucesso { get; set; }

    public static ICloudRespostaOperacao CriarSucesso(string uid, string? etag = null, string? url = null, int statusHttp = 200)
    {
        return new ICloudRespostaOperacao
        {
            Sucesso = true,
            Uid = uid,
            ETag = etag ?? $"\"{DateTime.UtcNow.Ticks}\"",
            Url = url,
            CodigoStatusHttp = statusHttp,
            ServidorGravouComSucesso = true
        };
    }

    public static ICloudRespostaOperacao CriarFalha(string mensagemErro, int statusHttp = 500, bool servidorGravou = false)
    {
        return new ICloudRespostaOperacao
        {
            Sucesso = false,
            MensagemErro = mensagemErro,
            CodigoStatusHttp = statusHttp,
            ServidorGravouComSucesso = servidorGravou
        };
    }
}
