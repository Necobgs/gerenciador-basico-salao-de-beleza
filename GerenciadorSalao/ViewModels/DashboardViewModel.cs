using System.Windows;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GerenciadorSalao.Helpers;
using GerenciadorSalao.Models;
using GerenciadorSalao.Models.DTOs;
using GerenciadorSalao.Services;

namespace GerenciadorSalao.ViewModels;

public class ChartMesItem
{
    public int Mes { get; set; }
    public string MesNome { get; set; } = string.Empty;
    public decimal Valor { get; set; }
    public string ValorFormatado { get; set; } = string.Empty;
    public string TooltipTexto { get; set; } = string.Empty;
    public double X { get; set; }
    public double Y { get; set; }
    public bool TemValor => Valor > 0;
}

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
    [NotifyPropertyChangedFor(nameof(DataHojeFormatada))]
    [NotifyPropertyChangedFor(nameof(IsHoje))]
    [NotifyPropertyChangedFor(nameof(TituloFaturamentoDia))]
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

    [ObservableProperty]
    private string _periodoSelecionado = "Ano completo";

    [ObservableProperty]
    private List<string> _periodosDisponiveis = new() { "Ano completo", "1º Semestre", "2º Semestre" };

    [ObservableProperty]
    private int _anoSelecionado = DateTime.Today.Year;

    [ObservableProperty]
    private List<int> _anosDisponiveis = new() { DateTime.Today.Year };

    [ObservableProperty]
    private decimal _faturamentoAcumuladoAno;

    [ObservableProperty]
    private string _periodoTagDescricao = "Acumulado do ano";

    partial void OnPeriodoSelecionadoChanged(string value) => _ = CarregarDadosAsync();
    partial void OnAnoSelecionadoChanged(int value) => _ = CarregarDadosAsync();

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

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TemAgendamentos))]
    [NotifyPropertyChangedFor(nameof(SemAgendamentos))]
    private List<AgendaItemDashboard> _agendaDoDia = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TemTopClientes))]
    [NotifyPropertyChangedFor(nameof(SemTopClientes))]
    private List<TopClienteDashboardItem> _topClientes = new();

    // Propriedades do Gráfico Dinâmico
    [ObservableProperty]
    private Geometry? _graficoLinhaGeometry;

    [ObservableProperty]
    private Geometry? _graficoAreaGeometry;

    [ObservableProperty]
    private List<ChartMesItem> _chartMeses = new();

    [ObservableProperty]
    private string _yLabelMax = "R$ 0";

    [ObservableProperty]
    private string _yLabel75 = "R$ 0";

    [ObservableProperty]
    private string _yLabel50 = "R$ 0";

    [ObservableProperty]
    private string _yLabel25 = "R$ 0";

    [ObservableProperty]
    private string _yLabel0 = "R$ 0";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SemDadosNoGrafico))]
    private bool _temDadosNoGrafico;

    [ObservableProperty]
    private int _colunasGrafico = 12;

    // Propriedades Computadas para Bindings da UI
    public bool SemDadosNoGrafico => !TemDadosNoGrafico;
    public bool TemAgendamentos => AgendaDoDia.Count > 0;
    public bool SemAgendamentos => AgendaDoDia.Count == 0;
    public bool TemTopClientes => TopClientes.Count > 0;
    public bool SemTopClientes => TopClientes.Count == 0;
    public bool IsHoje => DataSelecionada.Date == DateTime.Today;
    public string DataHojeFormatada => DataSelecionada.ToString("dd/MM/yyyy");
    public string TituloFaturamentoDia => IsHoje ? "Faturamento de hoje" : "Faturamento do dia";
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
        try
        {
            Carregando = true;

            var inicioDia = DataSelecionada.Date;
            var fimDia = inicioDia.AddDays(1).AddTicks(-1);

            // 1. Faturamento do dia selecionado
            FaturamentoHoje = await _relatorioService.ObterFaturamentoTotalPeriodoAsync(inicioDia, fimDia);

            // 2. Comandas abertas e finalizadas do dia
            var todasComandas = await _comandaService.ObterTodasAsync();
            ComandasAbertas = todasComandas.Count(c => c.Status == StatusComanda.Aberta);
            ComandasFinalizadasHoje = todasComandas.Count(c => c.Status == StatusComanda.Finalizada && c.DataFechamento?.Date == inicioDia);

            // Atualiza lista de anos disponíveis com base no banco e ano atual
            var anosSet = new SortedSet<int> { DateTime.Today.Year, DateTime.Today.Year - 1, DateTime.Today.Year - 2 };
            foreach (var c in todasComandas)
            {
                var dt = c.DataFechamento ?? c.DataAbertura;
                anosSet.Add(dt.Year);
            }
            var anosOrdenados = anosSet.Reverse().ToList();
            if (!AnosDisponiveis.SequenceEqual(anosOrdenados))
            {
                AnosDisponiveis = anosOrdenados;
            }
            if (!AnosDisponiveis.Contains(AnoSelecionado))
            {
                AnoSelecionado = AnosDisponiveis[0];
            }

            // 3. Agendamentos do dia selecionado
            var agendamentosHojeLista = await _agendamentoService.ObterPorPeriodoAsync(inicioDia, fimDia);
            AgendamentosHoje = agendamentosHojeLista.Count;
            AgendamentosPendentesHoje = agendamentosHojeLista.Count(a => a.Status == StatusAgendamento.Pendente);

            // 4. Total de clientes cadastrados
            var todosClientes = await _clienteService.ObterTodosAsync();
            TotalClientes = todosClientes.Count;

            // 5. Faturamento mensal e acumulado do ano selecionado via RelatorioService
            var faturamentoMensal = await _relatorioService.ObterFaturamentoMensalPorAnoAsync(AnoSelecionado);
            List<FaturamentoMensalDto> dadosPeriodo;

            if (PeriodoSelecionado == "1º Semestre")
            {
                dadosPeriodo = faturamentoMensal.Where(f => f.Mes <= 6).ToList();
                PeriodoTagDescricao = $"1º Semestre de {AnoSelecionado}";
            }
            else if (PeriodoSelecionado == "2º Semestre")
            {
                dadosPeriodo = faturamentoMensal.Where(f => f.Mes >= 7).ToList();
                PeriodoTagDescricao = $"2º Semestre de {AnoSelecionado}";
            }
            else
            {
                dadosPeriodo = faturamentoMensal.ToList();
                PeriodoTagDescricao = $"Acumulado de {AnoSelecionado}";
            }

            FaturamentoAcumuladoAno = dadosPeriodo.Sum(f => f.Total);

            // 5.1 Atualiza Geometria e Dados Dinâmicos do Gráfico
            AtualizarGrafico(dadosPeriodo);

            // 6. Top 5 Clientes formatados com iniciais e cores a partir do RelatorioService
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
                    AtendimentosTexto = $"{item.QuantidadeComandas} atendimento{(item.QuantidadeComandas > 1 ? "s" : "")}",
                    TotalGastoTexto = item.TotalGasto.ToString("C2"),
                    AvatarBg = avatarColors[i % avatarColors.Length]
                });
            }
            TopClientes = topList;

            // 7. Lista detalhada de agendamentos reais do dia selecionado
            var agendaList = new List<AgendaItemDashboard>();
            foreach (var ag in agendamentosHojeLista.OrderBy(a => a.DataHora))
            {
                var nomeCli = ag.Cliente?.NomeExibicao ?? "Cliente avulso";
                var servicos = string.Join(" + ", ag.AgendamentoServicos.Select(s => s.Servico?.NomeExibicao ?? s.Servico?.Descricao ?? "Serviço"));
                if (string.IsNullOrWhiteSpace(servicos)) servicos = "Atendimento geral";

                var valorTotal = ag.Comanda != null && ag.Comanda.ValorTotal > 0
                    ? ag.Comanda.ValorTotal
                    : ag.AgendamentoServicos.Sum(s => s.Servico?.Valor ?? 0);

                string statusFormatado = ag.Status switch
                {
                    StatusAgendamento.Pendente => "Agendado",
                    StatusAgendamento.Concluido => "Concluído",
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
                    Valor = valorTotal.ToString("C2"),
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

    private static readonly string[] NomesMeses = 
        { "Jan", "Fev", "Mar", "Abr", "Mai", "Jun", "Jul", "Ago", "Set", "Out", "Nov", "Dez" };
    private static readonly string[] NomesMesesCompletos = 
        { "Janeiro", "Fevereiro", "Março", "Abril", "Maio", "Junho", "Julho", "Agosto", "Setembro", "Outubro", "Novembro", "Dezembro" };

    private void AtualizarGrafico(List<FaturamentoMensalDto> dados)
    {
        ColunasGrafico = dados.Count;
        var maxValor = dados.Count > 0 ? dados.Max(d => d.Total) : 0m;
        TemDadosNoGrafico = maxValor > 0;

        decimal teto = CalcularTeto(maxValor);

        // Labels do Eixo Y
        YLabelMax = FormatarValorEixo(teto);
        YLabel75 = FormatarValorEixo(teto * 0.75m);
        YLabel50 = FormatarValorEixo(teto * 0.50m);
        YLabel25 = FormatarValorEixo(teto * 0.25m);
        YLabel0 = "R$ 0";

        // Coordenadas de renderização no Viewbox (Width = 500, Height = 140)
        // Baseline no Y = 125, topo da curva em Y = 12, altura útil = 113
        const double baselineY = 125.0;
        const double alturaUtil = 113.0;
        const double margemEsquerda = 25.0;
        const double larguraUtil = 450.0;

        int n = dados.Count;
        double stepX = n > 1 ? larguraUtil / (n - 1) : larguraUtil;

        var pontos = new List<Point>();
        var mesesItems = new List<ChartMesItem>();

        for (int i = 0; i < n; i++)
        {
            var item = dados[i];
            int mesIdx = Math.Clamp(item.Mes - 1, 0, 11);

            double x = margemEsquerda + (i * stepX);
            double ratio = teto > 0 ? (double)(item.Total / teto) : 0.0;
            double y = baselineY - (ratio * alturaUtil);

            pontos.Add(new Point(x, y));

            mesesItems.Add(new ChartMesItem
            {
                Mes = item.Mes,
                MesNome = NomesMeses[mesIdx],
                Valor = item.Total,
                ValorFormatado = item.Total.ToString("C2"),
                TooltipTexto = $"{NomesMesesCompletos[mesIdx]}/{AnoSelecionado}: {item.Total:C2}",
                X = x,
                Y = y
            });
        }

        ChartMeses = mesesItems;

        var (linha, area) = GerarGeometriasGrafico(pontos, baselineY);
        GraficoLinhaGeometry = linha;
        GraficoAreaGeometry = area;
    }

    private static (Geometry Linha, Geometry Area) GerarGeometriasGrafico(List<Point> pontos, double baselineY)
    {
        if (pontos.Count == 0)
        {
            return (Geometry.Empty, Geometry.Empty);
        }

        var linhaFig = new PathFigure { StartPoint = pontos[0], IsClosed = false, IsFilled = false };
        var areaFig = new PathFigure { StartPoint = new Point(pontos[0].X, baselineY), IsClosed = true, IsFilled = true };
        areaFig.Segments.Add(new LineSegment(pontos[0], true));

        if (pontos.Count == 1)
        {
            linhaFig.Segments.Add(new LineSegment(pontos[0], true));
            areaFig.Segments.Add(new LineSegment(new Point(pontos[0].X, baselineY), true));
        }
        else
        {
            for (int i = 0; i < pontos.Count - 1; i++)
            {
                var p0 = i > 0 ? pontos[i - 1] : pontos[i];
                var p1 = pontos[i];
                var p2 = pontos[i + 1];
                var p3 = (i + 2 < pontos.Count) ? pontos[i + 2] : p2;

                // Catmull-Rom para Bezier Cúbica:
                // CP1 = P1 + (P2 - P0) / 6
                // CP2 = P2 - (P3 - P1) / 6
                var cp1 = new Point(
                    p1.X + (p2.X - p0.X) / 6.0,
                    Math.Clamp(p1.Y + (p2.Y - p0.Y) / 6.0, 8.0, baselineY)
                );
                var cp2 = new Point(
                    p2.X - (p3.X - p1.X) / 6.0,
                    Math.Clamp(p2.Y - (p3.Y - p1.Y) / 6.0, 8.0, baselineY)
                );

                var bezier = new BezierSegment(cp1, cp2, p2, true);
                linhaFig.Segments.Add(bezier);
                areaFig.Segments.Add(bezier);
            }

            areaFig.Segments.Add(new LineSegment(new Point(pontos[^1].X, baselineY), true));
        }

        var linhaGeom = new PathGeometry();
        linhaGeom.Figures.Add(linhaFig);
        linhaGeom.Freeze();

        var areaGeom = new PathGeometry();
        areaGeom.Figures.Add(areaFig);
        areaGeom.Freeze();

        return (linhaGeom, areaGeom);
    }

    private static decimal CalcularTeto(decimal maxValor)
    {
        if (maxValor <= 0) return 1000m;
        if (maxValor <= 500) return 500m;
        if (maxValor <= 1000) return 1000m;
        if (maxValor <= 2000) return 2000m;
        if (maxValor <= 3000) return 3000m;
        if (maxValor <= 5000) return 5000m;
        if (maxValor <= 8000) return 8000m;
        if (maxValor <= 10000) return 10000m;
        if (maxValor <= 15000) return 15000m;
        if (maxValor <= 20000) return 20000m;
        if (maxValor <= 30000) return 30000m;
        if (maxValor <= 50000) return 50000m;

        return Math.Ceiling(maxValor / 10000m) * 10000m;
    }

    private static string FormatarValorEixo(decimal valor)
    {
        if (valor <= 0) return "R$ 0";
        if (valor >= 1000)
        {
            var mil = valor / 1000m;
            return mil % 1 == 0 
                ? $"{mil:0} mil" 
                : $"{mil:0.#} mil";
        }
        return valor.ToString("C0");
    }

    private static string ObterIniciais(string nome)
    {
        if (string.IsNullOrWhiteSpace(nome)) return "KB";
        var partes = nome.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (partes.Length == 1) return partes[0].Substring(0, Math.Min(2, partes[0].Length)).ToUpper();
        return $"{partes[0][0]}{partes[^1][0]}".ToUpper();
    }

    public void AtualizarDiasCalendario()
    {
        MesVisualizadoTexto = MesVisualizado.ToString("MMMM 'de' yyyy", new System.Globalization.CultureInfo("pt-BR"));

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
