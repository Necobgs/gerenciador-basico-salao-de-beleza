using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GerenciadorSalao.Helpers;
using GerenciadorSalao.Models;
using GerenciadorSalao.Services;

namespace GerenciadorSalao.ViewModels;

public class ClienteTableItem
{
    public int Id { get; set; }
    public string Nome { get; set; } = string.Empty;
    public string? Apelido { get; set; }
    public string? Telefone { get; set; }
    public string? Email { get; set; }
    public string? Observacao { get; set; }
    public string Iniciais { get; set; } = string.Empty;
    public string AvatarBg { get; set; } = "#FEE9E6";
    public int TotalAtendimentos { get; set; }
    public decimal TotalGasto { get; set; }
    public string TotalGastoFormatado => TotalGasto.ToString("C2");
    public string TelefoneExibicao => string.IsNullOrWhiteSpace(Telefone) ? "—" : Telefone;
    public string EmailExibicao => string.IsNullOrWhiteSpace(Email) ? "—" : Email;
    public string NomeExibicao => string.IsNullOrWhiteSpace(Apelido) ? Nome : $"{Nome} ({Apelido})";
}

public class ClienteHistoricoComandaItem
{
    public int ComandaId { get; set; }
    public string DataFormatada { get; set; } = string.Empty;
    public string ItensResumo { get; set; } = string.Empty;
    public decimal ValorTotal { get; set; }
    public string ValorTotalFormatado => ValorTotal.ToString("C2");
    public string Status { get; set; } = "Finalizada";
}

public partial class ClientesViewModel : ObservableObject, IModalHost
{
    public bool IsModalAberto => IsModalClienteAberto || IsHistoricoAberto;

    public void FecharModalAtivo()
    {
        if (IsModalClienteAberto) FecharModalCliente();
        if (IsHistoricoAberto) FecharHistorico();
    }

    partial void OnIsModalClienteAbertoChanged(bool value)
    {
        OnPropertyChanged(nameof(IsModalAberto));
    }

    partial void OnIsHistoricoAbertoChanged(bool value)
    {
        OnPropertyChanged(nameof(IsModalAberto));
    }

    private readonly IClienteService _clienteService;
    private readonly IComandaService _comandaService;

    private static readonly string[] AvatarPalette = new[]
    {
        "#FEE9E6", // Rosé suave
        "#FCE4EC", // Rosa blush
        "#FFF3E0", // Pêssego suave
        "#E1F5FE", // Azul céu suave
        "#F3E5F5", // Lavanda suave
        "#E8F5E9"  // Menta suave
    };

    [ObservableProperty]
    private string _termoBusca = string.Empty;

    partial void OnTermoBuscaChanged(string value) => _ = FiltrarClientesAsync();

    [ObservableProperty]
    private List<ClienteTableItem> _clientes = new();

    [ObservableProperty]
    private int _totalClientes;

    private readonly List<ClienteTableItem> _todosClientesFiltrados = new();
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
    private bool _carregando;

    public bool TemClientes => Clientes.Count > 0;
    public bool SemClientes => Clientes.Count == 0;

    // Propriedades do Modal "Novo Cliente" / "Editar Cliente"
    [ObservableProperty]
    private bool _isModalClienteAberto;

    [ObservableProperty]
    private bool _isModoEdicao;

    [ObservableProperty]
    private string _modalTitulo = "Novo cliente";

    [ObservableProperty]
    private int _formClienteId;

    [ObservableProperty]
    private string _formNome = string.Empty;

    [ObservableProperty]
    private string _formApelido = string.Empty;

    [ObservableProperty]
    private string _formTelefone = string.Empty;

    partial void OnFormTelefoneChanged(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return;
        var formatado = ValidationHelper.FormatarTelefone(value);
        if (formatado != value)
        {
            FormTelefone = formatado;
        }
    }

    [ObservableProperty]
    private string _formEmail = string.Empty;

    partial void OnFormEmailChanged(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return;
        if (value.Contains(' '))
        {
            FormEmail = value.Replace(" ", "");
        }
    }

    [ObservableProperty]
    private string _formObservacao = string.Empty;

    [ObservableProperty]
    private string? _formMensagemErro;

    // Propriedades do Modal de Histórico do Cliente
    [ObservableProperty]
    private bool _isHistoricoAberto;

    [ObservableProperty]
    private ClienteTableItem? _clienteHistoricoSelecionado;

    [ObservableProperty]
    private List<ClienteHistoricoComandaItem> _historicoComandas = new();

    [ObservableProperty]
    private string _historicoTotalGastoTexto = "R$ 0,00";

    [ObservableProperty]
    private string _historicoTotalAtendimentosTexto = "0 atendimentos";

    public bool TemHistoricoComandas => HistoricoComandas.Count > 0;
    public bool SemHistoricoComandas => HistoricoComandas.Count == 0;

    public ClientesViewModel(IClienteService clienteService, IComandaService comandaService)
    {
        _clienteService = clienteService;
        _comandaService = comandaService;

        _ = CarregarClientesAsync();
    }

    [RelayCommand]
    public async Task CarregarClientesAsync()
    {
        try
        {
            Carregando = true;
            await FiltrarClientesAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ClientesViewModel] Erro ao carregar clientes: {ex}");
        }
        finally
        {
            Carregando = false;
        }
    }

    private async Task FiltrarClientesAsync()
    {
        try
        {
            var clientesDb = await _clienteService.ObterTodosAsync(TermoBusca);
            var todasComandas = await _comandaService.ObterTodasAsync();

            var itens = new List<ClienteTableItem>();
            for (int i = 0; i < clientesDb.Count; i++)
            {
                var c = clientesDb[i];
                var comandasDoCliente = todasComandas.Where(cmd => cmd.ClienteId == c.Id && cmd.Status == StatusComanda.Finalizada).ToList();
                var atendimentos = comandasDoCliente.Count;
                var totalGasto = comandasDoCliente.Sum(cmd => cmd.ValorTotal);

                itens.Add(new ClienteTableItem
                {
                    Id = c.Id,
                    Nome = c.Nome,
                    Apelido = c.Apelido,
                    Telefone = c.Telefone,
                    Email = c.Email,
                    Observacao = c.Observacao,
                    Iniciais = ObterIniciais(c.Nome),
                    AvatarBg = AvatarPalette[Math.Abs(c.Id) % AvatarPalette.Length],
                    TotalAtendimentos = atendimentos,
                    TotalGasto = totalGasto
                });
            }

            _todosClientesFiltrados.Clear();
            _todosClientesFiltrados.AddRange(itens);
            TotalClientes = _todosClientesFiltrados.Count;
            AtualizarPaginaClientes(resetPagina: true);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ClientesViewModel] Erro ao filtrar clientes: {ex}");
        }
    }

    private void AtualizarPaginaClientes(bool resetPagina = false)
    {
        if (resetPagina) PaginaAtual = 1;

        var total = _todosClientesFiltrados.Count;
        TotalPaginas = total > 0 ? (int)Math.Ceiling((double)total / ItensPorPagina) : 1;

        if (PaginaAtual > TotalPaginas) PaginaAtual = TotalPaginas;
        if (PaginaAtual < 1) PaginaAtual = 1;

        var skip = (PaginaAtual - 1) * ItensPorPagina;
        Clientes = _todosClientesFiltrados.Skip(skip).Take(ItensPorPagina).ToList();

        PodeVoltarPagina = PaginaAtual > 1;
        PodeAvancarPagina = PaginaAtual < TotalPaginas;
        TemPaginacao = total > 0;

        if (total == 0)
        {
            TextoPaginacao = "Nenhum cliente encontrado";
        }
        else
        {
            var inicio = skip + 1;
            var fim = Math.Min(skip + ItensPorPagina, total);
            TextoPaginacao = $"Mostrando {inicio}-{fim} de {total} (Página {PaginaAtual} de {TotalPaginas})";
        }

        OnPropertyChanged(nameof(TemClientes));
        OnPropertyChanged(nameof(SemClientes));
    }

    [RelayCommand]
    public void ProximaPagina()
    {
        if (PodeAvancarPagina)
        {
            PaginaAtual++;
            AtualizarPaginaClientes();
        }
    }

    [RelayCommand]
    public void PaginaAnterior()
    {
        if (PodeVoltarPagina)
        {
            PaginaAtual--;
            AtualizarPaginaClientes();
        }
    }

    [RelayCommand]
    public void AbrirModalNovoCliente()
    {
        IsModoEdicao = false;
        ModalTitulo = "Novo cliente";
        FormClienteId = 0;
        FormNome = string.Empty;
        FormApelido = string.Empty;
        FormTelefone = string.Empty;
        FormEmail = string.Empty;
        FormObservacao = string.Empty;
        FormMensagemErro = null;
        IsModalClienteAberto = true;
    }

    [RelayCommand]
    public void AbrirModalEditarCliente(ClienteTableItem item)
    {
        if (item == null) return;

        IsModoEdicao = true;
        ModalTitulo = "Editar cliente";
        FormClienteId = item.Id;
        FormNome = item.Nome;
        FormApelido = item.Apelido ?? string.Empty;
        FormTelefone = item.Telefone ?? string.Empty;
        FormEmail = item.Email ?? string.Empty;
        FormObservacao = item.Observacao ?? string.Empty;
        FormMensagemErro = null;
        IsModalClienteAberto = true;
    }

    [RelayCommand]
    public void FecharModalCliente()
    {
        IsModalClienteAberto = false;
        FormMensagemErro = null;
    }

    [RelayCommand]
    public async Task SalvarClienteAsync()
    {
        try
        {
            if (!ValidationHelper.ValidarNome(FormNome, out var erroNome))
            {
                FormMensagemErro = erroNome;
                return;
            }

            if (!ValidationHelper.ValidarTelefone(FormTelefone, out var erroTelefone, obrigatorio: false))
            {
                FormMensagemErro = erroTelefone;
                return;
            }

            if (!ValidationHelper.ValidarEmail(FormEmail, out var erroEmail, obrigatorio: false))
            {
                FormMensagemErro = erroEmail;
                return;
            }

            FormMensagemErro = null;

            if (IsModoEdicao)
            {
                var cliente = new Cliente
                {
                    Id = FormClienteId,
                    Nome = FormNome.Trim(),
                    Apelido = string.IsNullOrWhiteSpace(FormApelido) ? null : FormApelido.Trim(),
                    Telefone = string.IsNullOrWhiteSpace(FormTelefone) ? null : FormTelefone.Trim(),
                    Email = string.IsNullOrWhiteSpace(FormEmail) ? null : FormEmail.Trim(),
                    Observacao = string.IsNullOrWhiteSpace(FormObservacao) ? null : FormObservacao.Trim()
                };

                await _clienteService.AtualizarAsync(cliente);
            }
            else
            {
                var cliente = new Cliente
                {
                    Nome = FormNome.Trim(),
                    Apelido = string.IsNullOrWhiteSpace(FormApelido) ? null : FormApelido.Trim(),
                    Telefone = string.IsNullOrWhiteSpace(FormTelefone) ? null : FormTelefone.Trim(),
                    Email = string.IsNullOrWhiteSpace(FormEmail) ? null : FormEmail.Trim(),
                    Observacao = string.IsNullOrWhiteSpace(FormObservacao) ? null : FormObservacao.Trim()
                };

                await _clienteService.AdicionarAsync(cliente);
            }

            IsModalClienteAberto = false;
            await CarregarClientesAsync();
        }
        catch (Exception ex)
        {
            FormMensagemErro = ex.Message;
        }
    }

    [RelayCommand]
    public async Task ExcluirClienteAsync(ClienteTableItem item)
    {
        if (item == null) return;

        var result = MessageBox.Show(
            $"Deseja realmente remover o cliente \"{item.Nome}\"?\nO histórico financeiro de comandas anteriores será preservado.",
            "Confirmar Exclusão",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result == MessageBoxResult.Yes)
        {
            try
            {
                await _clienteService.ExcluirAsync(item.Id);
                await CarregarClientesAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Erro ao excluir cliente: {ex.Message}", "Atenção", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
    }

    [RelayCommand]
    public async Task AbrirHistoricoAsync(ClienteTableItem item)
    {
        if (item == null) return;

        try
        {
            ClienteHistoricoSelecionado = item;
            HistoricoTotalGastoTexto = item.TotalGastoFormatado;
            HistoricoTotalAtendimentosTexto = $"{item.TotalAtendimentos} atendimento{(item.TotalAtendimentos != 1 ? "s" : "")}";

            var clienteComHistorico = await _clienteService.ObterHistoricoClienteAsync(item.Id);
            var comandasList = new List<ClienteHistoricoComandaItem>();

            if (clienteComHistorico?.Comandas != null)
            {
                var ordenadas = clienteComHistorico.Comandas
                    .OrderByDescending(c => c.DataFechamento ?? c.DataAbertura)
                    .ToList();

                foreach (var c in ordenadas)
                {
                    var dt = c.DataFechamento ?? c.DataAbertura;
                    var itensStr = c.Itens.Count > 0
                        ? string.Join(", ", c.Itens.Select(i => i.Descricao))
                        : "Atendimento geral";

                    string statusStr = c.Status switch
                    {
                        StatusComanda.Aberta => "Em andamento",
                        StatusComanda.Finalizada => "Finalizada",
                        _ => c.Status.ToString()
                    };

                    comandasList.Add(new ClienteHistoricoComandaItem
                    {
                        ComandaId = c.Id,
                        DataFormatada = dt.ToString("dd 'de' MMM. 'de' yyyy, HH:mm"),
                        ItensResumo = itensStr,
                        ValorTotal = c.ValorTotal,
                        Status = statusStr
                    });
                }
            }

            HistoricoComandas = comandasList;
            OnPropertyChanged(nameof(TemHistoricoComandas));
            OnPropertyChanged(nameof(SemHistoricoComandas));
            IsHistoricoAberto = true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ClientesViewModel] Erro ao carregar histórico: {ex}");
        }
    }

    [RelayCommand]
    public void FecharHistorico()
    {
        IsHistoricoAberto = false;
        ClienteHistoricoSelecionado = null;
        HistoricoComandas.Clear();
    }

    private static string ObterIniciais(string nome)
    {
        if (string.IsNullOrWhiteSpace(nome)) return "CL";
        var partes = nome.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (partes.Length == 1) return partes[0].Substring(0, Math.Min(2, partes[0].Length)).ToUpper();
        return $"{partes[0][0]}{partes[^1][0]}".ToUpper();
    }
}
