using System.Diagnostics;
using System.IO;
using System.Net.NetworkInformation;
using System.Text.Json;
using GerenciadorSalao.Data;
using GerenciadorSalao.Models;
using GerenciadorSalao.Models.DTOs;
using Microsoft.EntityFrameworkCore;

namespace GerenciadorSalao.Services;

/// <summary>
/// Implementação do serviço de sincronização offline-first entre SQLite e iPhone / iCloud Calendar.
/// Gerencia fila local, detecção de conectividade, retry exponencial com jitter, resolução de conflitos e idempotência.
/// </summary>
public class ICloudSyncService : IICloudSyncService, IDisposable
{
    private readonly Func<AppDbContext>? _contextFactory;
    private readonly AppDbContext? _injectedContext;
    private IICloudCalendarProvider _provider;
    private readonly SemaphoreSlim _syncLock = new(1, 1);
    private readonly Random _random = new();

    private CancellationTokenSource? _ctsMonitoramento;

    // Controle de Backoff e Retry
    private double _intervaloAtualSegundos = 5.0;
    private const double IntervaloInicialSegundos = 5.0;
    private const double IntervaloMaximoSegundos = 300.0; // 5 minutos

    private readonly string? _customConfigPath;

    public EstadoSincronizacao EstadoAtual { get; private set; } = EstadoSincronizacao.Offline;
    public DateTime? UltimaSincronizacaoSucesso { get; private set; }
    public int OperacoesPendentesCount { get; private set; }
    public int ConflitosCount { get; private set; }
    public string? UltimoErro { get; private set; }

    public event EventHandler<EstadoSincronizacao>? EstadoMudou;

    public ICloudSyncService(AppDbContext context, IICloudCalendarProvider? provider = null, string? customConfigPath = null)
    {
        _injectedContext = context;
        _provider = provider ?? new ICloudCalDavProvider();
        _customConfigPath = customConfigPath;
        CarregarConfiguracaoLocal();
    }

    public ICloudSyncService(Func<AppDbContext> contextFactory, IICloudCalendarProvider? provider = null, string? customConfigPath = null)
    {
        _contextFactory = contextFactory;
        _provider = provider ?? new ICloudCalDavProvider();
        _customConfigPath = customConfigPath;
        CarregarConfiguracaoLocal();
    }

    private AppDbContext CriarContexto()
    {
        if (_contextFactory != null)
            return _contextFactory();

        return new AppDbContext();
    }

    public void SetProvider(IICloudCalendarProvider provider)
    {
        _provider = provider;
    }

    public IICloudCalendarProvider ObterProviderAtual() => _provider;

    private string ObterCaminhoConfiguracao()
    {
        if (!string.IsNullOrEmpty(_customConfigPath))
        {
            var dir = Path.GetDirectoryName(_customConfigPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);
            return _customConfigPath;
        }

        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var pastaApp = Path.Combine(localAppData, "GerenciadorSalao");
        if (!Directory.Exists(pastaApp))
            Directory.CreateDirectory(pastaApp);

        return Path.Combine(pastaApp, "icloud_settings.json");
    }

    private void CarregarConfiguracaoLocal()
    {
        try
        {
            var caminho = ObterCaminhoConfiguracao();
            if (File.Exists(caminho))
            {
                var json = File.ReadAllText(caminho);
                var config = JsonSerializer.Deserialize<ICloudConfiguracaoDto>(json);
                if (config != null)
                {
                    _provider.ConfigurarCredenciais(config.AppleIdEmail, config.SenhaApp);
                    UltimaSincronizacaoSucesso = config.UltimaSincronizacaoSucesso;
                    if (config.EstaAtivo)
                    {
                        EstadoAtual = EstadoSincronizacao.Pendente;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ICloudSync] Erro ao carregar configurações: {ex.Message}");
        }
    }

    public async Task<ICloudConfiguracaoDto> ObterConfiguracaoAsync()
    {
        var caminho = ObterCaminhoConfiguracao();
        ICloudConfiguracaoDto? config = null;

        if (File.Exists(caminho))
        {
            try
            {
                var json = await File.ReadAllTextAsync(caminho);
                config = JsonSerializer.Deserialize<ICloudConfiguracaoDto>(json);
            }
            catch { }
        }

        config ??= new ICloudConfiguracaoDto();

        using var context = CriarContexto();
        config.OperacoesPendentesCount = await context.OperacoesSincronizacao
            .CountAsync(o => o.Estado == EstadoProcessamentoOperacao.Pendente);

        config.ConflitosCount = await context.ConflitosSincronizacao
            .CountAsync(c => !c.Resolvido);

        config.Estado = EstadoAtual;
        config.UltimaSincronizacaoSucesso = UltimaSincronizacaoSucesso;
        config.UltimoErro = UltimoErro;

        return config;
    }

    public async Task SalvarConfiguracaoAsync(string email, string senhaApp, bool ativar, bool dispararSincronizacao = true)
    {
        var config = new ICloudConfiguracaoDto
        {
            AppleIdEmail = email.Trim(),
            SenhaApp = senhaApp.Trim(),
            EstaAtivo = ativar,
            UltimaSincronizacaoSucesso = UltimaSincronizacaoSucesso,
            Estado = ativar ? EstadoSincronizacao.Pendente : EstadoSincronizacao.Offline
        };

        var json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(ObterCaminhoConfiguracao(), json);

        _provider.ConfigurarCredenciais(email, senhaApp);
        ResetarBackoff();

        if (ativar)
        {
            AtualizarEstado(EstadoSincronizacao.Pendente);
            if (dispararSincronizacao)
            {
                DispararSincronizacaoEmSegundoPlano();
            }
        }
        else
        {
            AtualizarEstado(EstadoSincronizacao.Offline);
        }
    }

    public async Task DesconectarAsync()
    {
        var caminho = ObterCaminhoConfiguracao();
        if (File.Exists(caminho))
        {
            File.Delete(caminho);
        }

        _provider.ConfigurarCredenciais(string.Empty, string.Empty);
        UltimaSincronizacaoSucesso = null;
        UltimoErro = null;
        AtualizarEstado(EstadoSincronizacao.Offline);
    }

    // ================= ENFILEIRAMENTO OFFLINE-FIRST NO SQLITE =================

    public async Task EnfileirarOperacaoAsync(int? agendamentoId, string? iCloudEventId, TipoOperacaoSincronizacao tipo, Agendamento? agendamentoSnapshot)
    {
        try
        {
            using var context = CriarContexto();

            // Gera UID estável se ainda não existir
            if (string.IsNullOrEmpty(iCloudEventId) && agendamentoSnapshot != null)
            {
                if (!string.IsNullOrEmpty(agendamentoSnapshot.ICloudEventId))
                {
                    iCloudEventId = agendamentoSnapshot.ICloudEventId;
                }
                else
                {
                    iCloudEventId = $"salao-{agendamentoSnapshot.Id}-{Guid.NewGuid():N}@gerenciadorsalao.app";
                    agendamentoSnapshot.ICloudEventId = iCloudEventId;
                }
            }

            // Snapshot serializado para payload
            string? payloadJson = null;
            if (agendamentoSnapshot != null)
            {
                var payloadDto = new
                {
                    agendamentoSnapshot.Id,
                    agendamentoSnapshot.ClienteId,
                    NomeCliente = agendamentoSnapshot.Cliente != null ? agendamentoSnapshot.Cliente.NomeExibicao : "Cliente",
                    TelefoneCliente = agendamentoSnapshot.Cliente?.Telefone,
                    agendamentoSnapshot.DataHora,
                    agendamentoSnapshot.TempoTotalPrevistoMinutos,
                    agendamentoSnapshot.Observacao,
                    agendamentoSnapshot.Status,
                    Servicos = agendamentoSnapshot.AgendamentoServicos?.Select(asv => new
                    {
                        asv.ServicoId,
                        Descricao = asv.Servico?.NomeExibicao ?? "Serviço",
                        Valor = asv.Servico?.Valor ?? 0m
                    }).ToList()
                };
                payloadJson = JsonSerializer.Serialize(payloadDto);
            }

            // Regra de consolidação de operações pendentes:
            var opPendente = await context.OperacoesSincronizacao
                .FirstOrDefaultAsync(o => o.Estado == EstadoProcessamentoOperacao.Pendente &&
                                          ((agendamentoId.HasValue && o.AgendamentoId == agendamentoId.Value) ||
                                           (!string.IsNullOrEmpty(iCloudEventId) && o.ICloudEventId == iCloudEventId)));

            if (opPendente != null)
            {
                if (opPendente.TipoOperacao == TipoOperacaoSincronizacao.Criar)
                {
                    if (tipo == TipoOperacaoSincronizacao.Atualizar)
                    {
                        // Edição após criação offline: atualiza payload e versão mantendo Criar
                        opPendente.PayloadJson = payloadJson;
                        opPendente.VersaoLocal = agendamentoSnapshot?.VersaoLocal ?? (opPendente.VersaoLocal + 1);
                        opPendente.AtualizadoEm = DateTime.Now;
                    }
                    else if (tipo == TipoOperacaoSincronizacao.Excluir)
                    {
                        // Criado offline e excluído antes de enviar remotamente: remove da fila!
                        if (string.IsNullOrEmpty(agendamentoSnapshot?.ICloudETag))
                        {
                            context.OperacoesSincronizacao.Remove(opPendente);
                        }
                        else
                        {
                            opPendente.TipoOperacao = TipoOperacaoSincronizacao.Excluir;
                            opPendente.AtualizadoEm = DateTime.Now;
                        }
                    }
                }
                else if (opPendente.TipoOperacao == TipoOperacaoSincronizacao.Atualizar)
                {
                    if (tipo == TipoOperacaoSincronizacao.Atualizar)
                    {
                        opPendente.PayloadJson = payloadJson;
                        opPendente.VersaoLocal = agendamentoSnapshot?.VersaoLocal ?? (opPendente.VersaoLocal + 1);
                        opPendente.AtualizadoEm = DateTime.Now;
                    }
                    else if (tipo == TipoOperacaoSincronizacao.Excluir)
                    {
                        opPendente.TipoOperacao = TipoOperacaoSincronizacao.Excluir;
                        opPendente.AtualizadoEm = DateTime.Now;
                    }
                }
            }
            else
            {
                var novaOp = new OperacaoSincronizacao
                {
                    AgendamentoId = agendamentoId,
                    ICloudEventId = iCloudEventId,
                    TipoOperacao = tipo,
                    VersaoLocal = agendamentoSnapshot?.VersaoLocal ?? 1,
                    CriadoEm = DateTime.Now,
                    Estado = EstadoProcessamentoOperacao.Pendente,
                    PayloadJson = payloadJson
                };
                context.OperacoesSincronizacao.Add(novaOp);
            }

            await context.SaveChangesAsync();
            await AtualizarContadoresAsync();

            if (EstadoAtual == EstadoSincronizacao.Sincronizado)
            {
                AtualizarEstado(EstadoSincronizacao.Pendente);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ICloudSync] Erro ao enfileirar operação offline: {ex.Message}");
        }
    }

    // ================= CICLO COMPLETO DE RECUPERAÇÃO E SINCRONIZAÇÃO (10 PASSOS) =================

    public async Task<ICloudSyncResultadoDto> SincronizarAgoraAsync(CancellationToken cancellationToken = default)
    {
        var resultado = new ICloudSyncResultadoDto();

        // Controle de concorrência: aguarda até 5 segundos caso haja outra operação em curso
        if (!await _syncLock.WaitAsync(TimeSpan.FromSeconds(5), cancellationToken))
        {
            resultado.Sucesso = false;
            resultado.EstadoFinal = EstadoAtual;
            resultado.Mensagem = "Sincronização já em andamento.";
            return resultado;
        }

        try
        {
            AtualizarEstado(EstadoSincronizacao.Sincronizando);

            // PASSO 1: Verificar a disponibilidade do serviço remoto
            bool disponivel = await _provider.VerificarDisponibilidadeAsync(cancellationToken);
            if (!disponivel)
            {
                resultado.Sucesso = false;
                resultado.EstadoFinal = EstadoSincronizacao.Offline;
                resultado.Mensagem = "Serviço do iCloud inacessível ou sem conexão.";
                AtualizarEstado(EstadoSincronizacao.Offline, resultado.Mensagem);
                CalcularProximoBackoff();
                return resultado;
            }

            // PASSO 2: Validar a autenticação
            var config = await ObterConfiguracaoAsync();
            if (string.IsNullOrWhiteSpace(config.AppleIdEmail) || string.IsNullOrWhiteSpace(config.SenhaApp))
            {
                resultado.Sucesso = false;
                resultado.EstadoFinal = EstadoSincronizacao.AutenticacaoNecessaria;
                resultado.Mensagem = "Credenciais do iCloud não configuradas.";
                AtualizarEstado(EstadoSincronizacao.AutenticacaoNecessaria, resultado.Mensagem);
                return resultado;
            }

            bool credenciaisValidas = await _provider.ValidarCredenciaisAsync(config.AppleIdEmail, config.SenhaApp, cancellationToken);
            if (!credenciaisValidas)
            {
                resultado.Sucesso = false;
                resultado.EstadoFinal = EstadoSincronizacao.AutenticacaoNecessaria;
                resultado.Mensagem = "Credenciais do Apple ID ou Senha de App inválidas ou expiradas.";
                AtualizarEstado(EstadoSincronizacao.AutenticacaoNecessaria, resultado.Mensagem);
                return resultado;
            }

            // PASSO 3: Consultar alterações remotas ocorridas durante o período offline
            var inicioJanela = DateTime.Today.AddDays(-60);
            var fimJanela = DateTime.Today.AddDays(90);
            List<ICloudEventoRemoto> eventosRemotos;
            try
            {
                eventosRemotos = await _provider.ObterEventosAsync(inicioJanela, fimJanela, cancellationToken);
            }
            catch (Exception ex)
            {
                resultado.Sucesso = false;
                resultado.EstadoFinal = EstadoSincronizacao.Offline;
                resultado.Mensagem = $"Falha ao consultar servidor iCloud: {ex.Message}";
                AtualizarEstado(EstadoSincronizacao.Offline, resultado.Mensagem);
                CalcularProximoBackoff();
                return resultado;
            }

            using var context = CriarContexto();

            // PASSO 4: Comparar o estado remoto com a última versão sincronizada e alterações locais pendentes
            var agendamentosLocais = await context.Agendamentos
                .Include(a => a.Cliente)
                .Include(a => a.Comanda)
                .Include(a => a.AgendamentoServicos)
                    .ThenInclude(asv => asv.Servico)
                .Where(a => a.DataHora >= inicioJanela && a.DataHora <= fimJanela)
                .ToListAsync(cancellationToken);

            var operacoesPendentes = await context.OperacoesSincronizacao
                .Where(o => o.Estado == EstadoProcessamentoOperacao.Pendente)
                .OrderBy(o => o.CriadoEm)
                .ToListAsync(cancellationToken);

            var localPorUid = agendamentosLocais
                .Where(a => !string.IsNullOrEmpty(a.ICloudEventId))
                .ToDictionary(a => a.ICloudEventId!, a => a, StringComparer.OrdinalIgnoreCase);

            var pendingPorUid = operacoesPendentes
                .Where(o => !string.IsNullOrEmpty(o.ICloudEventId))
                .ToDictionary(o => o.ICloudEventId!, o => o, StringComparer.OrdinalIgnoreCase);

            var remotoPorUid = eventosRemotos
                .ToDictionary(e => e.Uid, e => e, StringComparer.OrdinalIgnoreCase);

            // PASSO 5: Identificar conflitos antes de enviar alterações
            // PASSO 6: Resolver automaticamente apenas os casos sem ambiguidade
            foreach (var remoto in eventosRemotos)
            {
                bool temLocal = localPorUid.TryGetValue(remoto.Uid, out var local);
                bool temPendente = pendingPorUid.TryGetValue(remoto.Uid, out var pendente);

                if (temLocal && temPendente && pendente != null)
                {
                    // Ambos foram modificados offline!
                    bool remotoMudou = !string.IsNullOrEmpty(remoto.ETag) && remoto.ETag != local!.ICloudETag;
                    bool remotoCancelou = remoto.IsCancelado && local!.Status != StatusAgendamento.Cancelado;
                    bool localCancelou = !remoto.IsCancelado && local!.Status == StatusAgendamento.Cancelado;

                    if (remotoMudou || remotoCancelou || localCancelou)
                    {
                        bool datasIguais = Math.Abs((local!.DataHora - remoto.DataHoraInicio).TotalMinutes) < 1;
                        bool titulosIguais = string.Equals(local.Cliente?.NomeExibicao, remoto.NomeCliente ?? remoto.Titulo, StringComparison.OrdinalIgnoreCase);
                        bool statusIguais = (remoto.IsCancelado && local.Status == StatusAgendamento.Cancelado) ||
                                            (!remoto.IsCancelado && local.Status != StatusAgendamento.Cancelado);

                        if (datasIguais && titulosIguais && statusIguais)
                        {
                            // Resolução automática sem ambiguidade
                            local.ICloudETag = remoto.ETag;
                            local.SincronizadoComICloud = true;
                            local.UltimaSincronizacaoICloud = DateTime.Now;
                            pendente.Estado = EstadoProcessamentoOperacao.Concluido;
                        }
                        else
                        {
                            // Conflito genuíno e divergente: preservar ambas as versões sem descarte silencioso
                            await RegistrarConflitoAsync(context, local, pendente, remoto);
                            pendente.Estado = EstadoProcessamentoOperacao.Conflito;
                            resultado.ConflitosIdentificados++;
                        }
                    }
                }
                else if (temLocal && !temPendente)
                {
                    // Apenas remoto mudou: aplicar atualização remota no SQLite (pull seguro)
                    if (remoto.IsCancelado)
                    {
                        if (local!.Status != StatusAgendamento.Cancelado)
                        {
                            local.Status = StatusAgendamento.Cancelado;
                            if (local.Comanda != null && local.Comanda.Status == StatusComanda.Aberta)
                            {
                                local.Comanda.Status = StatusComanda.Cancelada;
                            }
                            local.ICloudETag = remoto.ETag;
                            local.SincronizadoComICloud = true;
                            local.UltimaSincronizacaoICloud = DateTime.Now;
                            resultado.EventosCancelados++;
                        }
                    }
                    else
                    {
                        bool mudou = false;
                        if (Math.Abs((local!.DataHora - remoto.DataHoraInicio).TotalMinutes) >= 1)
                        {
                            local.DataHora = remoto.DataHoraInicio;
                            local.TempoTotalPrevistoMinutos = remoto.DuracaoMinutos;
                            if (local.Comanda != null && local.Comanda.Status == StatusComanda.Aberta)
                            {
                                local.Comanda.DataAbertura = remoto.DataHoraInicio;
                            }
                            mudou = true;
                        }

                        if (!string.IsNullOrWhiteSpace(remoto.Descricao) && local.Observacao != remoto.Descricao.Trim())
                        {
                            local.Observacao = remoto.Descricao.Trim();
                            mudou = true;
                        }

                        if (mudou || local.ICloudETag != remoto.ETag)
                        {
                            local.ICloudETag = remoto.ETag;
                            local.SincronizadoComICloud = true;
                            local.UltimaSincronizacaoICloud = DateTime.Now;
                            resultado.EventosAtualizados++;
                        }
                    }
                }
                else if (!temLocal && !temPendente && !remoto.IsCancelado)
                {
                    // Novo evento criado no iPhone durante período offline: importar para o SQLite
                    await ImportarNovoEventoRemotoAsync(context, remoto);
                    resultado.EventosImportados++;
                }
            }

            // PASSO 6b: Identificar exclusões remotas no iPhone
            // Se um agendamento foi sincronizado previamente com o iCloud e agora não existe mais no calendário remoto,
            // significa que o usuário apagou o evento diretamente no app Calendário do iPhone.
            foreach (var local in localPorUid.Values)
            {
                if (local.Status == StatusAgendamento.Cancelado)
                    continue;

                if (remotoPorUid.ContainsKey(local.ICloudEventId!))
                    continue;

                // Só considera exclusão remota se já havia sido confirmado e sincronizado com o iCloud
                if (!local.SincronizadoComICloud && string.IsNullOrEmpty(local.ICloudETag))
                    continue;

                bool temPendente = pendingPorUid.TryGetValue(local.ICloudEventId!, out var pendente);
                if (temPendente && pendente != null)
                {
                    if (pendente.TipoOperacao == TipoOperacaoSincronizacao.Excluir)
                    {
                        // Excluído em ambos os lados
                        local.Status = StatusAgendamento.Cancelado;
                        if (local.Comanda != null && local.Comanda.Status == StatusComanda.Aberta)
                        {
                            local.Comanda.Status = StatusComanda.Cancelada;
                        }
                        context.OperacoesSincronizacao.Remove(pendente);
                        resultado.EventosCancelados++;
                    }
                    else if (pendente.TipoOperacao == TipoOperacaoSincronizacao.Atualizar)
                    {
                        // Conflito: Exclusão no iPhone vs Edição local no Salão
                        await RegistrarConflitoAsync(context, local, pendente, new ICloudEventoRemoto
                        {
                            Uid = local.ICloudEventId!,
                            Titulo = local.Cliente?.NomeExibicao ?? "Agendamento",
                            DataHoraInicio = local.DataHora,
                            DataHoraFim = local.DataHora.AddMinutes(local.TempoTotalPrevistoMinutos),
                            IsCancelado = true
                        });
                        pendente.Estado = EstadoProcessamentoOperacao.Conflito;
                        resultado.ConflitosIdentificados++;
                    }
                }
                else
                {
                    // Exclusão remota legítima no iPhone: cancela o agendamento local e a comanda aberta vinculada
                    local.Status = StatusAgendamento.Cancelado;
                    if (local.Comanda != null && local.Comanda.Status == StatusComanda.Aberta)
                    {
                        local.Comanda.Status = StatusComanda.Cancelada;
                    }
                    local.SincronizadoComICloud = true;
                    local.UltimaSincronizacaoICloud = DateTime.Now;
                    resultado.EventosCancelados++;
                }
            }

            await context.SaveChangesAsync(cancellationToken);

            // PASSO 7: Enviar as operações locais pendentes
            // PASSO 8: Confirmar cada operação remota e persistir identificadores e ETags
            // PASSO 9: Atualizar os estados locais e remover da fila somente as confirmadas
            var operacoesProntasParaEnvio = await context.OperacoesSincronizacao
                .Where(o => o.Estado == EstadoProcessamentoOperacao.Pendente)
                .OrderBy(o => o.CriadoEm)
                .ToListAsync(cancellationToken);

            foreach (var op in operacoesProntasParaEnvio)
            {
                op.Estado = EstadoProcessamentoOperacao.Processando;
                op.Tentativas++;
                op.AtualizadoEm = DateTime.Now;

                try
                {
                    if (op.TipoOperacao == TipoOperacaoSincronizacao.Criar)
                    {
                        // Idempotência: verifica se o evento já existe remotamente com este UID
                        ICloudEventoRemoto? remotoJaExistente = null;
                        if (!string.IsNullOrEmpty(op.ICloudEventId))
                        {
                            if (remotoPorUid.TryGetValue(op.ICloudEventId, out var r))
                                remotoJaExistente = r;
                            else
                                remotoJaExistente = await _provider.ObterEventoPorUidAsync(op.ICloudEventId, cancellationToken);
                        }

                        if (remotoJaExistente != null)
                        {
                            if (op.AgendamentoId.HasValue)
                            {
                                var ag = await context.Agendamentos.FindAsync(new object[] { op.AgendamentoId.Value }, cancellationToken);
                                if (ag != null)
                                {
                                    ag.ICloudEventId = remotoJaExistente.Uid;
                                    ag.ICloudETag = remotoJaExistente.ETag;
                                    ag.SincronizadoComICloud = true;
                                    ag.UltimaSincronizacaoICloud = DateTime.Now;
                                }
                            }

                            context.OperacoesSincronizacao.Remove(op);
                            resultado.EventosEnviados++;
                            continue;
                        }

                        var agendamento = op.AgendamentoId.HasValue 
                            ? await context.Agendamentos
                                .Include(a => a.Cliente)
                                .Include(a => a.AgendamentoServicos).ThenInclude(asv => asv.Servico)
                                .FirstOrDefaultAsync(a => a.Id == op.AgendamentoId.Value, cancellationToken)
                            : null;

                        var evRemoto = MontarEventoRemoto(op, agendamento);
                        var resposta = await _provider.CriarEventoAsync(evRemoto, cancellationToken);

                        if (resposta.Sucesso)
                        {
                            if (agendamento != null)
                            {
                                agendamento.ICloudEventId = resposta.Uid ?? evRemoto.Uid;
                                agendamento.ICloudETag = resposta.ETag;
                                agendamento.ICloudUrl = resposta.Url;
                                agendamento.SincronizadoComICloud = true;
                                agendamento.UltimaSincronizacaoICloud = DateTime.Now;
                            }

                            context.OperacoesSincronizacao.Remove(op);
                            resultado.EventosEnviados++;
                        }
                        else
                        {
                            op.Estado = EstadoProcessamentoOperacao.Pendente;
                            op.UltimoErro = resposta.MensagemErro;
                        }
                    }
                    else if (op.TipoOperacao == TipoOperacaoSincronizacao.Atualizar)
                    {
                        var agendamento = op.AgendamentoId.HasValue 
                            ? await context.Agendamentos
                                .Include(a => a.Cliente)
                                .Include(a => a.AgendamentoServicos).ThenInclude(asv => asv.Servico)
                                .FirstOrDefaultAsync(a => a.Id == op.AgendamentoId.Value, cancellationToken)
                            : null;

                        var evRemoto = MontarEventoRemoto(op, agendamento);
                        var resposta = await _provider.AtualizarEventoAsync(evRemoto, agendamento?.ICloudETag, cancellationToken);

                        if (resposta.Sucesso)
                        {
                            if (agendamento != null)
                            {
                                agendamento.ICloudETag = resposta.ETag;
                                agendamento.SincronizadoComICloud = true;
                                agendamento.UltimaSincronizacaoICloud = DateTime.Now;
                            }

                            context.OperacoesSincronizacao.Remove(op);
                            resultado.EventosAtualizados++;
                        }
                        else if (resposta.CodigoStatusHttp == 412)
                        {
                            op.Estado = EstadoProcessamentoOperacao.Conflito;
                            op.UltimoErro = "Conflito detectado pelo servidor remoto (ETag divergente).";
                        }
                        else
                        {
                            op.Estado = EstadoProcessamentoOperacao.Pendente;
                            op.UltimoErro = resposta.MensagemErro;
                        }
                    }
                    else if (op.TipoOperacao == TipoOperacaoSincronizacao.Excluir)
                    {
                        if (!string.IsNullOrEmpty(op.ICloudEventId))
                        {
                            var resposta = await _provider.ExcluirEventoAsync(op.ICloudEventId, null, cancellationToken);
                            if (resposta.Sucesso)
                            {
                                context.OperacoesSincronizacao.Remove(op);
                                resultado.EventosCancelados++;
                            }
                            else
                            {
                                op.Estado = EstadoProcessamentoOperacao.Pendente;
                                op.UltimoErro = resposta.MensagemErro;
                            }
                        }
                        else
                        {
                            context.OperacoesSincronizacao.Remove(op);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[ICloudSync] Falha na operação {op.Id}: {ex.Message}");
                    op.Estado = EstadoProcessamentoOperacao.Pendente;
                    op.UltimoErro = ex.Message;
                }
            }

            await context.SaveChangesAsync(cancellationToken);

            // PASSO 10: Executar verificação final
            var restantes = await context.OperacoesSincronizacao
                .CountAsync(o => o.Estado == EstadoProcessamentoOperacao.Pendente, cancellationToken);

            var conflitosAtivos = await context.ConflitosSincronizacao
                .CountAsync(c => !c.Resolvido, cancellationToken);

            resultado.OperacoesPendentesRestantes = restantes;
            resultado.ConflitosIdentificados = conflitosAtivos;

            if (conflitosAtivos > 0)
            {
                resultado.Sucesso = true;
                resultado.EstadoFinal = EstadoSincronizacao.Conflito;
                resultado.Mensagem = $"Sincronização concluída com {conflitosAtivos} conflito(s) aguardando sua decisão.";
                AtualizarEstado(EstadoSincronizacao.Conflito, resultado.Mensagem);
            }
            else if (restantes > 0)
            {
                resultado.Sucesso = false;
                resultado.EstadoFinal = EstadoSincronizacao.Pendente;
                resultado.Mensagem = $"Sincronização parcial: {restantes} alteração(ões) pendentes para a próxima tentativa.";
                AtualizarEstado(EstadoSincronizacao.Pendente, resultado.Mensagem);
                CalcularProximoBackoff();
            }
            else
            {
                resultado.Sucesso = true;
                resultado.EstadoFinal = EstadoSincronizacao.Sincronizado;
                resultado.Mensagem = $"Sincronização com o iPhone concluída com sucesso! ({resultado.EventosEnviados} enviados, {resultado.EventosImportados} importados, {resultado.EventosAtualizados} atualizados, {resultado.EventosCancelados} cancelados).";
                UltimaSincronizacaoSucesso = DateTime.Now;
                ResetarBackoff();
                AtualizarEstado(EstadoSincronizacao.Sincronizado, resultado.Mensagem);
                await SalvarUltimaSincronizacaoEmDiscoAsync();

                // Agenda a próxima verificação periódica em segundo plano (polling a cada 30 segundos)
                AgendarProximaTentativa(TimeSpan.FromSeconds(30));
            }
        }
        catch (Exception ex)
        {
            resultado.Sucesso = false;
            resultado.EstadoFinal = EstadoSincronizacao.ErroSincronizacao;
            resultado.Mensagem = $"Erro inesperado durante a sincronização: {ex.Message}";
            AtualizarEstado(EstadoSincronizacao.ErroSincronizacao, resultado.Mensagem);
            CalcularProximoBackoff();
        }
        finally
        {
            await AtualizarContadoresAsync();
            _syncLock.Release();
        }

        return resultado;
    }

    private async Task SalvarUltimaSincronizacaoEmDiscoAsync()
    {
        try
        {
            var config = await ObterConfiguracaoAsync();
            config.UltimaSincronizacaoSucesso = UltimaSincronizacaoSucesso;
            var json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(ObterCaminhoConfiguracao(), json);
        }
        catch { }
    }

    private static ICloudEventoRemoto MontarEventoRemoto(OperacaoSincronizacao op, Agendamento? agendamento)
    {
        var uid = op.ICloudEventId ?? agendamento?.ICloudEventId ?? $"salao-{op.AgendamentoId}-{Guid.NewGuid():N}@gerenciadorsalao.app";
        string titulo;
        string? descricao = null;
        DateTime inicio;
        DateTime fim;

        if (agendamento != null)
        {
            var clienteNome = agendamento.Cliente != null ? agendamento.Cliente.NomeExibicao : "Cliente";
            titulo = $"Salão - {clienteNome}";
            inicio = agendamento.DataHora;
            var duracao = agendamento.TempoTotalPrevistoMinutos > 0 ? agendamento.TempoTotalPrevistoMinutos : 30;
            fim = inicio.AddMinutes(duracao);

            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"Cliente: {clienteNome}");
            if (!string.IsNullOrEmpty(agendamento.Cliente?.Telefone))
                sb.AppendLine($"Telefone: {agendamento.Cliente.Telefone}");
            if (!string.IsNullOrWhiteSpace(agendamento.Observacao))
                sb.AppendLine($"Observações: {agendamento.Observacao}");

            if (agendamento.AgendamentoServicos != null && agendamento.AgendamentoServicos.Count > 0)
            {
                sb.AppendLine("Serviços:");
                foreach (var s in agendamento.AgendamentoServicos)
                {
                    if (s.Servico != null)
                        sb.AppendLine($"- {s.Servico.NomeExibicao}");
                }
            }

            descricao = sb.ToString();
        }
        else if (!string.IsNullOrEmpty(op.PayloadJson))
        {
            using var doc = JsonDocument.Parse(op.PayloadJson);
            var root = doc.RootElement;
            var nome = root.TryGetProperty("NomeCliente", out var n) ? n.GetString() ?? "Cliente" : "Cliente";
            titulo = $"Salão - {nome}";
            inicio = root.TryGetProperty("DataHora", out var d) ? d.GetDateTime() : DateTime.Today.AddHours(9);
            var duracao = root.TryGetProperty("TempoTotalPrevistoMinutos", out var dur) ? dur.GetInt32() : 30;
            fim = inicio.AddMinutes(duracao);
            descricao = root.TryGetProperty("Observacao", out var obs) ? obs.GetString() : null;
        }
        else
        {
            titulo = "Agendamento Salão";
            inicio = DateTime.Today.AddHours(9);
            fim = inicio.AddMinutes(30);
        }

        return new ICloudEventoRemoto
        {
            Uid = uid,
            Titulo = titulo,
            Descricao = descricao,
            DataHoraInicio = inicio,
            DataHoraFim = fim
        };
    }

    private static async Task ImportarNovoEventoRemotoAsync(AppDbContext context, ICloudEventoRemoto remoto)
    {
        var titulo = (remoto.Titulo ?? string.Empty).Trim();
        string nomeCliente = titulo;
        if (titulo.StartsWith("Salão - ", StringComparison.OrdinalIgnoreCase))
            nomeCliente = titulo.Substring("Salão - ".Length).Trim();
        else if (titulo.StartsWith("Salao - ", StringComparison.OrdinalIgnoreCase))
            nomeCliente = titulo.Substring("Salao - ".Length).Trim();

        if (string.IsNullOrWhiteSpace(nomeCliente))
            nomeCliente = "Cliente iPhone";

        var cliente = await context.Clientes
            .FirstOrDefaultAsync(c => c.Nome.ToLower() == nomeCliente.ToLower());

        if (cliente == null)
        {
            cliente = new Cliente
            {
                Nome = nomeCliente,
                Observacao = "Importado via Calendário do iPhone",
                DataCadastro = DateTime.Now
            };
            context.Clientes.Add(cliente);
            await context.SaveChangesAsync();
        }

        var servico = await context.Servicos.FirstOrDefaultAsync();
        if (servico == null)
        {
            servico = new Servico
            {
                Descricao = "Atendimento Geral",
                Valor = 0m,
                DuracaoMinutos = remoto.DuracaoMinutos,
                Ativo = true
            };
            context.Servicos.Add(servico);
            await context.SaveChangesAsync();
        }

        var novoAgendamento = new Agendamento
        {
            ClienteId = cliente.Id,
            Cliente = cliente,
            DataHora = remoto.DataHoraInicio,
            TempoTotalPrevistoMinutos = remoto.DuracaoMinutos,
            Status = StatusAgendamento.Pendente,
            Observacao = remoto.Descricao?.Trim(),
            ICloudEventId = remoto.Uid,
            ICloudETag = remoto.ETag,
            ICloudUrl = remoto.Url,
            SincronizadoComICloud = true,
            UltimaSincronizacaoICloud = DateTime.Now,
            VersaoLocal = 1
        };

        novoAgendamento.AgendamentoServicos.Add(new AgendamentoServico
        {
            Agendamento = novoAgendamento,
            ServicoId = servico.Id,
            Servico = servico
        });

        var novaComanda = new Comanda
        {
            ClienteId = cliente.Id,
            DataAbertura = remoto.DataHoraInicio,
            Status = StatusComanda.Aberta,
            Observacao = remoto.Descricao?.Trim(),
            Agendamento = novoAgendamento
        };

        novaComanda.Itens.Add(new ComandaItem
        {
            Comanda = novaComanda,
            Tipo = TipoItemComanda.Servico,
            ServicoId = servico.Id,
            Descricao = servico.NomeExibicao,
            Quantidade = 1,
            ValorUnitario = servico.Valor
        });

        novaComanda.ValorTotal = novaComanda.CalcularTotalItens();
        novoAgendamento.Comanda = novaComanda;

        context.Agendamentos.Add(novoAgendamento);
        context.Comandas.Add(novaComanda);
    }

    private static async Task RegistrarConflitoAsync(AppDbContext context, Agendamento local, OperacaoSincronizacao op, ICloudEventoRemoto remoto)
    {
        var conflito = new ConflitoSincronizacao
        {
            AgendamentoId = local.Id,
            ICloudEventId = remoto.Uid,
            Titulo = $"Conflito em '{local.Cliente?.NomeExibicao ?? "Agendamento"}'",
            Descricao = $"O evento foi modificado no Salão para {local.DataHora:dd/MM/yyyy HH:mm} e simultaneamente no iPhone para {remoto.DataHoraInicio:dd/MM/yyyy HH:mm}.",
            DadosLocaisJson = JsonSerializer.Serialize(new
            {
                local.Id,
                NomeCliente = local.Cliente?.NomeExibicao,
                local.DataHora,
                local.TempoTotalPrevistoMinutos,
                local.Observacao,
                Status = local.Status.ToString()
            }),
            DadosRemotosJson = JsonSerializer.Serialize(remoto),
            CriadoEm = DateTime.Now,
            Resolvido = false
        };

        context.ConflitosSincronizacao.Add(conflito);
        await context.SaveChangesAsync();
    }

    public async Task<List<OperacaoSincronizacao>> ObterOperacoesPendentesAsync()
    {
        using var context = CriarContexto();
        return await context.OperacoesSincronizacao
            .Where(o => o.Estado == EstadoProcessamentoOperacao.Pendente)
            .OrderBy(o => o.CriadoEm)
            .ToListAsync();
    }

    public async Task<List<ConflitoSincronizacao>> ObterConflitosNaoResolvidosAsync()
    {
        using var context = CriarContexto();
        return await context.ConflitosSincronizacao
            .Where(c => !c.Resolvido)
            .OrderByDescending(c => c.CriadoEm)
            .ToListAsync();
    }

    public async Task ResolverConflitoAsync(int conflitoId, string decisao)
    {
        using var context = CriarContexto();
        var conflito = await context.ConflitosSincronizacao.FindAsync(conflitoId);
        if (conflito == null) return;

        conflito.Resolvido = true;
        conflito.ResolvidoEm = DateTime.Now;
        conflito.ResolucaoEscolhida = decisao;

        if (conflito.AgendamentoId.HasValue)
        {
            var local = await context.Agendamentos
                .Include(a => a.Cliente)
                .FirstOrDefaultAsync(a => a.Id == conflito.AgendamentoId.Value);

            var pendente = await context.OperacoesSincronizacao
                .FirstOrDefaultAsync(o => o.AgendamentoId == conflito.AgendamentoId.Value && o.Estado == EstadoProcessamentoOperacao.Conflito);

            if (decisao == "ManterLocal")
            {
                if (pendente != null)
                {
                    pendente.Estado = EstadoProcessamentoOperacao.Pendente;
                    pendente.TipoOperacao = TipoOperacaoSincronizacao.Atualizar;
                }
            }
            else if (decisao == "ManterRemoto")
            {
                if (!string.IsNullOrEmpty(conflito.DadosRemotosJson) && local != null)
                {
                    var remoto = JsonSerializer.Deserialize<ICloudEventoRemoto>(conflito.DadosRemotosJson);
                    if (remoto != null)
                    {
                        local.DataHora = remoto.DataHoraInicio;
                        local.TempoTotalPrevistoMinutos = remoto.DuracaoMinutos;
                        local.Observacao = remoto.Descricao;
                        local.ICloudETag = remoto.ETag;
                        local.SincronizadoComICloud = true;
                    }
                }

                if (pendente != null)
                {
                    context.OperacoesSincronizacao.Remove(pendente);
                }
            }
        }

        await context.SaveChangesAsync();
        await AtualizarContadoresAsync();
        DispararSincronizacaoEmSegundoPlano();
    }

    // ================= CONTROLE DE RETRY, EXPONENTIAL BACKOFF E JITTER =================

    private void ResetarBackoff()
    {
        _intervaloAtualSegundos = IntervaloInicialSegundos;
    }

    private void CalcularProximoBackoff()
    {
        _intervaloAtualSegundos = Math.Min(IntervaloMaximoSegundos, _intervaloAtualSegundos * 2.0);

        var fatorJitter = 0.85 + (_random.NextDouble() * 0.30);
        var intervaloComJitter = _intervaloAtualSegundos * fatorJitter;

        AgendarProximaTentativa(TimeSpan.FromSeconds(intervaloComJitter));
    }

    private void AgendarProximaTentativa(TimeSpan delay)
    {
        if (_ctsMonitoramento == null || _ctsMonitoramento.IsCancellationRequested)
            return;

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(delay, _ctsMonitoramento.Token);
                if (!_ctsMonitoramento.IsCancellationRequested)
                {
                    await SincronizarAgoraAsync(_ctsMonitoramento.Token);
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ICloudSync] Erro no timer de retry: {ex.Message}");
            }
        });
    }

    public bool HabilitarSincronizacaoAutomaticaEmSegundoPlano { get; set; } = true;

    public void DispararSincronizacaoEmSegundoPlano()
    {
        if (!HabilitarSincronizacaoAutomaticaEmSegundoPlano)
            return;

        _ = Task.Run(async () =>
        {
            try
            {
                await SincronizarAgoraAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ICloudSync] Falha na sincronização em background: {ex.Message}");
            }
        });
    }

    public void IniciarMonitoramento()
    {
        PararMonitoramento();
        _ctsMonitoramento = new CancellationTokenSource();

        try
        {
            NetworkChange.NetworkAvailabilityChanged += OnNetworkAvailabilityChanged;
            NetworkChange.NetworkAddressChanged += OnNetworkAddressChanged;
        }
        catch { }

        AgendarProximaTentativa(TimeSpan.FromSeconds(3));
    }

    public void PararMonitoramento()
    {
        try
        {
            NetworkChange.NetworkAvailabilityChanged -= OnNetworkAvailabilityChanged;
            NetworkChange.NetworkAddressChanged -= OnNetworkAddressChanged;
        }
        catch { }

        if (_ctsMonitoramento != null)
        {
            _ctsMonitoramento.Cancel();
            _ctsMonitoramento.Dispose();
            _ctsMonitoramento = null;
        }
    }

    private void OnNetworkAvailabilityChanged(object? sender, NetworkAvailabilityEventArgs e)
    {
        if (e.IsAvailable)
        {
            Debug.WriteLine("[ICloudSync] Conectividade de rede restaurada. Disparando sincronização.");
            ResetarBackoff();
            DispararSincronizacaoEmSegundoPlano();
        }
        else
        {
            AtualizarEstado(EstadoSincronizacao.Offline);
        }
    }

    private void OnNetworkAddressChanged(object? sender, EventArgs e)
    {
        Debug.WriteLine("[ICloudSync] Endereço de rede alterado. Verificando reconexão.");
        DispararSincronizacaoEmSegundoPlano();
    }

    private void AtualizarEstado(EstadoSincronizacao novoEstado, string? erro = null)
    {
        EstadoAtual = novoEstado;
        if (erro != null) UltimoErro = erro;
        EstadoMudou?.Invoke(this, novoEstado);
    }

    private async Task AtualizarContadoresAsync()
    {
        try
        {
            using var context = CriarContexto();
            OperacoesPendentesCount = await context.OperacoesSincronizacao
                .CountAsync(o => o.Estado == EstadoProcessamentoOperacao.Pendente);

            ConflitosCount = await context.ConflitosSincronizacao
                .CountAsync(c => !c.Resolvido);
        }
        catch { }
    }

    public void Dispose()
    {
        PararMonitoramento();
        _syncLock.Dispose();
    }
}
