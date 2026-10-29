using System.Collections.ObjectModel;

using System.Globalization;

using System.Windows.Media;

using CommunityToolkit.Mvvm.ComponentModel;

using CommunityToolkit.Mvvm.Input;

using GerenciadorSalao.Helpers;

using GerenciadorSalao.Models;

using GerenciadorSalao.Models.DTOs;

using GerenciadorSalao.Services;



namespace GerenciadorSalao.ViewModels;



public partial class ComandasViewModel : ObservableObject, IModalHost

{

    private readonly IComandaService _comandaService;

    private readonly IClienteService _clienteService;

    private readonly IServicoService _servicoService;

    private readonly IProdutoService _produtoService;

    private readonly IAgendamentoService _agendamentoService;

    private static readonly CultureInfo PtBrCulture = new("pt-BR");



    [ObservableProperty]

    private string _titulo = "Comandas";



    [ObservableProperty]

    private string _subtitulo = "Do primeiro cuidado ao ├║ltimo detalhe do atendimento.";



    [ObservableProperty]

    private string _termoBusca = string.Empty;



    partial void OnTermoBuscaChanged(string value)

    {

        AplicarFiltros();

    }



    [ObservableProperty]

    [NotifyPropertyChangedFor(nameof(TemFiltroData))]

    [NotifyPropertyChangedFor(nameof(FiltroDataFormatada))]

    private DateTime? _filtroData = DateTime.Today;



    public bool TemFiltroData => FiltroData.HasValue;



    public string FiltroDataFormatada

    {

        get

        {

            if (!FiltroData.HasValue) return "Todas as datas";

            var diaSemana = FiltroData.Value.ToString("dddd", PtBrCulture);

            return $"{diaSemana} {FiltroData.Value:dd/MM/yyyy}";

        }

    }



    [ObservableProperty]

    private bool _isCalendarPopupOpen;



    [RelayCommand]

    public void AlternarCalendarPopup()

    {

        IsCalendarPopupOpen = !IsCalendarPopupOpen;

    }



    [RelayCommand]

    public void SelecionarDataHoje()

    {

        FiltroData = DateTime.Today;

        IsCalendarPopupOpen = false;

    }



    partial void OnFiltroDataChanged(DateTime? value)

    {

        IsCalendarPopupOpen = false;

        AplicarFiltros();

    }



    [RelayCommand]

    public void LimparFiltroData()

    {

        FiltroData = null;

        IsCalendarPopupOpen = false;

    }



    // Filtro de abas: "Todas", "Abertas", "Pagas"

    [ObservableProperty]

    private string _filtroSelecionado = "Todas";



    [ObservableProperty]

    private bool _isFiltroTodas = true;



    [ObservableProperty]

    private bool _isFiltroAbertas;



    [ObservableProperty]

    private bool _isFiltroPagas;



    [ObservableProperty]

    private ObservableCollection<ComandaListItemViewModel> _comandas = new();



    private List<ComandaListItemViewModel> _todasComandasCache = new();



    [ObservableProperty]

    private int _totalComandas;



    [ObservableProperty]

    private string _totalComandasTexto = "0 comandas";



    private readonly List<ComandaListItemViewModel> _todasComandasFiltradas = new();

    private const int ItensPorPagina = 20;



    [ObservableProperty]

    private int _paginaAtual = 1;



    [ObservableProperty]

    private int _totalPaginas = 1;



    [ObservableProperty]

    private string _textoPaginacao = string.Empty;



    [ObservableProperty]

    private bool _podeVoltarPagina;



    [ObservableProperty]

    private bool _podeAvancarPagina;



    [ObservableProperty]

    private bool _temPaginacao;



    [ObservableProperty]

    private bool _temComandas;



    [ObservableProperty]

    private bool _semComandas = true;



    [ObservableProperty]

    private bool _carregando;



    // ==========================================

    // MODAL 1: NOVA COMANDA (AGENDAMENTOS DO DIA)

    public bool IsModalAberto => IsModalNovaComandaAberto || IsModalEdicaoAberto || IsModalExcluirAberto;



    public void FecharModalAtivo()

    {

        IsItemPopupOpen = false;

        IsCalendarPopupOpen = false;

        if (IsModalEdicaoAberto) FecharModalEdicao();

        if (IsModalNovaComandaAberto) FecharModalNovaComanda();

        if (IsModalExcluirAberto) FecharModalExcluir();

    }



    partial void OnIsModalNovaComandaAbertoChanged(bool value)

    {

        OnPropertyChanged(nameof(IsModalAberto));

    }



    partial void OnIsModalEdicaoAbertoChanged(bool value)

    {

        if (!value)

        {

            IsItemPopupOpen = false;

        }

        OnPropertyChanged(nameof(IsModalAberto));

    }



    partial void OnIsModalExcluirAbertoChanged(bool value)

    {

        OnPropertyChanged(nameof(IsModalAberto));

    }



    [ObservableProperty]

    private bool _isModalNovaComandaAberto;



    [ObservableProperty]

    private DateTime _dataAgendamentos = DateTime.Today;



    partial void OnDataAgendamentosChanged(DateTime value)

    {

        _ = CarregarAgendamentosPendentesAsync();

    }



    [ObservableProperty]

    private ObservableCollection<AgendamentoPendenteItemViewModel> _agendamentosPendentes = new();



    [ObservableProperty]

    private bool _temAgendamentosPendentes;



    [ObservableProperty]

    private bool _semAgendamentosPendentes = true;



    // ==========================================

    // MODAL 2: EDI├ç├âO / CRIA├ç├âO / VISUALIZA├ç├âO

    // ==========================================

    [ObservableProperty]

    private bool _isModalEdicaoAberto;



    [ObservableProperty]

    private string _modalEdicaoTitulo = "Nova comanda";



    [ObservableProperty]

    [NotifyPropertyChangedFor(nameof(PodeEditar))]

    private bool _isVisualizacao;



    public bool PodeEditar => !IsVisualizacao;



    [ObservableProperty]

    private int _formComandaId;



    [ObservableProperty]

    private string _formNumeroComanda = "#0";

    [ObservableProperty]
    private int? _formAgendamentoVinculadoId;

    private bool _isAtualizandoTextoProgramaticamente;

    partial void OnFormClienteSelecionadoChanged(Cliente? value)
    {
        _isAtualizandoTextoProgramaticamente = true;
        BuscaClienteTexto = value?.NomeExibicao ?? string.Empty;
        _isAtualizandoTextoProgramaticamente = false;
    }



    [ObservableProperty]

    private Cliente? _formClienteSelecionado;



    [ObservableProperty]

    private string _buscaClienteTexto = string.Empty;



    [ObservableProperty]

    private bool _isClientePopupOpen;



    [ObservableProperty]

    private ObservableCollection<Cliente> _clientesDisponiveis = new();



    [ObservableProperty]

    private ObservableCollection<Cliente> _clientesDisponiveisFiltrados = new();



        partial void OnBuscaClienteTextoChanged(string value)
    {
        FiltrarClientesDisponiveis();

        if (!_isAtualizandoTextoProgramaticamente && PodeEditar && (IsClientePopupOpen || !string.IsNullOrEmpty(value)))
        {
            IsClientePopupOpen = true;
        }

        if (!_isAtualizandoTextoProgramaticamente && string.IsNullOrWhiteSpace(value))
        {
            FormClienteSelecionado = null;
        }
    }

    [RelayCommand]
    public void SelecionarClienteOption(Cliente? cliente)
    {
        if (cliente == null || !PodeEditar) return;

        FormClienteSelecionado = cliente;
        IsClientePopupOpen = false;
    }

    public void RestaurarTextoClienteSelecionado()
    {
        _isAtualizandoTextoProgramaticamente = true;
        BuscaClienteTexto = FormClienteSelecionado?.NomeExibicao ?? string.Empty;
        _isAtualizandoTextoProgramaticamente = false;
    }



    public void FiltrarClientesDisponiveis()

    {

        var termo = BuscaClienteTexto?.Trim() ?? string.Empty;

        ClientesDisponiveisFiltrados.Clear();



        var query = string.IsNullOrEmpty(termo)

            ? ClientesDisponiveis

            : ClientesDisponiveis.Where(c => c.NomeExibicao.Contains(termo, StringComparison.OrdinalIgnoreCase) ||

                                            (!string.IsNullOrEmpty(c.Telefone) && c.Telefone.Contains(termo)));



        foreach (var c in query.Take(20))

        {

            ClientesDisponiveisFiltrados.Add(c);

        }

    }



    private readonly List<ItemDisponivelOption> _todosItensCache = new();



    [ObservableProperty]

    [NotifyPropertyChangedFor(nameof(TemBuscaItem))]

    private string _buscaItemTexto = string.Empty;



    public bool TemBuscaItem => !string.IsNullOrWhiteSpace(BuscaItemTexto);



    [ObservableProperty]

    private bool _isItemPopupOpen;



    partial void OnBuscaItemTextoChanged(string value)

    {

        FiltrarItensDisponiveis();

        if (PodeEditar && (IsItemPopupOpen || !string.IsNullOrEmpty(value)))

        {

            IsItemPopupOpen = true;

        }

    }



    [RelayCommand]

    public void SelecionarItemOption(ItemDisponivelOption? item)

    {

        if (item == null || !PodeEditar) return;

        AdicionarItemAoFormularioDireto(item);

    }



    [RelayCommand]

    public void AbrirItemPopup()

    {

        if (PodeEditar)

        {

            IsItemPopupOpen = true;

        }

    }



    [RelayCommand]

    public void LimparBuscaItem()

    {

        BuscaItemTexto = string.Empty;

        IsItemPopupOpen = false;

    }



    [ObservableProperty]

    private ObservableCollection<ItemDisponivelOption> _itensDisponiveis = new();



    [ObservableProperty]

    private ItemDisponivelOption? _itemDisponivelSelecionado;



    [ObservableProperty]

    private ObservableCollection<ComandaItemFormViewModel> _formItens = new();



    [ObservableProperty]

    private string _formObservacao = string.Empty;



    [ObservableProperty]

    private int _formTempoTotalMinutos;



    [ObservableProperty]

    private string _formTempoTotalTexto = "0 minutos";



    [ObservableProperty]

    private decimal _formValorTotal;



    [ObservableProperty]

    private string _formValorTotalTexto = "R$ 0,00";



    [ObservableProperty]

    private string? _formMensagemErro;



    // ==========================================

    // MODAL 3: EXCLUS├âO DE COMANDA

    // ==========================================

    [ObservableProperty]

    private bool _isModalExcluirAberto;



    [ObservableProperty]

    private ComandaListItemViewModel? _comandaParaExcluir;



    public ComandasViewModel(

        IComandaService comandaService,

        IClienteService clienteService,

        IServicoService servicoService,

        IProdutoService produtoService,

        IAgendamentoService agendamentoService)

    {

        _comandaService = comandaService;

        _clienteService = clienteService;

        _servicoService = servicoService;

        _produtoService = produtoService;

        _agendamentoService = agendamentoService;



        _ = CarregarDadosIniciaisAsync();

    }



    [RelayCommand]

    public async Task CarregarDadosIniciaisAsync()

    {

        try

        {

            Carregando = true;

            await CarregarComandasAsync();

            await CarregarListasAuxiliaresAsync();

        }

        finally

        {

            Carregando = false;

        }

    }



    public async Task CarregarComandasAsync()

    {

        var comandasDb = await _comandaService.ObterTodasAsync();



        _todasComandasCache.Clear();

        foreach (var c in comandasDb)

        {

            var isAberta = c.Status == StatusComanda.Aberta;

            var isFinalizada = c.Status == StatusComanda.Finalizada;



            var descricoesItens = c.Itens.Select(i => i.Descricao).ToList();

            var descTexto = descricoesItens.Count switch

            {

                0 => "Sem itens",

                1 => descricoesItens[0],

                2 => $"{descricoesItens[0]} + {descricoesItens[1]}",

                _ => $"{descricoesItens[0]} + {descricoesItens.Count - 1} itens"

            };



            var dataExibicao = isFinalizada && c.DataFechamento.HasValue

                ? c.DataFechamento.Value.ToString("dd/MM/yyyy")

                : c.DataAbertura.ToString("dd/MM/yyyy");



            _todasComandasCache.Add(new ComandaListItemViewModel

            {

                Id = c.Id,

                NumeroTexto = $"#{c.Id}",

                ClienteId = c.ClienteId,

                ClienteNome = c.Cliente?.Nome ?? "Cliente n├úo identificado",

                DescricaoItens = descTexto,

                DataTexto = dataExibicao,

                DataHora = c.DataAbertura,

                ValorTotal = c.ValorTotal,

                ValorTotalTexto = c.ValorTotal.ToString("C2", PtBrCulture),

                Status = c.Status,

                IsAberta = isAberta,

                IsFinalizada = isFinalizada,

                StatusTexto = isAberta ? "Aberta" : "Paga",

                StatusBg = isAberta ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FDE8E4")) : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E8F5E9")),

                StatusFg = isAberta ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#A84834")) : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2E7D32"))

            });

        }



        AplicarFiltros();

    }



    private void AplicarFiltros()

    {

        var filtradas = _todasComandasCache.AsEnumerable();



        if (FiltroSelecionado == "Abertas")

            filtradas = filtradas.Where(c => c.IsAberta);

        else if (FiltroSelecionado == "Pagas")

            filtradas = filtradas.Where(c => c.IsFinalizada);



        if (FiltroData.HasValue)

        {

            filtradas = filtradas.Where(c => c.DataHora.Date == FiltroData.Value.Date);

        }



        if (!string.IsNullOrWhiteSpace(TermoBusca))

        {

            var termo = TermoBusca.Trim().ToLower();

            filtradas = filtradas.Where(c =>

                c.ClienteNome.ToLower().Contains(termo) ||

                c.NumeroTexto.ToLower().Contains(termo) ||

                c.DescricaoItens.ToLower().Contains(termo) ||

                c.DataTexto.ToLower().Contains(termo));

        }



        _todasComandasFiltradas.Clear();

        _todasComandasFiltradas.AddRange(filtradas);



        TotalComandas = _todasComandasFiltradas.Count;

        TotalComandasTexto = TotalComandas == 1 ? "1 comanda" : $"{TotalComandas} comandas";



        AtualizarPaginaComandas(resetPagina: true);

    }



    private void AtualizarPaginaComandas(bool resetPagina = false)

    {

        if (resetPagina) PaginaAtual = 1;



        var total = _todasComandasFiltradas.Count;

        TotalPaginas = total > 0 ? (int)Math.Ceiling((double)total / ItensPorPagina) : 1;



        if (PaginaAtual > TotalPaginas) PaginaAtual = TotalPaginas;

        if (PaginaAtual < 1) PaginaAtual = 1;



        var skip = (PaginaAtual - 1) * ItensPorPagina;

        Comandas.Clear();

        foreach (var c in _todasComandasFiltradas.Skip(skip).Take(ItensPorPagina))

        {

            Comandas.Add(c);

        }



        PodeVoltarPagina = total > 0 && PaginaAtual > 1;

        PodeAvancarPagina = total > 0 && PaginaAtual < TotalPaginas;

        TemPaginacao = total > 0 && TotalPaginas > 1;

        TemComandas = total > 0;

        SemComandas = total == 0;



        if (total == 0)

        {

            TextoPaginacao = string.Empty;

        }

        else

        {

            var inicio = skip + 1;

            var fim = Math.Min(skip + ItensPorPagina, total);

            TextoPaginacao = $"Mostrando {inicio}-{fim} de {total} (Página {PaginaAtual} de {TotalPaginas})";

        }

    }



    [RelayCommand]

    public void ProximaPagina()

    {

        if (PodeAvancarPagina)

        {

            PaginaAtual++;

            AtualizarPaginaComandas();

        }

    }



    [RelayCommand]

    public void PaginaAnterior()

    {

        if (PodeVoltarPagina)

        {

            PaginaAtual--;

            AtualizarPaginaComandas();

        }

    }



    [RelayCommand]

    public void SelecionarFiltro(string filtro)

    {

        FiltroSelecionado = filtro;

        IsFiltroTodas = filtro == "Todas";

        IsFiltroAbertas = filtro == "Abertas";

        IsFiltroPagas = filtro == "Pagas";

        AplicarFiltros();

    }



    private async Task CarregarListasAuxiliaresAsync()

    {

        // Carrega clientes

        var clientes = await _clienteService.ObterTodosAsync();

        ClientesDisponiveis.Clear();

        foreach (var cli in clientes)

        {

            ClientesDisponiveis.Add(cli);

        }

        FiltrarClientesDisponiveis();



        // Carrega cat├ílogo de serviços e produtos para sele├º├úo no modal

        var servicos = await _servicoService.ObterTodosAsync(apenasAtivos: true);

        var produtos = await _produtoService.ObterTodosAsync(apenasAtivos: true);



        _todosItensCache.Clear();

        foreach (var s in servicos)

        {

            _todosItensCache.Add(new ItemDisponivelOption

            {

                Tipo = TipoItemComanda.Servico,

                Id = s.Id,

                Nome = s.NomeExibicao,

                Preco = s.Valor,

                DuracaoMinutos = s.DuracaoMinutos,

                DisplayText = $"[serviço] {s.NomeExibicao} - {s.Valor.ToString("C2", PtBrCulture)}"

            });

        }



        foreach (var p in produtos)

        {

            _todosItensCache.Add(new ItemDisponivelOption

            {

                Tipo = TipoItemComanda.Produto,

                Id = p.Id,

                Nome = p.Descricao,

                Preco = p.Valor,

                DuracaoMinutos = 0,

                DisplayText = $"[Produto] {p.Descricao} - {p.Valor.ToString("C2", PtBrCulture)}"

            });

        }



        FiltrarItensDisponiveis();

    }



    private void FiltrarItensDisponiveis()

    {

        var termo = BuscaItemTexto?.Trim() ?? string.Empty;

        var selecionadoAntes = ItemDisponivelSelecionado;



        ItensDisponiveis.Clear();

        var lista = string.IsNullOrWhiteSpace(termo)

            ? _todosItensCache

            : _todosItensCache.Where(i => i.Nome.Contains(termo, StringComparison.OrdinalIgnoreCase)

                                       || i.DisplayText.Contains(termo, StringComparison.OrdinalIgnoreCase));



        // Exibe cat├ílogo amplo para n├úo esconder itens no dropdown com scroll

        foreach (var item in lista.Take(20))

        {

            ItensDisponiveis.Add(item);

        }



        if (selecionadoAntes != null && ItensDisponiveis.Contains(selecionadoAntes))

        {

            ItemDisponivelSelecionado = selecionadoAntes;

        }

        else

        {

            ItemDisponivelSelecionado = ItensDisponiveis.FirstOrDefault();

        }

    }



    // ==========================================

    // FLUXO MODAL 1: AGENDAMENTOS PENDENTES DO DIA

    // ==========================================

    [RelayCommand]

    public async Task AbrirNovaComandaAsync()

    {

        DataAgendamentos = DateTime.Today;

        await CarregarAgendamentosPendentesAsync();

        IsModalNovaComandaAberto = true;

    }



    [RelayCommand]

    public void FecharModalNovaComanda()

    {

        IsModalNovaComandaAberto = false;

    }



    public async Task CarregarAgendamentosPendentesAsync()

    {

        var agendas = await _agendamentoService.ObterAgendasPendentesDoDiaAsync(DataAgendamentos);



        AgendamentosPendentes.Clear();

        foreach (var a in agendas)

        {

            var servicosTexto = string.Join(" + ", a.AgendamentoServicos.Select(s => s.Servico.NomeExibicao));

            var valorTotal = a.AgendamentoServicos.Sum(s => s.Servico.Valor);



            var clienteNome = a.Cliente?.NomeExibicao

                ?? (!string.IsNullOrWhiteSpace(a.ClienteNaoEncontradoNome)

                    ? $"{a.ClienteNaoEncontradoNome} (Não cadastrado)"

                    : "Cliente Desconhecido");



            AgendamentosPendentes.Add(new AgendamentoPendenteItemViewModel

            {

                Id = a.Id,

                ClienteId = a.ClienteId,

                ClienteNome = clienteNome,

                HorarioTexto = a.DataHora.ToString("HH:mm"),

                ServicosTexto = servicosTexto,

                TempoEstimadoTexto = ValidationHelper.FormatarDuracao(a.TempoTotalPrevistoMinutos),

                ValorTotalTexto = valorTotal.ToString("C2", PtBrCulture),

                ComandaId = a.Comanda?.Id ?? a.ComandaId

            });

        }



        TemAgendamentosPendentes = AgendamentosPendentes.Count > 0;

        SemAgendamentosPendentes = AgendamentosPendentes.Count == 0;

    }



        [RelayCommand]
    public async Task AtenderAgendamentoAsync(AgendamentoPendenteItemViewModel agItem)
    {
        if (agItem == null) return;

        IsModalNovaComandaAberto = false;

        var agendamentoDb = await _agendamentoService.ObterPorIdAsync(agItem.Id);
        int? comandaId = agItem.ComandaId ?? agendamentoDb?.Comanda?.Id ?? agendamentoDb?.ComandaId;

        if (comandaId.HasValue && comandaId.Value > 0)
        {
            await AbrirEditarComandaPorIdAsync(comandaId.Value);
        }
        else
        {
            if (agendamentoDb != null)
            {
                await AbrirCriarComandaParaAgendamentoAsync(agendamentoDb);
            }
            else
            {
                await AbrirCriarComandaDoZeroAsync(agItem.ClienteId);
            }
        }
    }

    // ==========================================
    // FLUXO MODAL 2: CRIAR / EDITAR / VISUALIZAR
    // ==========================================
    [RelayCommand]
    public async Task CriarComandaDoZeroAsync()
    {
        await AbrirCriarComandaDoZeroAsync(null);
    }

    [RelayCommand]
    public async Task AbrirCriarComandaDoZeroAsync(int? clientePreSelecionadoId = null)
    {
        IsModalNovaComandaAberto = false;
        IsVisualizacao = false;
        ModalEdicaoTitulo = "Nova comanda";
        FormComandaId = 0;
        FormNumeroComanda = "Nova";
        FormAgendamentoVinculadoId = null;
        FormObservacao = string.Empty;
        FormMensagemErro = null;
        BuscaItemTexto = string.Empty;

        await CarregarListasAuxiliaresAsync();

        Cliente? cliente = null;
        if (clientePreSelecionadoId.HasValue)
        {
            cliente = ClientesDisponiveis.FirstOrDefault(c => c.Id == clientePreSelecionadoId.Value);
            if (cliente == null)
            {
                cliente = await _clienteService.ObterPorIdAsync(clientePreSelecionadoId.Value);
                if (cliente != null)
                {
                    ClientesDisponiveis.Insert(0, cliente);
                }
            }
        }
        else
        {
            cliente = ClientesDisponiveis.FirstOrDefault();
        }

        FormClienteSelecionado = cliente;
        _isAtualizandoTextoProgramaticamente = true;
        BuscaClienteTexto = cliente?.NomeExibicao ?? string.Empty;
        _isAtualizandoTextoProgramaticamente = false;
        IsClientePopupOpen = false;

        FormItens.Clear();
        ItemDisponivelSelecionado = ItensDisponiveis.FirstOrDefault();

        RecalcularTotaisFormulario();
        IsModalEdicaoAberto = true;
    }

    public async Task AbrirCriarComandaParaAgendamentoAsync(Agendamento agendamento)
    {
        IsModalNovaComandaAberto = false;
        IsVisualizacao = false;
        ModalEdicaoTitulo = "Nova comanda";
        FormComandaId = 0;
        FormNumeroComanda = "Nova";
        FormAgendamentoVinculadoId = agendamento.Id;
        FormObservacao = agendamento.Observacao ?? string.Empty;
        FormMensagemErro = null;
        BuscaItemTexto = string.Empty;

        await CarregarListasAuxiliaresAsync();

        Cliente? cliente = null;
        if (agendamento.ClienteId.HasValue)
        {
            cliente = ClientesDisponiveis.FirstOrDefault(c => c.Id == agendamento.ClienteId.Value);
            if (cliente == null && agendamento.Cliente != null)
            {
                cliente = agendamento.Cliente;
                ClientesDisponiveis.Insert(0, cliente);
            }
            else if (cliente == null)
            {
                cliente = await _clienteService.ObterPorIdAsync(agendamento.ClienteId.Value);
                if (cliente != null)
                {
                    ClientesDisponiveis.Insert(0, cliente);
                }
            }
        }
        else if (agendamento.Cliente != null)
        {
            cliente = agendamento.Cliente;
            if (!ClientesDisponiveis.Any(c => c.Id == cliente.Id))
            {
                ClientesDisponiveis.Insert(0, cliente);
            }
        }

        FormClienteSelecionado = cliente;
        _isAtualizandoTextoProgramaticamente = true;
        BuscaClienteTexto = cliente?.NomeExibicao ?? string.Empty;
        _isAtualizandoTextoProgramaticamente = false;
        IsClientePopupOpen = false;

        if (cliente == null && !string.IsNullOrWhiteSpace(agendamento.ClienteNaoEncontradoNome))
        {
            FormMensagemErro = $"Cliente '{agendamento.ClienteNaoEncontradoNome}' não cadastrada. Por favor, selecione a cliente.";
        }

        FormItens.Clear();
        if (agendamento.AgendamentoServicos != null && agendamento.AgendamentoServicos.Count > 0)
        {
            foreach (var asv in agendamento.AgendamentoServicos)
            {
                var s = asv.Servico;
                if (s == null) continue;

                var duracao = s.DuracaoMinutos;
                var detalhe = duracao > 0 ? ValidationHelper.FormatarDuracao(duracao) : "Serviço";

                var formItem = new ComandaItemFormViewModel
                {
                    Tipo = TipoItemComanda.Servico,
                    ServicoId = s.Id,
                    ReferenciaId = s.Id,
                    Descricao = s.NomeExibicao,
                    Detalhe = detalhe,
                    DuracaoMinutos = duracao,
                    DuracaoTexto = ValidationHelper.FormatarMinutosParaHhMm(duracao),
                    Quantidade = 1,
                    ValorUnitario = s.Valor,
                    ValorUnitarioTexto = s.Valor.ToString("N2", PtBrCulture),
                    OnChanged = RecalcularTotaisFormulario
                };
                formItem.RecalcularSubtotal();
                FormItens.Add(formItem);
            }
        }

        ItemDisponivelSelecionado = ItensDisponiveis.FirstOrDefault();
        RecalcularTotaisFormulario();
        IsModalEdicaoAberto = true;
    }

    public async Task AbrirComandaPorAgendamentoIdAsync(int agendamentoId)
    {
        var agendamento = await _agendamentoService.ObterPorIdAsync(agendamentoId);
        if (agendamento == null) return;

        var comandaId = agendamento.Comanda?.Id ?? agendamento.ComandaId;
        if (comandaId.HasValue && comandaId.Value > 0)
        {
            await AbrirEditarComandaPorIdAsync(comandaId.Value);
        }
        else
        {
            await AbrirCriarComandaParaAgendamentoAsync(agendamento);
        }
    }

    [RelayCommand]
    public async Task AbrirEditarComandaAsync(ComandaListItemViewModel item)
    {
        if (item == null) return;
        await AbrirEditarComandaPorIdAsync(item.Id);
    }



    public async Task AbrirEditarComandaPorIdAsync(int comandaId)

    {

        var comandaDb = await _comandaService.ObterPorIdAsync(comandaId);

        if (comandaDb == null) return;



        BuscaItemTexto = string.Empty;

        await CarregarListasAuxiliaresAsync();



        FormComandaId = comandaDb.Id;

        FormNumeroComanda = $"#{comandaDb.Id}";

        IsVisualizacao = comandaDb.Status != StatusComanda.Aberta;

        ModalEdicaoTitulo = IsVisualizacao ? "Visualizar comanda" : "Editar comanda";

        FormObservacao = comandaDb.Observacao ?? string.Empty;

                FormAgendamentoVinculadoId = comandaDb.AgendamentoId ?? comandaDb.Agendamento?.Id;

        var cliente = ClientesDisponiveis.FirstOrDefault(c => c.Id == comandaDb.ClienteId) ?? comandaDb.Cliente;
        if (cliente != null && !ClientesDisponiveis.Any(c => c.Id == cliente.Id))
        {
            ClientesDisponiveis.Insert(0, cliente);
        }

        FormClienteSelecionado = cliente;
        _isAtualizandoTextoProgramaticamente = true;
        BuscaClienteTexto = cliente?.NomeExibicao ?? string.Empty;
        _isAtualizandoTextoProgramaticamente = false;
        IsClientePopupOpen = false;

        FormMensagemErro = null;



        FormItens.Clear();

        foreach (var it in comandaDb.Itens)

        {

            var duracao = it.Servico?.DuracaoMinutos ?? 0;

            var detalhe = it.Tipo == TipoItemComanda.Servico

                ? (duracao > 0 ? ValidationHelper.FormatarDuracao(duracao) : "Serviço")

                : "Produto cosmético";



            var formItem = new ComandaItemFormViewModel

            {

                Id = it.Id,

                Tipo = it.Tipo,

                ServicoId = it.ServicoId,

                ProdutoId = it.ProdutoId,

                ReferenciaId = it.Tipo == TipoItemComanda.Servico ? (it.ServicoId ?? 0) : (it.ProdutoId ?? 0),

                Descricao = it.Descricao,

                Detalhe = detalhe,

                DuracaoMinutos = duracao,

                DuracaoTexto = ValidationHelper.FormatarMinutosParaHhMm(duracao),

                Quantidade = it.Quantidade,

                ValorUnitario = it.ValorUnitario,

                ValorUnitarioTexto = it.ValorUnitario.ToString("N2", PtBrCulture),

                OnChanged = RecalcularTotaisFormulario

            };



            formItem.RecalcularSubtotal();

            FormItens.Add(formItem);

        }



        ItemDisponivelSelecionado = ItensDisponiveis.FirstOrDefault();

        RecalcularTotaisFormulario();

        IsModalEdicaoAberto = true;

    }



    [RelayCommand]

    public void FecharModalEdicao()

    {

        IsItemPopupOpen = false;

        IsModalEdicaoAberto = false;

        FormMensagemErro = null;

    }



    [RelayCommand]

    public void AdicionarItemAoFormulario()

    {

        if (!PodeEditar) return;



        if (ItemDisponivelSelecionado == null && ItensDisponiveis.Count > 0)

        {

            ItemDisponivelSelecionado = ItensDisponiveis.FirstOrDefault();

        }



        if (ItemDisponivelSelecionado == null) return;



        AdicionarItemAoFormularioDireto(ItemDisponivelSelecionado);

    }



    public void AdicionarItemAoFormularioDireto(ItemDisponivelOption option)

    {

        if (option == null || !PodeEditar) return;



        var detalhe = option.Tipo == TipoItemComanda.Servico

            ? (option.DuracaoMinutos > 0 ? ValidationHelper.FormatarDuracao(option.DuracaoMinutos) : "Serviço")

            : "Produto cosmético";



        var novoItem = new ComandaItemFormViewModel

        {

            Id = null,

            Tipo = option.Tipo,

            ReferenciaId = option.Id,

            ServicoId = option.Tipo == TipoItemComanda.Servico ? option.Id : null,

            ProdutoId = option.Tipo == TipoItemComanda.Produto ? option.Id : null,

            Descricao = option.Nome,

            Detalhe = detalhe,

            DuracaoMinutos = option.DuracaoMinutos,

            DuracaoTexto = ValidationHelper.FormatarMinutosParaHhMm(option.DuracaoMinutos),

            Quantidade = 1,

            ValorUnitario = option.Preco,

            ValorUnitarioTexto = option.Preco.ToString("N2", PtBrCulture),

            OnChanged = RecalcularTotaisFormulario

        };



        novoItem.RecalcularSubtotal();

        FormItens.Add(novoItem);

        BuscaItemTexto = string.Empty;

        IsItemPopupOpen = false;

        ItemDisponivelSelecionado = ItensDisponiveis.FirstOrDefault();

        RecalcularTotaisFormulario();

    }



    [RelayCommand]

    public void RemoverItemDoFormulario(ComandaItemFormViewModel item)

    {

        if (item == null) return;

        FormItens.Remove(item);

        RecalcularTotaisFormulario();

    }



    [RelayCommand]

    public void AumentarQtd(ComandaItemFormViewModel item)

    {

        if (item == null || IsVisualizacao) return;

        item.Quantidade++;

        item.RecalcularSubtotal();

        RecalcularTotaisFormulario();

    }



    [RelayCommand]

    public void DiminuirQtd(ComandaItemFormViewModel item)

    {

        if (item == null || IsVisualizacao) return;

        if (item.Quantidade > 1)

        {

            item.Quantidade--;

            item.RecalcularSubtotal();

            RecalcularTotaisFormulario();

        }

    }



    public void RecalcularTotaisFormulario()

    {

        var totalMinutos = 0;

        var totalValor = 0m;



        foreach (var it in FormItens)

        {

            it.RecalcularSubtotal();

            totalValor += it.Subtotal;

            totalMinutos += it.DuracaoMinutos * it.Quantidade;

        }



        FormTempoTotalMinutos = totalMinutos;

        FormTempoTotalTexto = ValidationHelper.FormatarDuracao(totalMinutos);

        FormValorTotal = totalValor;

        FormValorTotalTexto = totalValor.ToString("C2", PtBrCulture);

    }



    [RelayCommand]

    public async Task SalvarComandaAbertaAsync()

    {

        await SalvarComandaInternoAsync(finalizar: false);

    }



    [RelayCommand]

    public async Task FinalizarComandaAsync()

    {

        await SalvarComandaInternoAsync(finalizar: true);

    }



    private async Task SalvarComandaInternoAsync(bool finalizar)

    {

        try

        {

            if (FormClienteSelecionado == null)

            {

                FormMensagemErro = "Selecione o cliente da comanda.";

                return;

            }



            if (FormItens.Count == 0)

            {

                FormMensagemErro = "Adicione ao menos um serviço ou produto á comanda.";

                return;

            }



            var itensDto = new List<ComandaItemDto>();



            foreach (var it in FormItens)

            {

                if (!ValidationHelper.ValidarValorMonetario(it.ValorUnitarioTexto, out var valorUnitario, out var erroPreco, $"item '{it.Descricao}'"))

                {

                    FormMensagemErro = erroPreco;

                    return;

                }



                if (it.Quantidade <= 0)

                {

                    FormMensagemErro = $"A quantidade de '{it.Descricao}' deve ser maior que zero.";

                    return;

                }



                itensDto.Add(new ComandaItemDto

                {

                    Id = it.Id,

                    Tipo = it.Tipo,

                    ReferenciaId = it.ReferenciaId,

                    Descricao = it.Descricao,

                    Quantidade = it.Quantidade,

                    ValorUnitario = valorUnitario

                });

            }



            FormMensagemErro = null;



            await _comandaService.SalvarComandaCompletaAsync(

                FormComandaId > 0 ? FormComandaId : null,

                FormClienteSelecionado.Id,

                FormObservacao,

                itensDto,

                finalizar: finalizar,
                agendamentoId: FormAgendamentoVinculadoId);



            IsModalEdicaoAberto = false;

            await CarregarComandasAsync();

        }

        catch (ArgumentException ex)

        {

            FormMensagemErro = ex.Message;

        }

        catch (Exception ex)

        {

            FormMensagemErro = "Erro ao processar a comanda. Verifique os dados e tente novamente.";

            System.Diagnostics.Debug.WriteLine($"[ComandasViewModel] Erro ao salvar: {ex}");

        }

    }



    // ==========================================

    // FLUXO MODAL 3: EXCLUS├âO DE COMANDA

    // ==========================================

    [RelayCommand]

    public void AbrirExcluirComanda(ComandaListItemViewModel item)

    {

        if (item == null) return;

        ComandaParaExcluir = item;

        IsModalExcluirAberto = true;

    }



    [RelayCommand]

    public void FecharModalExcluir()

    {

        IsModalExcluirAberto = false;

        ComandaParaExcluir = null;

    }



    [RelayCommand]

    public async Task ConfirmarExclusaoAsync()

    {

        if (ComandaParaExcluir == null) return;



        try

        {

            await _comandaService.ExcluirComandaAsync(ComandaParaExcluir.Id);

            IsModalExcluirAberto = false;

            ComandaParaExcluir = null;

            await CarregarComandasAsync();

        }

        catch (Exception ex)

        {

            System.Diagnostics.Debug.WriteLine($"[ComandasViewModel] Erro ao excluir comanda: {ex}");

        }

    }

}



public class ComandaListItemViewModel

{

    public int Id { get; set; }

    public string NumeroTexto { get; set; } = string.Empty;

    public int ClienteId { get; set; }

    public string ClienteNome { get; set; } = string.Empty;

    public string DescricaoItens { get; set; } = string.Empty;

    public string DataTexto { get; set; } = string.Empty;

    public DateTime DataHora { get; set; }

    public decimal ValorTotal { get; set; }

    public string ValorTotalTexto { get; set; } = "R$ 0,00";

    public StatusComanda Status { get; set; }

    public bool IsAberta { get; set; }

    public bool IsFinalizada { get; set; }

    public string StatusTexto { get; set; } = string.Empty;

    public SolidColorBrush StatusBg { get; set; } = null!;

    public SolidColorBrush StatusFg { get; set; } = null!;

}



public partial class ComandaItemFormViewModel : ObservableObject

{

    private static readonly CultureInfo PtBrCulture = new("pt-BR");



    public int? Id { get; set; }

    public TipoItemComanda Tipo { get; set; }

    public int ReferenciaId { get; set; }

    public int? ServicoId { get; set; }

    public int? ProdutoId { get; set; }

    public string Descricao { get; set; } = string.Empty;

    public string Detalhe { get; set; } = string.Empty;



    public bool IsServico => Tipo == TipoItemComanda.Servico;

    public bool IsProduto => Tipo == TipoItemComanda.Produto;



    [ObservableProperty]

    private int _duracaoMinutos;



    [ObservableProperty]

    private string _duracaoTexto = "0:00";



    [ObservableProperty]

    private int _quantidade = 1;



    public string QuantidadeTextoExibicao => $"{Quantidade}x";



    [ObservableProperty]

    private decimal _valorUnitario;



    [ObservableProperty]

    private string _valorUnitarioTexto = "0,00";



    partial void OnValorUnitarioTextoChanged(string value)

    {

        if (ValidationHelper.ValidarValorMonetario(value, out var parsed, out _))

        {

            _valorUnitario = parsed;

        }

        RecalcularSubtotal();

        OnChanged?.Invoke();

    }



    partial void OnQuantidadeChanged(int value)

    {

        RecalcularSubtotal();

        OnPropertyChanged(nameof(QuantidadeTextoExibicao));

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



    [ObservableProperty]

    private decimal _subtotal;



    [ObservableProperty]

    private string _subtotalTexto = "R$ 0,00";



    public Action? OnChanged { get; set; }



    public void RecalcularSubtotal()

    {

        Subtotal = Quantidade * ValorUnitario;

        SubtotalTexto = Subtotal.ToString("C2", PtBrCulture);

    }

}



public class AgendamentoPendenteItemViewModel

{

    public int Id { get; set; }

    public int? ClienteId { get; set; }

    public string ClienteNome { get; set; } = string.Empty;

    public string HorarioTexto { get; set; } = string.Empty;

    public string ServicosTexto { get; set; } = string.Empty;

    public string TempoEstimadoTexto { get; set; } = string.Empty;

    public string ValorTotalTexto { get; set; } = string.Empty;

    public int? ComandaId { get; set; }

}



public class ItemDisponivelOption

{

    private static readonly CultureInfo PtBrCulture = new("pt-BR");



    public TipoItemComanda Tipo { get; set; }

    public int Id { get; set; }

    public string Nome { get; set; } = string.Empty;

    public decimal Preco { get; set; }

    public int DuracaoMinutos { get; set; }

    public string DisplayText { get; set; } = string.Empty;



    public string PrecoFormatado => Preco.ToString("C2", PtBrCulture);

    public string TipoTexto => Tipo == TipoItemComanda.Servico ? "serviço" : "Produto";

    public string DuracaoTexto => DuracaoMinutos > 0 ? ValidationHelper.FormatarDuracao(DuracaoMinutos) : string.Empty;

    public string SubtituloTexto => Tipo == TipoItemComanda.Servico

        ? (DuracaoMinutos > 0 ? ValidationHelper.FormatarDuracao(DuracaoMinutos) : "Atendimento")

        : "Produto cosm├⌐tico";

    public bool TemDuracao => DuracaoMinutos > 0;

    public bool IsServico => Tipo == TipoItemComanda.Servico;

}

