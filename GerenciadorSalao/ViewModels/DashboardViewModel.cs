using System.Globalization;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GerenciadorSalao.Helpers;
using GerenciadorSalao.Models;
using GerenciadorSalao.Models.DTOs;
using GerenciadorSalao.Services;

namespace GerenciadorSalao.ViewModels;

public class TopClienteDashboardItem
{
    public string Posicao { get; set; } = "01";
    public string Iniciais { get; set; } = string.Empty;
    public string Nome { get; set; } = string.Empty;
    public string Telefone { get; set; } = string.Empty;
    public string AtendimentosTexto { get; set; } = string.Empty;
    public string AvatarBg { get; set; } = "#FEE9E6";
}

public class TopServicoDashboardItem
{
    public string Posicao { get; set; } = "01";
    public string Nome { get; set; } = string.Empty;
    public int AtendimentosCount { get; set; }
    public string AtendimentosTexto { get; set; } = string.Empty;
    public string DuracaoTexto { get; set; } = string.Empty;
    public double Percentual { get; set; }
    public string PercentualTexto => $"{Percentual:0.#}%";
}

public class AgendaItemDashboard
{
    public int Id { get; set; }
    public string Horario { get; set; } = string.Empty;
    public string Iniciais { get; set; } = string.Empty;
    public string NomeCliente { get; set; } = string.Empty;
    public string Servicos { get; set; } = string.Empty;
    public string Duracao { get; set; } = string.Empty;
    public string Status { get; set; } = "Agendado";
}

public partial class DashboardViewModel : ObservableObject
{
    private readonly IRelatorioService _relatorioService;
    private readonly IAgendamentoService _agendamentoService;
    private readonly IComandaService _comandaService;
    private readonly IClienteService _clienteService;

    public Action<string, object?>? NavigationRequested { get; set; }

    // Cards de Métricas Operacionais (Livre de Informações Sigilosas Financeiras)
    [ObservableProperty]
    private int _comandasFinalizadasHoje;

    [ObservableProperty]
    private int _comandasAbertas;

    [ObservableProperty]
    private int _agendamentosHoje;

    [ObservableProperty]
    private int _agendamentosPendentesHoje;

    [ObservableProperty]
    private int _totalClientes;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DataHojeFormatada))]
    [NotifyPropertyChangedFor(nameof(IsHoje))]
    [NotifyPropertyChangedFor(nameof(TituloAtendimentosDia))]
    [NotifyPropertyChangedFor(nameof(TituloAgendamentosDia))]
    [NotifyPropertyChangedFor(nameof(TituloAgendaTabela))]
    private DateTime _dataSelecionada = DateTime.Today;

    [ObservableProperty]
    private DateTime _mesVisualizado = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);

    [ObservableProperty]
    private string _mesVisualizadoTexto = string.Empty;

    [ObservableProperty]
    private bool _isCalendarOpen;

    [ObservableProperty]
    private List<CalendarDayItem> _diasCalendario = new();

    partial void OnDataSelecionadaChanged(DateTime value)
    {
        AtualizarDiasCalendario();
        _ = CarregarDadosAsync();
    }

    partial void OnIsCalendarOpenChanged(bool value)
    {
        if (value)
        {
            MesVisualizado = new DateTime(DataSelecionada.Year, DataSelecionada.Month, 1);
            AtualizarDiasCalendario();
        }
    }

    [ObservableProperty]
    private bool _carregando;

    // Agenda do Dia Selecionado
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TemAgendamentos))]
    [NotifyPropertyChangedFor(nameof(SemAgendamentos))]
    private List<AgendaItemDashboard> _agendaDoDia = new();

    // Top 5 Clientes Mais Frequentes (Fidelidade / Mais Atendimentos)
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TemTopClientes))]
    [NotifyPropertyChangedFor(nameof(SemTopClientes))]
    private List<TopClienteDashboardItem> _topClientes = new();

    // Serviços Mais Procurados
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TemTopServicos))]
    [NotifyPropertyChangedFor(nameof(SemTopServicos))]
    private List<TopServicoDashboardItem> _topServicos = new();

    [ObservableProperty]
    private int _totalServicosRealizados;

    // Propriedades Computadas para Bindings da UI
    public bool TemAgendamentos => AgendaDoDia.Count > 0;
    public bool SemAgendamentos => AgendaDoDia.Count == 0;
    public bool TemTopClientes => TopClientes.Count > 0;
    public bool SemTopClientes => TopClientes.Count == 0;
    public bool TemTopServicos => TopServicos.Count > 0;
    public bool SemTopServicos => TopServicos.Count == 0;

    public bool IsHoje => DataSelecionada.Date == DateTime.Today;
    public string DataHojeFormatada => DataSelecionada.ToString("dd/MM/yyyy");
    public string TituloAtendimentosDia => IsHoje ? "Atendimentos de hoje" : "Atendimentos do dia";
    public string TituloAgendamentosDia => IsHoje ? "Agendamentos hoje" : "Agendamentos do dia";
    public string TituloAgendaTabela => IsHoje ? "Agenda de hoje" : "Agenda do dia";

    public DashboardViewModel(
        IRelatorioService relatorioService,
        IAgendamentoService agendamentoService,
        IComandaService comandaService,
        IClienteService clienteService)
    {
        _relatorioService = relatorioService;
        _agendamentoService = agendamentoService;
        _comandaService = comandaService;
        _clienteService = clienteService;

        MesVisualizado = new DateTime(DataSelecionada.Year, DataSelecionada.Month, 1);
        AtualizarDiasCalendario();

        _ = CarregarDadosAsync();
    }

    [RelayCommand]
    public async Task CarregarDadosAsync()
    {
        if (Carregando) return;
        Carregando = true;

        try
        {
            var inicioDia = DataSelecionada.Date;
            var fimDia = DataSelecionada.Date.AddDays(1).AddTicks(-1);

            // 1. Comandas do dia selecionado
            var todasComandas = await _comandaService.ObterTodasAsync();
            var comandasDoDia = todasComandas
                .Where(c => (c.DataFechamento ?? c.DataAbertura) >= inicioDia && (c.DataFechamento ?? c.DataAbertura) <= fimDia)
                .ToList();

            ComandasFinalizadasHoje = comandasDoDia.Count(c => c.Status == StatusComanda.Finalizada);

            // 2. Comandas abertas no salão
            ComandasAbertas = todasComandas.Count(c => c.Status == StatusComanda.Aberta);

            // 3. Agendamentos do dia selecionado
            var agendamentosHojeLista = await _agendamentoService.ObterPorPeriodoAsync(inicioDia, fimDia);
            AgendamentosHoje = agendamentosHojeLista.Count;
            AgendamentosPendentesHoje = agendamentosHojeLista.Count(a => a.Status == StatusAgendamento.Pendente);

            // 4. Total de clientes cadastrados
            var todosClientes = await _clienteService.ObterTodosAsync();
            TotalClientes = todosClientes.Count;

            // 5. Top Serviços Mais Populares (Operacional)
            var servicosPopulares = await _relatorioService.ObterServicosMaisPopularesAsync(top: 5);
            var topServicosList = new List<TopServicoDashboardItem>();
            for (int i = 0; i < servicosPopulares.Count; i++)
            {
                var s = servicosPopulares[i];
                topServicosList.Add(new TopServicoDashboardItem
                {
                    Posicao = (i + 1).ToString("D2"),
                    Nome = s.NomeServico,
                    AtendimentosCount = s.QuantidadeRealizada,
                    AtendimentosTexto = s.QuantidadeRealizada == 1 ? "1 realização" : $"{s.QuantidadeRealizada} realizações",
                    DuracaoTexto = s.DuracaoMediaMinutos > 0 ? ValidationHelper.FormatarDuracao(s.DuracaoMediaMinutos) : "Atendimento",
                    Percentual = s.Percentual
                });
            }
            TopServicos = topServicosList;
            TotalServicosRealizados = servicosPopulares.Sum(s => s.QuantidadeRealizada);

            // 6. Top 5 Clientes Mais Frequentes no Salão (Mais Atendimentos/Fidelidade)
            var topFrequentes = await _relatorioService.ObterTopClientesFrequentesAsync(top: 5);
            var avatarColors = new[] { "#FCE4EC", "#FFF3E0", "#E1F5FE", "#F3E5F5", "#E8F5E9" };

            var topClientesList = new List<TopClienteDashboardItem>();
            for (int i = 0; i < topFrequentes.Count; i++)
            {
                var tc = topFrequentes[i];
                var nome = tc.NomeCliente;
                var iniciais = ObterIniciais(nome);

                topClientesList.Add(new TopClienteDashboardItem
                {
                    Posicao = (i + 1).ToString("D2"),
                    Iniciais = iniciais,
                    Nome = nome,
                    Telefone = tc.Telefone ?? string.Empty,
                    AtendimentosTexto = tc.QuantidadeComandas == 1 ? "1 atendimento" : $"{tc.QuantidadeComandas} atendimentos",
                    AvatarBg = avatarColors[i % avatarColors.Length]
                });
            }
            TopClientes = topClientesList;

            // 7. Lista detalhada de agendamentos reais do dia (SEM dados sigilosos de valores)
            var agendaList = new List<AgendaItemDashboard>();
            foreach (var ag in agendamentosHojeLista.OrderBy(a => a.DataHora))
            {
                var nomeCli = ag.Cliente?.NomeExibicao ?? "Cliente avulso";
                var servicos = string.Join(" + ", ag.AgendamentoServicos.Select(s => s.Servico?.NomeExibicao ?? s.Servico?.Descricao ?? "Serviço"));
                if (string.IsNullOrWhiteSpace(servicos)) servicos = "Atendimento geral";

                string statusFormatado = ag.Status switch
                {
                    StatusAgendamento.Pendente => "Pendente",
                    StatusAgendamento.Finalizado => "Finalizado",
                    StatusAgendamento.Cancelado => "Cancelado",
                    _ => ag.Status.ToString()
                };

                agendaList.Add(new AgendaItemDashboard
                {
                    Id = ag.Id,
                    Horario = ag.DataHora.ToString("HH:mm"),
                    Iniciais = ObterIniciais(nomeCli),
                    NomeCliente = nomeCli,
                    Servicos = servicos,
                    Duracao = ValidationHelper.FormatarDuracao(ag.TempoTotalPrevistoMinutos),
                    Status = statusFormatado
                });
            }
            AgendaDoDia = agendaList;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[DashboardViewModel] Erro ao carregar dados: {ex}");
        }
        finally
        {
            Carregando = false;
        }
    }

    private static string ObterIniciais(string nomeCompleto)
    {
        if (string.IsNullOrWhiteSpace(nomeCompleto)) return "CL";
        var partes = nomeCompleto.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (partes.Length == 1) return partes[0][..Math.Min(2, partes[0].Length)].ToUpper();
        return $"{partes[0][0]}{partes[^1][0]}".ToUpper();
    }

    // ==========================================
    // CONTROLES DO MINI-CALENDÁRIO FLUTUANTE
    // ==========================================
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

    // Comandos de Navegação de Datas
    [RelayCommand]
    public void IrParaDiaAnterior() => DataSelecionada = DataSelecionada.AddDays(-1);

    [RelayCommand]
    public void IrParaProximoDia() => DataSelecionada = DataSelecionada.AddDays(1);

    [RelayCommand]
    public void IrParaHoje() => DataSelecionada = DateTime.Today;

    // Comandos de Navegação Entre Telas
    [RelayCommand]
    public void IrParaAgenda() => NavigationRequested?.Invoke("Agendamentos", DataSelecionada);

    [RelayCommand]
    public void NovoAgendamento() => NavigationRequested?.Invoke("Agendamentos", ("novo", DataSelecionada));

    [RelayCommand]
    public void AbrirAtendimento(AgendaItemDashboard? item)
    {
        if (item == null) return;
        NavigationRequested?.Invoke("Agendamentos", item.Id);
    }

    [RelayCommand]
    public void IrParaComandas() => NavigationRequested?.Invoke("Comandas", null);

    [RelayCommand]
    public void IrParaClientes() => NavigationRequested?.Invoke("Clientes", null);

    [RelayCommand]
    public void IrParaRelatorios() => NavigationRequested?.Invoke("Relatorios", null);

    [RelayCommand]
    public void IrParaServicos() => NavigationRequested?.Invoke("Servicos", null);

    public void AtualizarDiasCalendario()
    {
        MesVisualizadoTexto = MesVisualizado.ToString("MMMM 'de' yyyy", new CultureInfo("pt-BR"));
        var dias = new List<CalendarDayItem>();
        var primeiroDiaDoMes = new DateTime(MesVisualizado.Year, MesVisualizado.Month, 1);

        int diff = (int)primeiroDiaDoMes.DayOfWeek; // 0 = Domingo
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
}
