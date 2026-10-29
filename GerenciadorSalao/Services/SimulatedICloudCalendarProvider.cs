using System.Diagnostics;
using System.Net.Http;
using GerenciadorSalao.Models.DTOs;

namespace GerenciadorSalao.Services;

/// <summary>
/// Provedor simulado do servidor iCloud CalDAV para testes automatizados e validação de cenários de contingência.
/// Permite simular quedas de conexão, perdas de pacotes durante gravação, edições remotas concorrentes pelo iPhone e validação de idempotência.
/// </summary>
public class SimulatedICloudCalendarProvider : IICloudCalendarProvider
{
    private readonly Dictionary<string, ICloudEventoRemoto> _eventosRemotos = new(StringComparer.OrdinalIgnoreCase);
    private long _versaoEtag = 1000;

    public bool IsOnline { get; set; } = true;
    public bool SimularFalhaAutenticacao { get; set; } = false;
    public List<ICloudEventoRemoto>? EventosSobrescritos { get; set; }

    /// <summary>
    /// Quando verdadeiro, simula o cenário crítico: o servidor salva a alteração no disco,
    /// mas a conexão cai antes de enviar o HTTP 200/201 ao aplicativo cliente.
    /// </summary>
    public bool SimularQuedaConexaoAposGravacao { get; set; } = false;

    public string? EmailConfigurado { get; private set; }
    public string? SenhaAppConfigurada { get; private set; }

    public void ConfigurarCredenciais(string email, string senhaApp)
    {
        EmailConfigurado = email;
        SenhaAppConfigurada = senhaApp;
    }

    public Task<bool> VerificarDisponibilidadeAsync(CancellationToken cancellationToken = default)
    {
        if (!IsOnline)
            return Task.FromResult(false);

        return Task.FromResult(true);
    }

    public Task<bool> ValidarCredenciaisAsync(string email, string senhaApp, CancellationToken cancellationToken = default)
    {
        if (!IsOnline)
            return Task.FromResult(false);

        if (SimularFalhaAutenticacao)
            return Task.FromResult(false);

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(senhaApp))
            return Task.FromResult(false);

        // Validação simples: senha precisa ter ao menos 8 caracteres (formato xxxx-xxxx-xxxx-xxxx)
        return Task.FromResult(email.Contains('@') && senhaApp.Length >= 8);
    }

    public Task<List<ICloudEventoRemoto>> ObterEventosAsync(DateTime inicio, DateTime fim, CancellationToken cancellationToken = default)
    {
        if (!IsOnline)
            throw new HttpRequestException("Falha na conexão com caldav.icloud.com: Host inacessível (Offline).");

        if (SimularFalhaAutenticacao)
            throw new HttpRequestException("HTTP 401 Unauthorized: Credenciais inválidas para o Apple ID.");

        if (EventosSobrescritos != null)
        {
            return Task.FromResult(EventosSobrescritos.Select(ClonarEvento).ToList());
        }

        lock (_eventosRemotos)
        {
            var resultado = _eventosRemotos.Values
                .Where(e => e.DataHoraInicio >= inicio && e.DataHoraInicio <= fim)
                .Select(ClonarEvento)
                .ToList();

            return Task.FromResult(resultado);
        }
    }

    public Task<ICloudEventoRemoto?> ObterEventoPorUidAsync(string uid, CancellationToken cancellationToken = default)
    {
        if (!IsOnline)
            throw new HttpRequestException("Falha na conexão com caldav.icloud.com: Host inacessível (Offline).");

        lock (_eventosRemotos)
        {
            if (_eventosRemotos.TryGetValue(uid, out var evento))
            {
                return Task.FromResult<ICloudEventoRemoto?>(ClonarEvento(evento));
            }

            return Task.FromResult<ICloudEventoRemoto?>(null);
        }
    }

    public Task<ICloudRespostaOperacao> CriarEventoAsync(ICloudEventoRemoto evento, CancellationToken cancellationToken = default)
    {
        if (!IsOnline)
            throw new HttpRequestException("Falha de conexão durante envio (Offline).");

        if (SimularFalhaAutenticacao)
            return Task.FromResult(ICloudRespostaOperacao.CriarFalha("HTTP 401 Unauthorized", 401));

        lock (_eventosRemotos)
        {
            var novoEtag = GerarNovoEtag();
            var gravado = ClonarEvento(evento);
            gravado.ETag = novoEtag;
            gravado.Url = $"/calendars/home/{evento.Uid}.ics";
            gravado.UltimaModificacao = DateTime.UtcNow;

            _eventosRemotos[evento.Uid] = gravado;

            // Simulação de perda de conexão após a gravação remota no servidor
            if (SimularQuedaConexaoAposGravacao)
            {
                SimularQuedaConexaoAposGravacao = false; // desativa para a próxima
                throw new HttpRequestException("Conexão interrompida antes de receber resposta HTTP do servidor (timeout/socket reset).");
            }

            return Task.FromResult(ICloudRespostaOperacao.CriarSucesso(evento.Uid, novoEtag, gravado.Url, 201));
        }
    }

    public Task<ICloudRespostaOperacao> AtualizarEventoAsync(ICloudEventoRemoto evento, string? ifMatchEtag = null, CancellationToken cancellationToken = default)
    {
        if (!IsOnline)
            throw new HttpRequestException("Falha de conexão durante envio (Offline).");

        if (SimularFalhaAutenticacao)
            return Task.FromResult(ICloudRespostaOperacao.CriarFalha("HTTP 401 Unauthorized", 401));

        lock (_eventosRemotos)
        {
            if (_eventosRemotos.TryGetValue(evento.Uid, out var existente))
            {
                // Se foi especificado If-Match e o ETag remoto mudou, o servidor CalDAV retorna 412 Precondition Failed
                if (!string.IsNullOrEmpty(ifMatchEtag) && existente.ETag != ifMatchEtag)
                {
                    return Task.FromResult(ICloudRespostaOperacao.CriarFalha($"HTTP 412 Precondition Failed: O evento foi alterado no iPhone (ETag remoto {existente.ETag} diferente do esperado {ifMatchEtag}).", 412));
                }

                var novoEtag = GerarNovoEtag();
                var atualizado = ClonarEvento(evento);
                atualizado.ETag = novoEtag;
                atualizado.Url = existente.Url;
                atualizado.UltimaModificacao = DateTime.UtcNow;

                _eventosRemotos[evento.Uid] = atualizado;

                if (SimularQuedaConexaoAposGravacao)
                {
                    SimularQuedaConexaoAposGravacao = false;
                    throw new HttpRequestException("Conexão interrompida antes de receber resposta HTTP do servidor (timeout/socket reset).");
                }

                return Task.FromResult(ICloudRespostaOperacao.CriarSucesso(evento.Uid, novoEtag, atualizado.Url, 200));
            }

            // Se não existia remotamente, cria
            return CriarEventoAsync(evento, cancellationToken);
        }
    }

    public Task<ICloudRespostaOperacao> ExcluirEventoAsync(string uid, string? ifMatchEtag = null, CancellationToken cancellationToken = default)
    {
        if (!IsOnline)
            throw new HttpRequestException("Falha de conexão durante exclusão (Offline).");

        if (SimularFalhaAutenticacao)
            return Task.FromResult(ICloudRespostaOperacao.CriarFalha("HTTP 401 Unauthorized", 401));

        lock (_eventosRemotos)
        {
            if (_eventosRemotos.TryGetValue(uid, out var existente))
            {
                if (!string.IsNullOrEmpty(ifMatchEtag) && existente.ETag != ifMatchEtag)
                {
                    return Task.FromResult(ICloudRespostaOperacao.CriarFalha("HTTP 412 Precondition Failed: Conflito de versão ao excluir.", 412));
                }

                _eventosRemotos.Remove(uid);
                return Task.FromResult(ICloudRespostaOperacao.CriarSucesso(uid, null, null, 204));
            }

            // Já não existia no servidor: idempotência
            return Task.FromResult(ICloudRespostaOperacao.CriarSucesso(uid, null, null, 204));
        }
    }

    // ================= MÉTODOS DE SIMULAÇÃO DE AÇÕES NO IPHONE =================

    /// <summary>
    /// Simula o usuário abrindo o calendário do iPhone e editando o evento durante o período offline do Salão.
    /// </summary>
    public ICloudEventoRemoto SimularModificacaoNoIPhone(string uid, DateTime novaDataHora, string novoTitulo, string? novaDescricao = null)
    {
        lock (_eventosRemotos)
        {
            if (!_eventosRemotos.TryGetValue(uid, out var evento))
            {
                evento = new ICloudEventoRemoto
                {
                    Uid = uid,
                    DataHoraInicio = novaDataHora,
                    DataHoraFim = novaDataHora.AddMinutes(45),
                    Titulo = novoTitulo,
                    Descricao = novaDescricao
                };
            }
            else
            {
                var duracao = (evento.DataHoraFim - evento.DataHoraInicio).TotalMinutes;
                if (duracao <= 0) duracao = 45;
                evento.DataHoraInicio = novaDataHora;
                evento.DataHoraFim = novaDataHora.AddMinutes(duracao);
                evento.Titulo = novoTitulo;
                if (novaDescricao != null) evento.Descricao = novaDescricao;
            }

            evento.ETag = GerarNovoEtag();
            evento.UltimaModificacao = DateTime.UtcNow;
            _eventosRemotos[uid] = evento;

            return ClonarEvento(evento);
        }
    }

    /// <summary>
    /// Simula o usuário apagando um evento diretamente no iPhone.
    /// </summary>
    public void SimularExclusaoNoIPhone(string uid)
    {
        lock (_eventosRemotos)
        {
            if (_eventosRemotos.TryGetValue(uid, out var evento))
            {
                // Em CalDAV alguns servidores removem o arquivo, outros marcam STATUS:CANCELLED
                evento.IsCancelado = true;
                evento.ETag = GerarNovoEtag();
                evento.UltimaModificacao = DateTime.UtcNow;
            }
        }
    }

    /// <summary>
    /// Simula o usuário criando um novo compromisso na agenda do iPhone.
    /// </summary>
    public ICloudEventoRemoto SimularCriacaoNoIPhone(string titulo, DateTime inicio, int duracaoMinutos, string? descricao = null)
    {
        lock (_eventosRemotos)
        {
            var uid = $"iphone-{Guid.NewGuid():N}@icloud.com";
            var evento = new ICloudEventoRemoto
            {
                Uid = uid,
                Titulo = titulo,
                DataHoraInicio = inicio,
                DataHoraFim = inicio.AddMinutes(duracaoMinutos),
                Descricao = descricao,
                ETag = GerarNovoEtag(),
                Url = $"/calendars/home/{uid}.ics",
                UltimaModificacao = DateTime.UtcNow
            };

            _eventosRemotos[uid] = evento;
            return ClonarEvento(evento);
        }
    }

    public List<ICloudEventoRemoto> ObterTodosEventosRemotos()
    {
        lock (_eventosRemotos)
        {
            return _eventosRemotos.Values.Select(ClonarEvento).ToList();
        }
    }

    public void LimparTudo()
    {
        lock (_eventosRemotos)
        {
            _eventosRemotos.Clear();
            _versaoEtag = 1000;
        }
    }

    private string GerarNovoEtag()
    {
        var v = Interlocked.Increment(ref _versaoEtag);
        return $"\"etag-v{v}\"";
    }

    private static ICloudEventoRemoto ClonarEvento(ICloudEventoRemoto original)
    {
        return new ICloudEventoRemoto
        {
            Uid = original.Uid,
            ETag = original.ETag,
            Url = original.Url,
            Titulo = original.Titulo,
            Descricao = original.Descricao,
            DataHoraInicio = original.DataHoraInicio,
            DataHoraFim = original.DataHoraFim,
            NomeCliente = original.NomeCliente,
            TelefoneCliente = original.TelefoneCliente,
            IsCancelado = original.IsCancelado,
            UltimaModificacao = original.UltimaModificacao,
            RawIcs = original.RawIcs
        };
    }
}
