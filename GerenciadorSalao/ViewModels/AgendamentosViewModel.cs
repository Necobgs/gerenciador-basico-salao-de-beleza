using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
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
}

public partial class AgendamentosViewModel : ObservableObject, IModalHost
{
    public bool IsModalAberto => IsModalNovoAgendamentoAberto;

    public void FecharModalAtivo()
    {
        FecharModalNovoAgendamento();
    }

    partial void OnIsModalNovoAgendamentoAbertoChanged(bool value)
    {
        OnPropertyChanged(nameof(IsModalAberto));
    }
    private readonly IAgendamentoService _agendamentoService;
    private readonly IClienteService _clienteService;
    private readonly IServicoService _servicoService;
    private readonly IComandaService _comandaService;

    public Action<string>? NavigationRequested { get; set; }

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

    // ===================== PROPRIEDADES DO MODAL NOVO AGENDAMENTO =====================
    [ObservableProperty]
    private bool _isModalNovoAgendamentoAberto;

    [ObservableProperty]
    private Cliente? _clienteSelecionado;

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

    public ObservableCollection<Cliente> ClientesDisponiveis { get; } = new();
    public ObservableCollection<Servico> ServicosDisponiveis { get; } = new();
    public ObservableCollection<Servico> ServicosSelecionadosParaNovo { get; } = new();

    public AgendamentosViewModel(
        IAgendamentoService agendamentoService,
        IClienteService clienteService,
        IServicoService servicoService,
        IComandaService comandaService)
    {
        _agendamentoService = agendamentoService;
        _clienteService = clienteService;
        _servicoService = servicoService;
        _comandaService = comandaService;

        MesVisualizado = new DateTime(DataSelecionada.Year, DataSelecionada.Month, 1);
        AtualizarDiasCalendario();
        _ = CarregarAgendamentosAsync();
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
                var nomeCli = ag.Cliente?.Nome ?? "Cliente";
                var servicos = string.Join(" + ", ag.AgendamentoServicos.Select(s => s.Servico?.Descricao ?? "Serviço"));
                if (string.IsNullOrWhiteSpace(servicos)) servicos = "Atendimento geral";

                var valorTotal = ag.AgendamentoServicos.Sum(s => s.Servico?.Valor ?? 0);

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
                    ComandaId = ag.ComandaId
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
    public async Task AbrirComandaAsync(int agendamentoId)
    {
        NavigationRequested?.Invoke("Comandas");
    }

    [RelayCommand]
    public async Task ExcluirAgendamentoAsync(int agendamentoId)
    {
        var confirm = MessageBox.Show(
            "Deseja realmente cancelar este agendamento?",
            "Cancelar Agendamento",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (confirm == MessageBoxResult.Yes)
        {
            await _agendamentoService.ExcluirAgendamentoAsync(agendamentoId);
            await CarregarAgendamentosAsync();
        }
    }

    // ===================== MODAL NOVO AGENDAMENTO =====================
    [RelayCommand]
    public async Task AbrirModalNovoAgendamentoAsync()
    {
        try
        {
            // Carrega Clientes
            var clientes = await _clienteService.ObterTodosAsync();
            ClientesDisponiveis.Clear();
            foreach (var c in clientes.OrderBy(c => c.Nome))
                ClientesDisponiveis.Add(c);

            // Carrega Serviços
            var servicos = await _servicoService.ObterTodosAsync(apenasAtivos: true);
            ServicosDisponiveis.Clear();
            foreach (var s in servicos.OrderBy(s => s.Descricao))
                ServicosDisponiveis.Add(s);

            ClienteSelecionado = null;
            ServicoSelecionadoParaAdicionar = null;
            ServicosSelecionadosParaNovo.Clear();
            NovaObservacao = string.Empty;

            NovaDataHoraTexto = $"{DataSelecionada:dd/MM/yyyy} 09:00";
            AtualizarTotaisNovoAgendamento();

            IsModalNovoAgendamentoAberto = true;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Erro ao carregar dados do agendamento: {ex.Message}", "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    public void FecharModalNovoAgendamento()
    {
        IsModalNovoAgendamentoAberto = false;
    }

    [RelayCommand]
    public void AdicionarServico()
    {
        if (ServicoSelecionadoParaAdicionar == null)
            return;

        ServicosSelecionadosParaNovo.Add(ServicoSelecionadoParaAdicionar);
        AtualizarTotaisNovoAgendamento();
        ServicoSelecionadoParaAdicionar = null;
    }

    [RelayCommand]
    public void RemoverServico(Servico servico)
    {
        if (servico == null) return;
        ServicosSelecionadosParaNovo.Remove(servico);
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
        if (ClienteSelecionado == null)
        {
            MessageBox.Show("Por favor, selecione um cliente.", "Atenção", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (ServicosSelecionadosParaNovo.Count == 0)
        {
            MessageBox.Show("Por favor, adicione pelo menos um serviço ao agendamento.", "Atenção", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        DateTime dataHoraAgendamento;
        if (!DateTime.TryParseExact(
                NovaDataHoraTexto?.Trim(), 
                new[] { "dd/MM/yyyy HH:mm", "dd/MM/yyyy H:mm", "dd/MM/yyyy", "yyyy-MM-dd HH:mm" }, 
                CultureInfo.InvariantCulture, 
                DateTimeStyles.None, 
                out dataHoraAgendamento))
        {
            dataHoraAgendamento = DataSelecionada.Date.AddHours(9);
        }

        try
        {
            var servicoIds = ServicosSelecionadosParaNovo.Select(s => s.Id).ToList();
            await _agendamentoService.CriarAgendamentoAsync(
                ClienteSelecionado.Id,
                dataHoraAgendamento,
                servicoIds,
                NovaObservacao);

            IsModalNovoAgendamentoAberto = false;
            DataSelecionada = dataHoraAgendamento.Date;
            await CarregarAgendamentosAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Não foi possível salvar o agendamento: {ex.Message}", "Erro ao Salvar", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
