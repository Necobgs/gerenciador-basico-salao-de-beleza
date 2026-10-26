using System.Globalization;
using System.Windows;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GerenciadorSalao.Helpers;
using GerenciadorSalao.Models.DTOs;
using GerenciadorSalao.Services;

namespace GerenciadorSalao.ViewModels;

public class TopClienteFinanceiroItem
{
    public string Posicao { get; set; } = "01";
    public string Iniciais { get; set; } = string.Empty;
    public string Nome { get; set; } = string.Empty;
    public string Telefone { get; set; } = string.Empty;
    public string AtendimentosTexto { get; set; } = string.Empty;
    public string TotalGastoTexto { get; set; } = string.Empty;
    public string AvatarBg { get; set; } = "#FEE9E6";
}

public partial class RelatoriosViewModel : ObservableObject
{
    private readonly IRelatorioService _relatorioService;
    private static readonly CultureInfo PtBrCulture = new("pt-BR");

    [ObservableProperty]
    private string _titulo = "Relatórios Financeiros";

    [ObservableProperty]
    private string _subtitulo = "Visão consolidada de faturamento, ticket médio e clientes que mais geram receita.";

    [ObservableProperty]
    private int _anoSelecionado = DateTime.Today.Year;

    [ObservableProperty]
    private List<int> _anosDisponiveis = new() { DateTime.Today.Year };

    [ObservableProperty]
    private string _periodoSelecionado = "Ano completo";

    [ObservableProperty]
    private List<string> _periodosDisponiveis = new() { "Ano completo", "1º Semestre", "2º Semestre" };

    [ObservableProperty]
    private string _periodoTagDescricao = "Acumulado do ano";

    partial void OnPeriodoSelecionadoChanged(string value) => _ = CarregarDadosAsync();
    partial void OnAnoSelecionadoChanged(int value) => _ = CarregarDadosAsync();

    [ObservableProperty]
    private bool _carregando;

    // Métricas Financeiras Principais
    [ObservableProperty]
    private decimal _faturamentoTotal;

    [ObservableProperty]
    private string _faturamentoTotalTexto = "R$ 0,00";

    [ObservableProperty]
    private decimal _ticketMedio;

    [ObservableProperty]
    private string _ticketMedioTexto = "R$ 0,00";

    [ObservableProperty]
    private int _totalAtendimentos;

    [ObservableProperty]
    private string _totalAtendimentosTexto = "0 atendimentos";

    [ObservableProperty]
    private decimal _mediaMensal;

    [ObservableProperty]
    private string _mediaMensalTexto = "R$ 0,00";

    // Gráfico de Linha / Área Dinâmico
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

    public bool SemDadosNoGrafico => !TemDadosNoGrafico;

    // Tabela Mês a Mês
    [ObservableProperty]
    private List<FaturamentoDetalhadoMesDto> _mesesDetalhados = new();

    // Top 5 Clientes por Receita
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TemTopClientes))]
    [NotifyPropertyChangedFor(nameof(SemTopClientes))]
    private List<TopClienteFinanceiroItem> _topClientes = new();

    public bool TemTopClientes => TopClientes.Count > 0;
    public bool SemTopClientes => TopClientes.Count == 0;

    public RelatoriosViewModel(IRelatorioService relatorioService)
    {
        _relatorioService = relatorioService;
        var anoAtual = DateTime.Today.Year;
        AnosDisponiveis = new List<int> { anoAtual, anoAtual - 1, anoAtual - 2 };
        AnoSelecionado = anoAtual;

        _ = CarregarDadosAsync();
    }

    [RelayCommand]
    public async Task CarregarDadosAsync()
    {
        if (Carregando) return;
        Carregando = true;

        try
        {
            var dadosAno = await _relatorioService.ObterFaturamentoMensalPorAnoAsync(AnoSelecionado);

            // Filtragem pelo Período selecionado
            List<FaturamentoMensalDto> dadosFiltrados;
            // 1. Tabela Detalhada Mês a Mês
            var detalhados = await _relatorioService.ObterFaturamentoDetalhadoMesesAsync(AnoSelecionado);
            if (PeriodoSelecionado == "1º Semestre")
            {
                dadosFiltrados = dadosAno.Where(d => d.Mes <= 6).ToList();
                detalhados = detalhados.Where(d => d.Mes <= 6).ToList();
                PeriodoTagDescricao = "1º Semestre (Jan a Jun)";
            }
            else if (PeriodoSelecionado == "2º Semestre")
            {
                dadosFiltrados = dadosAno.Where(d => d.Mes >= 7).ToList();
                detalhados = detalhados.Where(d => d.Mes >= 7).ToList();
                PeriodoTagDescricao = "2º Semestre (Jul a Dez)";
            }
            else
            {
                dadosFiltrados = dadosAno;
                PeriodoTagDescricao = "Ano completo (Jan a Dez)";
            }

            _dadosFiltrados = dadosFiltrados;
            _detalhadosFiltrados = detalhados;
            MesesDetalhados = detalhados;

            // 2. Gráfico Dinâmico de Faturamento (Adaptativo à largura real da tela)
            AtualizarGrafico(_dadosFiltrados, _detalhadosFiltrados, _larguraGraficoAtual);

            // 3. Totais e Médias
            FaturamentoTotal = dadosFiltrados.Sum(d => d.Total);
            FaturamentoTotalTexto = FaturamentoTotal.ToString("C2", PtBrCulture);

            TotalAtendimentos = detalhados.Sum(d => d.TotalAtendimentos);
            TotalAtendimentosTexto = TotalAtendimentos == 1 ? "1 atendimento" : $"{TotalAtendimentos} atendimentos";

            TicketMedio = TotalAtendimentos > 0 ? FaturamentoTotal / TotalAtendimentos : 0m;
            TicketMedioTexto = TicketMedio.ToString("C2", PtBrCulture);

            int mesesContados = dadosFiltrados.Count > 0 ? dadosFiltrados.Count : 1;
            MediaMensal = FaturamentoTotal / mesesContados;
            MediaMensalTexto = MediaMensal.ToString("C2", PtBrCulture);

            // Top Clientes por Faturamento
            var topList = await _relatorioService.ObterTopClientesAsync(top: 8, ano: AnoSelecionado);
            var avatarBgCores = new[] { "#FEE9E6", "#E8F5E9", "#EDE7F6", "#E1F5FE", "#FFF3E0" };

            var topItems = new List<TopClienteFinanceiroItem>();
            for (int i = 0; i < topList.Count; i++)
            {
                var tc = topList[i];
                var nomes = tc.NomeCliente.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                var iniciais = nomes.Length switch
                {
                    0 => "C",
                    1 => nomes[0][..Math.Min(2, nomes[0].Length)].ToUpper(),
                    _ => $"{nomes[0][0]}{nomes[^1][0]}".ToUpper()
                };

                topItems.Add(new TopClienteFinanceiroItem
                {
                    Posicao = (i + 1).ToString("D2"),
                    Iniciais = iniciais,
                    Nome = tc.NomeCliente,
                    Telefone = tc.Telefone ?? string.Empty,
                    AtendimentosTexto = tc.QuantidadeComandas == 1 ? "1 atendimento" : $"{tc.QuantidadeComandas} atendimentos",
                    TotalGastoTexto = tc.TotalGasto.ToString("C2", PtBrCulture),
                    AvatarBg = avatarBgCores[i % avatarBgCores.Length]
                });
            }

            TopClientes = topItems;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[RelatoriosViewModel] Erro ao carregar dados: {ex}");
        }
        finally
        {
            Carregando = false;
        }
    }

    private List<FaturamentoMensalDto> _dadosFiltrados = new();
    private List<FaturamentoDetalhadoMesDto> _detalhadosFiltrados = new();
    private double _larguraGraficoAtual = 900.0;

    /// <summary>
    /// Chamado quando a largura da tela/card é redimensionada, recalculando a distância entre os meses sem distorção.
    /// </summary>
    public void RedesenharGrafico(double larguraDisponivel)
    {
        if (larguraDisponivel <= 50) return;
        _larguraGraficoAtual = larguraDisponivel;
        if (_dadosFiltrados.Count > 0)
        {
            AtualizarGrafico(_dadosFiltrados, _detalhadosFiltrados, _larguraGraficoAtual);
        }
    }

    private static readonly string[] NomesMeses =
        { "Jan", "Fev", "Mar", "Abr", "Mai", "Jun", "Jul", "Ago", "Set", "Out", "Nov", "Dez" };
    private static readonly string[] NomesMesesCompletos =
        { "Janeiro", "Fevereiro", "Março", "Abril", "Maio", "Junho", "Julho", "Agosto", "Setembro", "Outubro", "Novembro", "Dezembro" };

    private void AtualizarGrafico(List<FaturamentoMensalDto> dados, List<FaturamentoDetalhadoMesDto> detalhados, double larguraTotal)
    {
        ColunasGrafico = dados.Count;
        var maxValor = dados.Count > 0 ? dados.Max(d => d.Total) : 0m;
        TemDadosNoGrafico = maxValor > 0;

        decimal teto = CalcularTeto(maxValor);

        YLabelMax = FormatarValorEixo(teto);
        YLabel75 = FormatarValorEixo(teto * 0.75m);
        YLabel50 = FormatarValorEixo(teto * 0.50m);
        YLabel25 = FormatarValorEixo(teto * 0.25m);
        YLabel0 = "R$ 0";

        // Altura fixa nativa (200px) com espaçamento regular de 35px entre cada linha
        const double baselineY = 165.0;
        const double topY = 25.0;
        const double alturaUtil = baselineY - topY; // 140.0

        double largura = larguraTotal > 50 ? larguraTotal : 900.0;
        int n = dados.Count;
        double columnWidth = n > 0 ? largura / n : largura;

        var detalhadosDict = detalhados.ToDictionary(d => d.Mes);
        var pontos = new List<Point>();
        var mesesItems = new List<ChartMesItem>();

        for (int i = 0; i < n; i++)
        {
            var item = dados[i];
            int mesIdx = Math.Clamp(item.Mes - 1, 0, 11);

            double columnLeft = i * columnWidth;
            double x = columnLeft + (columnWidth / 2.0);
            double ratio = teto > 0 ? (double)(item.Total / teto) : 0.0;
            double y = baselineY - (ratio * alturaUtil);

            pontos.Add(new Point(x, y));

            detalhadosDict.TryGetValue(item.Mes, out var det);
            int atendimentos = det?.TotalAtendimentos ?? 0;
            decimal ticket = det?.TicketMedio ?? 0m;

            mesesItems.Add(new ChartMesItem
            {
                Mes = item.Mes,
                MesNome = NomesMeses[mesIdx],
                MesNomeCompletoAno = $"{NomesMesesCompletos[mesIdx]} de {AnoSelecionado}",
                Valor = item.Total,
                ValorFormatado = item.Total.ToString("C2", PtBrCulture),
                TooltipTexto = $"{NomesMesesCompletos[mesIdx]}/{AnoSelecionado}: {item.Total:C2}",
                TotalAtendimentos = atendimentos,
                TicketMedio = ticket,
                TicketMedioFormatado = ticket.ToString("C2", PtBrCulture),
                X = x,
                Y = y,
                ColumnLeft = columnLeft,
                ColumnWidth = columnWidth
            });
        }

        ChartMeses = mesesItems;

        var (linha, area) = GerarGeometriasGrafico(pontos, baselineY, topY);
        GraficoLinhaGeometry = linha;
        GraficoAreaGeometry = area;
    }

    private static (Geometry Linha, Geometry Area) GerarGeometriasGrafico(List<Point> pontos, double baselineY, double topY)
    {
        if (pontos.Count == 0) return (Geometry.Empty, Geometry.Empty);

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

                var cp1 = new Point(
                    p1.X + (p2.X - p0.X) / 6.0,
                    Math.Clamp(p1.Y + (p2.Y - p0.Y) / 6.0, topY, baselineY)
                );
                var cp2 = new Point(
                    p2.X - (p3.X - p1.X) / 6.0,
                    Math.Clamp(p2.Y - (p3.Y - p1.Y) / 6.0, topY, baselineY)
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
        if (maxValor <= 1000m) return Math.Ceiling(maxValor / 200m) * 200m;
        if (maxValor <= 5000m) return Math.Ceiling(maxValor / 500m) * 500m;
        if (maxValor <= 20000m) return Math.Ceiling(maxValor / 2000m) * 2000m;
        return Math.Ceiling(maxValor / 10000m) * 10000m;
    }

    private static string FormatarValorEixo(decimal valor)
    {
        if (valor >= 1000m)
        {
            decimal k = valor / 1000m;
            return k % 1 == 0 ? $"R$ {k:0}k" : $"R$ {k:0.#}k";
        }
        return $"R$ {valor:0}";
    }
}
