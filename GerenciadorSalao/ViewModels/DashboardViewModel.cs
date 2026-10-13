using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GerenciadorSalao.Models;
using GerenciadorSalao.Models.DTOs;
using GerenciadorSalao.Services;

namespace GerenciadorSalao.ViewModels;

public class TopClienteDashboardItem
{
    public string Posicao { get; set; } = "01";
    public string Iniciais { get; set; } = string.Empty;
    public string Nome { get; set; } = string.Empty;
    public string AtendimentosTexto { get; set; } = string.Empty;
    public string TotalGastoTexto { get; set; } = string.Empty;
    public string AvatarBg { get; set; } = "#FEE9E6";
}

public class AgendaItemDashboard
{
    public int Id { get; set; }
    public string Horario { get; set; } = string.Empty;
    public string Iniciais { get; set; } = string.Empty;
    public string NomeCliente { get; set; } = string.Empty;
    public string Servicos { get; set; } = string.Empty;
    public string Duracao { get; set; } = string.Empty;
    public string Valor { get; set; } = string.Empty;
    public string Status { get; set; } = "Agendado";
}

public partial class DashboardViewModel : ObservableObject
{
    private readonly IRelatorioService _relatorioService;
    private readonly IAgendamentoService _agendamentoService;
    private readonly IComandaService _comandaService;
    private readonly IClienteService _clienteService;

    public Action<string>? NavigationRequested { get; set; }

    [ObservableProperty]
    private decimal _faturamentoHoje;

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
    private DateTime _dataSelecionada = DateTime.Today;

    [ObservableProperty]
    private int _anoSelecionado = DateTime.Today.Year;

    [ObservableProperty]
    private List<int> _anosDisponiveis = new() { 2024, 2025, 2026 };

    [ObservableProperty]
    private decimal _faturamentoAcumuladoAno;

    [ObservableProperty]
    private bool _carregando;

    [ObservableProperty]
    private List<AgendaItemDashboard> _agendaDoDia = new();

    [ObservableProperty]
    private List<TopClienteDashboardItem> _topClientes = new();

    public string DataHojeFormatada => DataSelecionada.ToString("dd/MM/yyyy");

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

        _ = CarregarDadosAsync();
    }

    [RelayCommand]
    public async Task CarregarDadosAsync()
    {
        try
        {
            Carregando = true;

            var inicioDia = DataSelecionada.Date;
            var fimDia = inicioDia.AddDays(1).AddTicks(-1);

            // 1. Faturamento do dia
            FaturamentoHoje = await _relatorioService.ObterFaturamentoTotalPeriodoAsync(inicioDia, fimDia);

            // 2. Comandas abertas e finalizadas
            var todasComandas = await _comandaService.ObterTodasAsync();
            ComandasAbertas = todasComandas.Count(c => c.Status == StatusComanda.Aberta);
            ComandasFinalizadasHoje = todasComandas.Count(c => c.Status == StatusComanda.Finalizada && c.DataFechamento?.Date == inicioDia);

            // 3. Agendamentos de hoje
            var agendamentosHojeLista = await _agendamentoService.ObterPorPeriodoAsync(inicioDia, fimDia);
            AgendamentosHoje = agendamentosHojeLista.Count;
            AgendamentosPendentesHoje = agendamentosHojeLista.Count(a => a.Status == StatusAgendamento.Pendente);

            // 4. Total de clientes cadastrados
            var todosClientes = await _clienteService.ObterTodosAsync();
            TotalClientes = todosClientes.Count;

            // 5. Faturamento acumulado do ano selecionado
            var faturamentoMensal = await _relatorioService.ObterFaturamentoMensalPorAnoAsync(AnoSelecionado);
            FaturamentoAcumuladoAno = faturamentoMensal.Sum(f => f.Total);

            // 6. Top 5 Clientes formatados com iniciais e cores
            var topRaw = await _relatorioService.ObterTopClientesAsync(5, AnoSelecionado);
            var avatarColors = new[] { "#FCE4EC", "#FFF3E0", "#E1F5FE", "#F3E5F5", "#E8F5E9" };

            var topList = new List<TopClienteDashboardItem>();
            for (int i = 0; i < topRaw.Count; i++)
            {
                var item = topRaw[i];
                var nome = item.NomeCliente;
                var iniciais = ObterIniciais(nome);

                topList.Add(new TopClienteDashboardItem
                {
                    Posicao = (i + 1).ToString("D2"),
                    Iniciais = iniciais,
                    Nome = nome,
                    AtendimentosTexto = $"{item.QuantidadeComandas} atendimentos",
                    TotalGastoTexto = item.TotalGasto.ToString("C2"),
                    AvatarBg = avatarColors[i % avatarColors.Length]
                });
            }
            TopClientes = topList;

            // 7. Lista da Agenda do Dia
            var agendaList = new List<AgendaItemDashboard>();
            foreach (var ag in agendamentosHojeLista)
            {
                var nomeCli = ag.Cliente?.Nome ?? "Cliente";
                var servicos = string.Join(" + ", ag.AgendamentoServicos.Select(s => s.Servico?.Descricao ?? "Serviço"));
                if (string.IsNullOrWhiteSpace(servicos)) servicos = "Atendimento geral";

                var valorTotal = ag.AgendamentoServicos.Sum(s => s.Servico?.Valor ?? 0);

                agendaList.Add(new AgendaItemDashboard
                {
                    Id = ag.Id,
                    Horario = ag.DataHora.ToString("HH:mm"),
                    Iniciais = ObterIniciais(nomeCli),
                    NomeCliente = nomeCli,
                    Servicos = servicos,
                    Duracao = $"{ag.TempoTotalPrevistoMinutos} min",
                    Valor = valorTotal.ToString("C2"),
                    Status = ag.Status == StatusAgendamento.Pendente ? "Agendado" : ag.Status.ToString()
                });
            }
            AgendaDoDia = agendaList;
        }
        catch
        {
            // Proteção contra quebras na renderização
        }
        finally
        {
            Carregando = false;
        }
    }

    private static string ObterIniciais(string nome)
    {
        if (string.IsNullOrWhiteSpace(nome)) return "KB";
        var partes = nome.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (partes.Length == 1) return partes[0].Substring(0, Math.Min(2, partes[0].Length)).ToUpper();
        return $"{partes[0][0]}{partes[^1][0]}".ToUpper();
    }

    [RelayCommand]
    public void IrParaAgenda() => NavigationRequested?.Invoke("Agendamentos");

    [RelayCommand]
    public void NovoAgendamento() => NavigationRequested?.Invoke("Agendamentos");

    [RelayCommand]
    public void IrParaComandas() => NavigationRequested?.Invoke("Comandas");

    [RelayCommand]
    public void IrParaClientes() => NavigationRequested?.Invoke("Clientes");

    [RelayCommand]
    public void IrParaRelatorios() => NavigationRequested?.Invoke("Relatorios");
}
