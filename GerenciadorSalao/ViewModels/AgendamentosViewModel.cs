using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GerenciadorSalao.Helpers;
using GerenciadorSalao.Models;
using GerenciadorSalao.Models.DTOs;
using GerenciadorSalao.Services;

namespace GerenciadorSalao.ViewModels;

public class AgendamentoListItemViewModel
{
    public int Id { get; set; }
    public DateTime DataHora { get; set; }
    public string Horario { get; set; } = string.Empty;
    public string DuracaoTexto { get; set; } = string.Empty;
    public string NomeCliente { get; set; } = string.Empty;
    public string ServicosTexto { get; set; } = string.Empty;
    public decimal ValorTotal { get; set; }
    public string ValorTotalTexto { get; set; } = string.Empty;
    public string StatusTexto { get; set; } = "Agendado";
    public string StatusBg { get; set; } = "#FEE9E6";
    public string StatusFg { get; set; } = "#885B53";
    public int? ComandaId { get; set; }
    public bool IsSincronizadoICloud { get; set; }
}

public partial class AgendamentosViewModel : ObservableObject, IModalHost
{
    private readonly IAgendamentoService _agendamentoService;
    private readonly IClienteService _clienteService;
    private readonly IServicoService _servicoService;
    private readonly IComandaService _comandaService;
    private readonly IICloudSyncService _iCloudSyncService;

    public bool IsModalAberto => IsModalNovoAgendamentoAberto || IsModalExcluirAberto || IsModalICloudAberto;

    public void FecharModalAtivo()
    {
        if (IsModalICloudAberto) FecharModalICloud();
        if (IsModalExcluirAberto) FecharModalExcluir();
        if (IsModalNovoAgendamentoAberto) FecharModalNovoAgendamento();
    }

    partial void OnIsModalNovoAgendamentoAbertoChanged(bool value)
    {
        OnPropertyChanged(nameof(IsModalAberto));
    }

    partial void OnIsModalExcluirAbertoChanged(bool value)
    {
        OnPropertyChanged(nameof(IsModalAberto));
    }

    partial void OnIsModalICloudAbertoChanged(bool value)
    {
        OnPropertyChanged(nameof(IsModalAberto));
    }

    public Action<string, object?>? NavigationRequested { get; set; }

    [ObservableProperty]
    private string _titulo = "Agenda";

    [ObservableProperty]
    private string _subtitulo = "Tempo reservado para transformar o dia de alguém.";

    [ObservableProperty]
    private DateTime _dataSelecionada = DateTime.Today;

    [ObservableProperty]
    private DateTime _mesVisualizado = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);

    [ObservableProperty]
    private string _mesVisualizadoTexto = string.Empty;

    [ObservableProperty]
    private bool _isCalendarOpen;

    [ObservableProperty]
    private List<CalendarDayItem> _diasCalendario = new();

    [ObservableProperty]
    private string _filtroSelecionado = "Todos";

    [ObservableProperty]
    private string _totalAgendamentosTexto = "0 agendamentos";

    [ObservableProperty]
    private bool _carregando;

    public ObservableCollection<AgendamentoListItemViewModel> TodosAgendamentos { get; } = new();
    public ObservableCollection<AgendamentoListItemViewModel> AgendamentosFiltrados { get; } = new();

    public string DataSelecionadaFormatada => DataSelecionada.ToString("dd/MM/yyyy");

    // ===================== PROPRIEDADES DO MODAL (NOVO / EDITAR) =====================
    [ObservableProperty]
    private bool _isModalNovoAgendamentoAberto;

    [ObservableProperty]
    private int? _agendamentoEmEdicaoId;

    public bool IsModoEdicao => AgendamentoEmEdicaoId.HasValue;

    [ObservableProperty]
    private string _modalTitulo = "Novo agendamento";

    [ObservableProperty]
    private string _modalSubtitulo = "Dados do atendimento";

    [ObservableProperty]
    private string _botaoSalvarTexto = "Salvar agendamento";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TemMensagemErroModal))]
    private string? _mensagemErroModal;

    public bool TemMensagemErroModal => !string.IsNullOrWhiteSpace(MensagemErroModal);

    [ObservableProperty]
    private Cliente? _clienteSelecionado;

    [ObservableProperty]
    private DateTime _novaData = DateTime.Today;

    [ObservableProperty]
    private string _novaHoraTexto = "09:00";

    [ObservableProperty]
    private string _novaDataHoraTexto = string.Empty;

    [ObservableProperty]
    private Servico? _servicoSelecionadoParaAdicionar;

    [ObservableProperty]
    private string _novaObservacao = string.Empty;

    [ObservableProperty]
    private int _tempoTotalNovoAgendamento;

    [ObservableProperty]
    private string _tempoTotalMinutosTexto = "0 minutos";

    [ObservableProperty]
    private decimal _valorTotalNovoAgendamento;

    [ObservableProperty]
    private string _valorTotalServicosTexto = "Total: R$ 0,00";

    [ObservableProperty]
    private string _buscaClienteTexto = string.Empty;

    [ObservableProperty]
    private bool _isClientePopupOpen;

    [ObservableProperty]
    private string _buscaServicoTexto = string.Empty;

    [ObservableProperty]
    private bool _isServicoPopupOpen;

    public ObservableCollection<Cliente> ClientesDisponiveis { get; } = new();
    public ObservableCollection<Cliente> ClientesDisponiveisFiltrados { get; } = new();
    public ObservableCollection<Servico> ServicosDisponiveis { get; } = new();
    public ObservableCollection<Servico> ServicosDisponiveisFiltrados { get; } = new();
    public ObservableCollection<AgendamentoServicoItemViewModel> ServicosSelecionadosParaNovo { get; } = new();

    // ===================== PROPRIEDADES DO MODAL DE EXCLUSÃO =====================
    [ObservableProperty]
    private bool _isModalExcluirAberto;

    [ObservableProperty]
    private AgendamentoListItemViewModel? _agendamentoParaExcluir;

    // ===================== PROPRIEDADES DO MODAL IPHONE / ICLOUD =====================
    [ObservableProperty]
    private bool _isModalICloudAberto;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ICloudBotaoHeaderTexto))]
    [NotifyPropertyChangedFor(nameof(ICloudStatusCor))]
    [NotifyPropertyChangedFor(nameof(ICloudStatusTexto))]
    private EstadoSincronizacao _iCloudEstado = EstadoSincronizacao.Offline;

    public string ICloudStatusCor => ICloudEstado switch
    {
        EstadoSincronizacao.Sincronizado => "#2E7D32",         // Verde
        EstadoSincronizacao.Offline => "#9E9E9E",              // Cinza
        EstadoSincronizacao.Pendente => "#F57C00",             // Âmbar / Laranja
        EstadoSincronizacao.Sincronizando => "#1976D2",        // Azul
        EstadoSincronizacao.Conflito => "#E64A19",             // Laranja escuro / Alerta
        EstadoSincronizacao.AutenticacaoNecessaria => "#7B1FA2",// Roxo
        EstadoSincronizacao.ErroSincronizacao => "#D32F2F",    // Vermelho
        _ => "#9E9E9E"
    };

    public string ICloudStatusTexto => ICloudEstado switch
    {
        EstadoSincronizacao.Sincronizado => "Sincronizado com iPhone",
        EstadoSincronizacao.Offline => "Serviço Offline / Sem Conexão",
        EstadoSincronizacao.Pendente => $"{ICloudOperacoesPendentesCount} alteração(ões) pendente(s)",
        EstadoSincronizacao.Sincronizando => "Sincronizando com iPhone...",
        EstadoSincronizacao.Conflito => $"{ICloudConflitosCount} conflito(s) aguardando decisão",
        EstadoSincronizacao.AutenticacaoNecessaria => "Autenticação Necessária (Senha de App)",
        EstadoSincronizacao.ErroSincronizacao => "Erro de sincronização",
        _ => "Desconectado"
    };

    public string ICloudBotaoHeaderTexto => ICloudEstado switch
    {
        EstadoSincronizacao.Sincronizado => "iPhone: OK",
        EstadoSincronizacao.Pendente => $"iPhone: {ICloudOperacoesPendentesCount} pend.",
        EstadoSincronizacao.Sincronizando => "iPhone: Sincronizando...",
        EstadoSincronizacao.Conflito => $"iPhone: {ICloudConflitosCount} conflito(s)",
        EstadoSincronizacao.Offline => "iPhone: Offline",
        EstadoSincronizacao.AutenticacaoNecessaria => "iPhone: Conectar",
        EstadoSincronizacao.ErroSincronizacao => "iPhone: Erro",
        _ => "Calendário iPhone"
    };

    [ObservableProperty]
    private string _iCloudUltimaSincronizacaoTexto = "Nenhuma sincronização realizada";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ICloudStatusTexto))]
    [NotifyPropertyChangedFor(nameof(ICloudBotaoHeaderTexto))]
    private int _iCloudOperacoesPendentesCount;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ICloudStatusTexto))]
    [NotifyPropertyChangedFor(nameof(ICloudBotaoHeaderTexto))]
    private int _iCloudConflitosCount;

    [ObservableProperty]
    private bool _isICloudConectado;

    [ObservableProperty]
    private string _iCloudEmailInput = string.Empty;

    [ObservableProperty]
    private string _iCloudSenhaAppInput = string.Empty;

    [ObservableProperty]
    private bool _isSincronizandoICloud;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TemFeedbackICloud))]
    private string? _iCloudFeedbackMensagem;

    public bool TemFeedbackICloud => !string.IsNullOrWhiteSpace(ICloudFeedbackMensagem);

    [ObservableProperty]
    private bool _iCloudFeedbackSucesso;

    [ObservableProperty]
    private ObservableCollection<OperacaoSincronizacao> _listaOperacoesPendentes = new();

    [ObservableProperty]
    private ObservableCollection<ConflitoSincronizacao> _listaConflitos = new();

    [ObservableProperty]
    private bool _isExecutandoTestes;

    [ObservableProperty]
    private string _resultadosTestesTexto = string.Empty;

    public AgendamentosViewModel(
        IAgendamentoService agendamentoService,
        IClienteService clienteService,
        IServicoService servicoService,
        IComandaService comandaService,
        IICloudSyncService iCloudSyncService)
    {
        _agendamentoService = agendamentoService;
        _clienteService = clienteService;
        _servicoService = servicoService;
        _comandaService = comandaService;
        _iCloudSyncService = iCloudSyncService;

        _iCloudSyncService.EstadoMudou += (s, estado) =>
        {
            System.Windows.Application.Current?.Dispatcher?.Invoke(() =>
            {
                ICloudEstado = estado;
                _ = AtualizarStatusICloudAsync();
                if (estado == EstadoSincronizacao.Sincronizado) { _ = CarregarAgendamentosAsync(); }
            });
        };

        MesVisualizado = new DateTime(DataSelecionada.Year, DataSelecionada.Month, 1);
        AtualizarDiasCalendario();
        _ = CarregarAgendamentosAsync();
        _ = AtualizarStatusICloudAsync();
    }

    partial void OnDataSelecionadaChanged(DateTime value)
    {
        OnPropertyChanged(nameof(DataSelecionadaFormatada));
        AtualizarDiasCalendario();
        _ = CarregarAgendamentosAsync();
    }

    partial void OnIsCalendarOpenChanged(bool value)
    {
        if (value)
        {
            MesVisualizado = new DateTime(DataSelecionada.Year, DataSelecionada.Month, 1);
            AtualizarDiasCalendario();
        }
    }

    public bool IsFiltroTodos
    {
        get => FiltroSelecionado == "Todos";
        set { if (value) DefinirFiltro("Todos"); }
    }

    public bool IsFiltroAgendados
    {
        get => FiltroSelecionado == "Agendados";
        set { if (value) DefinirFiltro("Agendados"); }
    }

    public bool IsFiltroEmAtendimento
    {
        get => FiltroSelecionado == "Em atendimento";
        set { if (value) DefinirFiltro("Em atendimento"); }
    }

    partial void OnFiltroSelecionadoChanged(string value)
    {
        OnPropertyChanged(nameof(IsFiltroTodos));
        OnPropertyChanged(nameof(IsFiltroAgendados));
        OnPropertyChanged(nameof(IsFiltroEmAtendimento));
        AplicarFiltro();
    }

    [RelayCommand]
    public async Task CarregarAgendamentosAsync()
    {
        try
        {
            Carregando = true;
            var inicioDia = DataSelecionada.Date;
            var fimDia = inicioDia.AddDays(1).AddTicks(-1);

            var agendamentosDb = await _agendamentoService.ObterPorPeriodoAsync(inicioDia, fimDia);

            TodosAgendamentos.Clear();

            foreach (var ag in agendamentosDb)
            {
                if (ag.Status == StatusAgendamento.Cancelado) continue;
                var nomeCli = ag.Cliente?.NomeExibicao ?? "Cliente";
                var servicos = string.Join(" + ", ag.AgendamentoServicos.Select(s => s.Servico?.NomeExibicao ?? s.Servico?.Descricao ?? "Serviço"));
                if (string.IsNullOrWhiteSpace(servicos)) servicos = "Atendimento geral";

                var valorTotal = ag.Comanda != null && ag.Comanda.ValorTotal > 0 
                    ? ag.Comanda.ValorTotal 
                    : ag.AgendamentoServicos.Sum(s => s.Servico?.Valor ?? 0);

                string statusTexto;
                string statusBg;
                string statusFg;

                if (ag.Comanda != null && ag.Comanda.Status == StatusComanda.Aberta)
                {
                    statusTexto = "Em atendimento";
                    statusBg = "#FFF3E0";
                    statusFg = "#ED6C02";
                }
                else if (ag.Status == StatusAgendamento.Concluido)
                {
                    statusTexto = "Concluído";
                    statusBg = "#E8F5E9";
                    statusFg = "#2E7D32";
                }
                else
                {
                    statusTexto = "Agendada";
                    statusBg = "#FEE9E6";
                    statusFg = "#885B53";
                }

                TodosAgendamentos.Add(new AgendamentoListItemViewModel
                {
                    Id = ag.Id,
                    DataHora = ag.DataHora,
                    Horario = ag.DataHora.ToString("HH:mm"),
                    DuracaoTexto = ValidationHelper.FormatarDuracao(ag.TempoTotalPrevistoMinutos),
                    NomeCliente = nomeCli,
                    ServicosTexto = servicos,
                    ValorTotal = valorTotal,
                    ValorTotalTexto = valorTotal.ToString("C2"),
                    StatusTexto = statusTexto,
                    StatusBg = statusBg,
                    StatusFg = statusFg,
                    ComandaId = ag.ComandaId,
                    IsSincronizadoICloud = ag.SincronizadoComICloud || !string.IsNullOrEmpty(ag.ICloudEventId)
                });
            }

            AplicarFiltro();
        }
        catch
        {
            // Proteção contra falhas visuais
        }
        finally
        {
            Carregando = false;
        }
    }

    private void AplicarFiltro()
    {
        AgendamentosFiltrados.Clear();

        IEnumerable<AgendamentoListItemViewModel> filtrados = TodosAgendamentos;

        if (FiltroSelecionado == "Agendados")
        {
            filtrados = TodosAgendamentos.Where(a => a.StatusTexto.StartsWith("Agendad", StringComparison.OrdinalIgnoreCase));
        }
        else if (FiltroSelecionado == "Em atendimento")
        {
            filtrados = TodosAgendamentos.Where(a => a.StatusTexto.Equals("Em atendimento", StringComparison.OrdinalIgnoreCase));
        }

        foreach (var item in filtrados)
        {
            AgendamentosFiltrados.Add(item);
        }

        var total = AgendamentosFiltrados.Count;
        TotalAgendamentosTexto = total == 1 ? "1 agendamento" : $"{total} agendamentos";
    }

    [RelayCommand]
    public void DefinirFiltro(string filtro)
    {
        FiltroSelecionado = filtro;
    }

    // ===================== COMANDOS DO CALENDÁRIO =====================
    public void AtualizarDiasCalendario()
    {
        MesVisualizadoTexto = MesVisualizado.ToString("MMMM 'de' yyyy", new CultureInfo("pt-BR"));

        var dias = new List<CalendarDayItem>();
        var primeiroDiaDoMes = new DateTime(MesVisualizado.Year, MesVisualizado.Month, 1);

        int diff = (int)primeiroDiaDoMes.DayOfWeek;
        var dataInicio = primeiroDiaDoMes.AddDays(-diff);

        for (int i = 0; i < 42; i++)
        {
            var dataAtual = dataInicio.AddDays(i);
            dias.Add(new CalendarDayItem
            {
                Data = dataAtual,
                IsMesAtual = dataAtual.Month == MesVisualizado.Month,
                IsSelecionado = dataAtual.Date == DataSelecionada.Date,
                IsHoje = dataAtual.Date == DateTime.Today
            });
        }

        DiasCalendario = dias;
    }

    [RelayCommand]
    public void MesAnterior()
    {
        MesVisualizado = MesVisualizado.AddMonths(-1);
        AtualizarDiasCalendario();
    }

    [RelayCommand]
    public void MesProximo()
    {
        MesVisualizado = MesVisualizado.AddMonths(1);
        AtualizarDiasCalendario();
    }

    [RelayCommand]
    public void SelecionarData(DateTime data)
    {
        DataSelecionada = data.Date;
        MesVisualizado = new DateTime(data.Year, data.Month, 1);
        IsCalendarOpen = false;
    }

    [RelayCommand]
    public void HojeData()
    {
        DataSelecionada = DateTime.Today;
        MesVisualizado = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        IsCalendarOpen = false;
    }

    [RelayCommand]
    public void LimparData()
    {
        DataSelecionada = DateTime.Today;
        MesVisualizado = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        IsCalendarOpen = false;
    }

    // ===================== AÇÕES DOS CARDS =====================
    [RelayCommand]
    public void AbrirComanda(int agendamentoId)
    {
        var item = TodosAgendamentos.FirstOrDefault(a => a.Id == agendamentoId);
        if (item?.ComandaId != null && item.ComandaId > 0)
        {
            NavigationRequested?.Invoke("Comandas", item.ComandaId.Value);
        }
        else
        {
            NavigationRequested?.Invoke("Comandas", ("agendamento", agendamentoId));
        }
    }

    [RelayCommand]
    public void SolicitarExcluirAgendamento(AgendamentoListItemViewModel? item)
    {
        if (item == null) return;
        AgendamentoParaExcluir = item;
        IsModalExcluirAberto = true;
    }

    [RelayCommand]
    public void FecharModalExcluir()
    {
        IsModalExcluirAberto = false;
        AgendamentoParaExcluir = null;
    }

    [RelayCommand]
    public async Task ConfirmarExclusaoAsync()
    {
        if (AgendamentoParaExcluir == null) return;
        var id = AgendamentoParaExcluir.Id;
        FecharModalExcluir();
        await _agendamentoService.ExcluirAgendamentoAsync(id);
        await CarregarAgendamentosAsync();
    }

    // ===================== COMANDOS DO IPHONE / ICLOUD CALENDAR =====================

    [RelayCommand]
    public async Task AtualizarStatusICloudAsync()
    {
        try
        {
            var config = await _iCloudSyncService.ObterConfiguracaoAsync();
            ICloudEstado = _iCloudSyncService.EstadoAtual;
            ICloudOperacoesPendentesCount = config.OperacoesPendentesCount;
            ICloudConflitosCount = config.ConflitosCount;
            IsICloudConectado = !string.IsNullOrWhiteSpace(config.AppleIdEmail) && config.EstaAtivo;

            if (string.IsNullOrEmpty(ICloudEmailInput) && !string.IsNullOrEmpty(config.AppleIdEmail))
            {
                ICloudEmailInput = config.AppleIdEmail;
            }

            if (config.UltimaSincronizacaoSucesso.HasValue)
            {
                ICloudUltimaSincronizacaoTexto = $"Última sincronização: {config.UltimaSincronizacaoSucesso.Value:dd/MM/yyyy 'às' HH:mm}";
            }
            else
            {
                ICloudUltimaSincronizacaoTexto = "Nenhuma sincronização recente realizada";
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[AgendamentosViewModel] Erro ao atualizar status iCloud: {ex.Message}");
        }
    }

    [RelayCommand]
    public async Task AbrirModalICloudAsync()
    {
        try
        {
            ICloudFeedbackMensagem = null;
            await AtualizarStatusICloudAsync();

            // Carrega pendências da fila SQLite
            var pendentes = await _iCloudSyncService.ObterOperacoesPendentesAsync();
            ListaOperacoesPendentes.Clear();
            foreach (var p in pendentes)
            {
                ListaOperacoesPendentes.Add(p);
            }

            // Carrega conflitos não resolvidos
            var conflitos = await _iCloudSyncService.ObterConflitosNaoResolvidosAsync();
            ListaConflitos.Clear();
            foreach (var c in conflitos)
            {
                ListaConflitos.Add(c);
            }

            IsModalICloudAberto = true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[AgendamentosViewModel] Erro ao abrir modal iCloud: {ex}");
            ICloudFeedbackSucesso = false;
            ICloudFeedbackMensagem = $"Não foi possível carregar os dados de sincronização: {ex.Message}";
            IsModalICloudAberto = true;
        }
    }

    [RelayCommand]
    public void FecharModalICloud()
    {
        IsModalICloudAberto = false;
        ICloudFeedbackMensagem = null;
    }

    [RelayCommand]
    public async Task SalvarCredenciaisICloudAsync()
    {
        try
        {
            if (string.IsNullOrWhiteSpace(ICloudEmailInput) || string.IsNullOrWhiteSpace(ICloudSenhaAppInput))
            {
                ICloudFeedbackSucesso = false;
                ICloudFeedbackMensagem = "Por favor, informe o seu Apple ID e a Senha de App gerada em appleid.apple.com.";
                return;
            }

            ICloudFeedbackSucesso = true;
            ICloudFeedbackMensagem = "Salvando credenciais e conectando ao iCloud...";

            await _iCloudSyncService.SalvarConfiguracaoAsync(ICloudEmailInput, ICloudSenhaAppInput, ativar: true, dispararSincronizacao: false);
            await AtualizarStatusICloudAsync();

            ICloudFeedbackSucesso = true;
            ICloudFeedbackMensagem = "Conta conectada com sucesso! Uma sincronização em segundo plano foi iniciada.";
            await SincronizarICloudAgoraAsync();
        }
        catch (Exception ex)
        {
            ICloudFeedbackSucesso = false;
            ICloudFeedbackMensagem = $"Falha ao salvar credenciais: {ex.Message}";
        }
    }

    [RelayCommand]
    public async Task DesconectarICloudAsync()
    {
        try
        {
            await _iCloudSyncService.DesconectarAsync();
            ICloudSenhaAppInput = string.Empty;
            await AtualizarStatusICloudAsync();
            ICloudFeedbackSucesso = true;
            ICloudFeedbackMensagem = "Conta do iCloud desconectada com sucesso.";
        }
        catch (Exception ex)
        {
            ICloudFeedbackSucesso = false;
            ICloudFeedbackMensagem = $"Erro ao desconectar: {ex.Message}";
        }
    }

    [RelayCommand]
    public async Task SincronizarICloudAgoraAsync()
    {
        try
        {
            IsSincronizandoICloud = true;
            ICloudFeedbackMensagem = "Sincronizando com o calendário do iPhone...";

            var resultado = await _iCloudSyncService.SincronizarAgoraAsync();
            ICloudFeedbackSucesso = resultado.Sucesso;
            ICloudFeedbackMensagem = resultado.Mensagem;

            await AtualizarStatusICloudAsync();
            await CarregarAgendamentosAsync();

            // Atualiza listas do modal
            var pendentes = await _iCloudSyncService.ObterOperacoesPendentesAsync();
            ListaOperacoesPendentes.Clear();
            foreach (var p in pendentes)
            {
                ListaOperacoesPendentes.Add(p);
            }

            var conflitos = await _iCloudSyncService.ObterConflitosNaoResolvidosAsync();
            ListaConflitos.Clear();
            foreach (var c in conflitos)
            {
                ListaConflitos.Add(c);
            }
        }
        catch (Exception ex)
        {
            ICloudFeedbackSucesso = false;
            ICloudFeedbackMensagem = $"Erro na sincronização: {ex.Message}";
        }
        finally
        {
            IsSincronizandoICloud = false;
        }
    }

    [RelayCommand]
    public async Task ResolverConflitoManterLocalAsync(int conflitoId)
    {
        try
        {
            await _iCloudSyncService.ResolverConflitoAsync(conflitoId, "ManterLocal");
            ICloudFeedbackSucesso = true;
            ICloudFeedbackMensagem = "Conflito resolvido: mantida a versão do Salão.";
            await SincronizarICloudAgoraAsync();
        }
        catch (Exception ex)
        {
            ICloudFeedbackSucesso = false;
            ICloudFeedbackMensagem = $"Erro ao resolver conflito: {ex.Message}";
        }
    }

    [RelayCommand]
    public async Task ResolverConflitoManterRemotoAsync(int conflitoId)
    {
        try
        {
            await _iCloudSyncService.ResolverConflitoAsync(conflitoId, "ManterRemoto");
            ICloudFeedbackSucesso = true;
            ICloudFeedbackMensagem = "Conflito resolvido: mantida a versão do iPhone.";
            await SincronizarICloudAgoraAsync();
        }
        catch (Exception ex)
        {
            ICloudFeedbackSucesso = false;
            ICloudFeedbackMensagem = $"Erro ao resolver conflito: {ex.Message}";
        }
    }

    [RelayCommand]
    public async Task ExecutarTestesAutomatizadosAsync()
    {
        try
        {
            IsExecutandoTestes = true;
            ResultadosTestesTexto = "Iniciando execução da bateria de 11 testes de aceitação com provedor simulado...\n";

            var sb = new System.Text.StringBuilder();
            var resultados = await GerenciadorSalao.Services.Tests.ICloudSyncAcceptanceTests.ExecutarTodosOsTestesAsync(msg =>
            {
                System.Windows.Application.Current?.Dispatcher?.Invoke(() =>
                {
                    sb.AppendLine(msg);
                    ResultadosTestesTexto = sb.ToString();
                });
            });

            int passaram = resultados.Count(r => r.Passou);
            sb.AppendLine($"\nFinalizado: {passaram}/{resultados.Count} testes passaram com sucesso!");
            ResultadosTestesTexto = sb.ToString();
            ICloudFeedbackSucesso = passaram == resultados.Count;
            ICloudFeedbackMensagem = $"{passaram}/{resultados.Count} testes de aceitação executados com sucesso!";
        }
        catch (Exception ex)
        {
            ICloudFeedbackSucesso = false;
            ICloudFeedbackMensagem = $"Erro ao executar testes: {ex.Message}";
        }
        finally
        {
            IsExecutandoTestes = false;
        }
    }

    partial void OnBuscaClienteTextoChanged(string value)
    {
        if (ClienteSelecionado != null && value != ClienteSelecionado.NomeExibicao)
        {
            ClienteSelecionado = null;
        }
        FiltrarClientesDisponiveis();
        if (!string.IsNullOrEmpty(value) && (ClienteSelecionado == null || value != ClienteSelecionado.NomeExibicao))
        {
            IsClientePopupOpen = true;
        }
    }

    partial void OnBuscaServicoTextoChanged(string value)
    {
        FiltrarServicosDisponiveis();
        if (!string.IsNullOrEmpty(value))
        {
            IsServicoPopupOpen = true;
        }
    }

    [RelayCommand]
    public void SelecionarClienteOption(Cliente? cliente)
    {
        if (cliente == null) return;
        ClienteSelecionado = cliente;
        BuscaClienteTexto = cliente.NomeExibicao;
        IsClientePopupOpen = false;
        MensagemErroModal = null;
    }

    private void FiltrarClientesDisponiveis()
    {
        var termo = BuscaClienteTexto?.Trim() ?? string.Empty;
        ClientesDisponiveisFiltrados.Clear();
        var lista = string.IsNullOrWhiteSpace(termo) || (ClienteSelecionado != null && termo == ClienteSelecionado.NomeExibicao)
            ? (IEnumerable<Cliente>)ClientesDisponiveis
            : ClientesDisponiveis.Where(c =>
                (c.Nome != null && c.Nome.Contains(termo, StringComparison.OrdinalIgnoreCase)) ||
                (c.Apelido != null && c.Apelido.Contains(termo, StringComparison.OrdinalIgnoreCase)) ||
                (c.Telefone != null && c.Telefone.Contains(termo, StringComparison.OrdinalIgnoreCase)));

        foreach (var cli in lista.Take(100))
        {
            ClientesDisponiveisFiltrados.Add(cli);
        }
    }

    [RelayCommand]
    public void SelecionarServicoOption(Servico? servico)
    {
        if (servico == null) return;

        var item = new AgendamentoServicoItemViewModel
        {
            ServicoId = servico.Id,
            NomeExibicao = servico.NomeExibicao,
            DuracaoMinutos = servico.DuracaoMinutos,
            DuracaoTexto = ValidationHelper.FormatarMinutosParaHhMm(servico.DuracaoMinutos),
            Valor = servico.Valor,
            ValorTexto = servico.Valor.ToString("N2", new CultureInfo("pt-BR")),
            OnChanged = AtualizarTotaisNovoAgendamento
        };

        ServicosSelecionadosParaNovo.Add(item);
        BuscaServicoTexto = string.Empty;
        IsServicoPopupOpen = false;
        AtualizarTotaisNovoAgendamento();
        MensagemErroModal = null;
    }

    private void FiltrarServicosDisponiveis()
    {
        var termo = BuscaServicoTexto?.Trim() ?? string.Empty;
        ServicosDisponiveisFiltrados.Clear();
        var lista = string.IsNullOrWhiteSpace(termo)
            ? (IEnumerable<Servico>)ServicosDisponiveis
            : ServicosDisponiveis.Where(s =>
                (s.Descricao != null && s.Descricao.Contains(termo, StringComparison.OrdinalIgnoreCase)) ||
                (s.Especificacao != null && s.Especificacao.Contains(termo, StringComparison.OrdinalIgnoreCase)));

        foreach (var s in lista.Take(100))
        {
            ServicosDisponiveisFiltrados.Add(s);
        }
    }

    // ===================== MODAL NOVO / EDITAR AGENDAMENTO =====================
    [RelayCommand]
    public async Task AbrirModalNovoAgendamentoAsync()
    {
        try
        {
            MensagemErroModal = null;
            AgendamentoEmEdicaoId = null;
            ModalTitulo = "Novo agendamento";
            ModalSubtitulo = "Dados do atendimento";
            BotaoSalvarTexto = "Salvar agendamento";

            await CarregarListasFormularioAsync();

            ClienteSelecionado = null;
            BuscaClienteTexto = string.Empty;
            IsClientePopupOpen = false;

            BuscaServicoTexto = string.Empty;
            IsServicoPopupOpen = false;

            ServicosSelecionadosParaNovo.Clear();
            NovaObservacao = string.Empty;

            NovaData = DataSelecionada.Date;
            NovaHoraTexto = "09:00";
            NovaDataHoraTexto = $"{DataSelecionada:dd/MM/yyyy} 09:00";

            FiltrarClientesDisponiveis();
            FiltrarServicosDisponiveis();

            AtualizarTotaisNovoAgendamento();
            IsModalNovoAgendamentoAberto = true;
        }
        catch (Exception ex)
        {
            MensagemErroModal = $"Erro ao preparar formulário: {ex.Message}";
            IsModalNovoAgendamentoAberto = true;
        }
    }

    [RelayCommand]
    public async Task AbrirModalEditarAgendamentoAsync(int agendamentoId)
    {
        try
        {
            MensagemErroModal = null;
            var agendamentoDb = await _agendamentoService.ObterPorIdAsync(agendamentoId);
            if (agendamentoDb == null) return;

            AgendamentoEmEdicaoId = agendamentoDb.Id;
            ModalTitulo = "Editar agendamento";
            ModalSubtitulo = "Altere o cliente, horário ou os serviços previstos";
            BotaoSalvarTexto = "Atualizar agendamento";

            await CarregarListasFormularioAsync();

            ClienteSelecionado = ClientesDisponiveis.FirstOrDefault(c => c.Id == agendamentoDb.ClienteId);
            BuscaClienteTexto = ClienteSelecionado?.NomeExibicao ?? string.Empty;
            IsClientePopupOpen = false;

            BuscaServicoTexto = string.Empty;
            IsServicoPopupOpen = false;

            NovaObservacao = agendamentoDb.Observacao ?? string.Empty;

            NovaData = agendamentoDb.DataHora.Date;
            NovaHoraTexto = agendamentoDb.DataHora.ToString("HH:mm");
            NovaDataHoraTexto = agendamentoDb.DataHora.ToString("dd/MM/yyyy HH:mm");

            ServicosSelecionadosParaNovo.Clear();
            foreach (var asv in agendamentoDb.AgendamentoServicos)
            {
                var s = ServicosDisponiveis.FirstOrDefault(x => x.Id == asv.ServicoId) ?? asv.Servico;
                if (s != null)
                {
                    var comandaItem = agendamentoDb.Comanda?.Itens.FirstOrDefault(i => i.ServicoId == s.Id);
                    var valor = comandaItem?.ValorUnitario ?? s.Valor;
                    var duracao = s.DuracaoMinutos;

                    ServicosSelecionadosParaNovo.Add(new AgendamentoServicoItemViewModel
                    {
                        ServicoId = s.Id,
                        NomeExibicao = s.NomeExibicao,
                        DuracaoMinutos = duracao,
                        DuracaoTexto = ValidationHelper.FormatarMinutosParaHhMm(duracao),
                        Valor = valor,
                        ValorTexto = valor.ToString("N2", new CultureInfo("pt-BR")),
                        OnChanged = AtualizarTotaisNovoAgendamento
                    });
                }
            }

            FiltrarClientesDisponiveis();
            FiltrarServicosDisponiveis();

            AtualizarTotaisNovoAgendamento();
            IsModalNovoAgendamentoAberto = true;
        }
        catch (Exception ex)
        {
            MensagemErroModal = $"Erro ao carregar agendamento: {ex.Message}";
            IsModalNovoAgendamentoAberto = true;
        }
    }

    private async Task CarregarListasFormularioAsync()
    {
        var clientes = await _clienteService.ObterTodosAsync();
        ClientesDisponiveis.Clear();
        foreach (var c in clientes.OrderBy(c => c.NomeExibicao))
            ClientesDisponiveis.Add(c);

        var servicos = await _servicoService.ObterTodosAsync(apenasAtivos: true);
        ServicosDisponiveis.Clear();
        foreach (var s in servicos.OrderBy(s => s.NomeExibicao))
            ServicosDisponiveis.Add(s);

        FiltrarClientesDisponiveis();
        FiltrarServicosDisponiveis();
    }

    [RelayCommand]
    public void FecharModalNovoAgendamento()
    {
        IsModalNovoAgendamentoAberto = false;
        IsClientePopupOpen = false;
        IsServicoPopupOpen = false;
        AgendamentoEmEdicaoId = null;
        MensagemErroModal = null;
    }

    [RelayCommand]
    public void RemoverServico(AgendamentoServicoItemViewModel? item)
    {
        if (item == null) return;
        ServicosSelecionadosParaNovo.Remove(item);
        AtualizarTotaisNovoAgendamento();
    }

    private void AtualizarTotaisNovoAgendamento()
    {
        TempoTotalNovoAgendamento = ServicosSelecionadosParaNovo.Sum(s => s.DuracaoMinutos);
        TempoTotalMinutosTexto = ValidationHelper.FormatarDuracao(TempoTotalNovoAgendamento);

        ValorTotalNovoAgendamento = ServicosSelecionadosParaNovo.Sum(s => s.Valor);
        ValorTotalServicosTexto = $"Total: {ValorTotalNovoAgendamento:C2}";
    }

    [RelayCommand]
    public async Task SalvarNovoAgendamentoAsync()
    {
        MensagemErroModal = null;

        if (ClienteSelecionado == null)
        {
            MensagemErroModal = "Por favor, selecione um cliente.";
            return;
        }

        if (ServicosSelecionadosParaNovo.Count == 0)
        {
            MensagemErroModal = "Por favor, adicione pelo menos um serviço ao agendamento.";
            return;
        }

        DateTime dataHoraAgendamento;
        if (ValidationHelper.ValidarHorario(NovaHoraTexto, out var tsHorario, out var erroHorario))
        {
            dataHoraAgendamento = NovaData.Date.Add(tsHorario);
            NovaHoraTexto = ValidationHelper.NormalizarHorario(NovaHoraTexto);
        }
        else if (DateTime.TryParseExact(
                    NovaDataHoraTexto?.Trim(),
                    new[] { "dd/MM/yyyy HH:mm", "dd/MM/yyyy H:mm", "yyyy-MM-dd HH:mm" },
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out var dtParsed))
        {
            dataHoraAgendamento = dtParsed;
        }
        else
        {
            MensagemErroModal = erroHorario ?? "Informe um horário válido no formato HH:mm (ex: 09:30).";
            return;
        }

        try
        {
            var servicoIds = ServicosSelecionadosParaNovo.Select(s => s.ServicoId).ToList();
            var servicosCustomizados = ServicosSelecionadosParaNovo.Select(s => new ItemServicoAgendamentoDto
            {
                ServicoId = s.ServicoId,
                Valor = s.Valor,
                DuracaoMinutos = s.DuracaoMinutos
            }).ToList();

            if (AgendamentoEmEdicaoId.HasValue)
            {
                await _agendamentoService.AtualizarAgendamentoAsync(
                    AgendamentoEmEdicaoId.Value,
                    ClienteSelecionado.Id,
                    dataHoraAgendamento,
                    servicoIds,
                    NovaObservacao,
                    servicosCustomizados);
            }
            else
            {
                await _agendamentoService.CriarAgendamentoAsync(
                    ClienteSelecionado.Id,
                    dataHoraAgendamento,
                    servicoIds,
                    NovaObservacao,
                    servicosCustomizados);
            }

            IsModalNovoAgendamentoAberto = false;
            IsClientePopupOpen = false;
            IsServicoPopupOpen = false;
            AgendamentoEmEdicaoId = null;
            DataSelecionada = dataHoraAgendamento.Date;
            await CarregarAgendamentosAsync();
        }
        catch (Exception ex)
        {
            MensagemErroModal = $"Não foi possível salvar o agendamento: {ex.Message}";
        }
    }
}

public partial class AgendamentoServicoItemViewModel : ObservableObject
{
    private static readonly CultureInfo PtBrCulture = new("pt-BR");

    public int ServicoId { get; set; }
    public string NomeExibicao { get; set; } = string.Empty;

    [ObservableProperty]
    private int _duracaoMinutos;

    [ObservableProperty]
    private string _duracaoTexto = string.Empty;

    [ObservableProperty]
    private decimal _valor;

    [ObservableProperty]
    private string _valorTexto = "0,00";

    public Action? OnChanged { get; set; }

    partial void OnValorTextoChanged(string value)
    {
        if (ValidationHelper.ValidarValorMonetario(value, out var parsed, out _))
        {
            _valor = parsed;
        }
        else
        {
            _valor = 0;
        }
        OnChanged?.Invoke();
    }

    partial void OnDuracaoTextoChanged(string value)
    {
        if (ValidationHelper.ValidarDuracaoServico(value, out var mins, out _))
        {
            _duracaoMinutos = mins;
        }
        else if (int.TryParse(ValidationHelper.LimparApenasDigitos(value), out var minsNum) && minsNum > 0)
        {
            _duracaoMinutos = minsNum;
        }
        OnChanged?.Invoke();
    }
}
