using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GerenciadorSalao.Helpers;
using GerenciadorSalao.Models;
using GerenciadorSalao.Services;

namespace GerenciadorSalao.ViewModels;

public partial class ServicosViewModel : ObservableObject, IModalHost
{
    public bool IsModalAberto => IsModalServicoAberto || IsModalExcluirAberto;

    public void FecharModalAtivo()
    {
        if (IsModalServicoAberto) FecharModalServico();
        if (IsModalExcluirAberto) FecharModalExcluir();
    }

    partial void OnIsModalServicoAbertoChanged(bool value)
    {
        OnPropertyChanged(nameof(IsModalAberto));
    }

    partial void OnIsModalExcluirAbertoChanged(bool value)
    {
        OnPropertyChanged(nameof(IsModalAberto));
    }

    private readonly IServicoService _servicoService;
    private static readonly CultureInfo PtBrCulture = new("pt-BR");

    [ObservableProperty]
    private string _titulo = "Serviços";

    [ObservableProperty]
    private string _subtitulo = "Seu talento, organizado em cada serviço.";

    [ObservableProperty]
    private string _termoBusca = string.Empty;

    partial void OnTermoBuscaChanged(string value)
    {
        _ = FiltrarServicosAsync();
    }

    [ObservableProperty]
    private ObservableCollection<ServicoCardItem> _servicos = new();

    [ObservableProperty]
    private int _totalServicos;

    [ObservableProperty]
    private string _totalServicosTexto = "0 serviços";

    [ObservableProperty]
    private bool _temServicos;

    [ObservableProperty]
    private bool _semServicos = true;

    [ObservableProperty]
    private bool _carregando;

    // Propriedades do Modal Adicionar / Editar Serviço
    [ObservableProperty]
    private bool _isModalServicoAberto;

    [ObservableProperty]
    private bool _isModoEdicao;

    [ObservableProperty]
    private string _modalTitulo = "Novo serviço";

    [ObservableProperty]
    private int _formServicoId;

    [ObservableProperty]
    private string _formDescricao = string.Empty;

    [ObservableProperty]
    private string _formValor = "0,00";

    [ObservableProperty]
    private string _formDuracao = "1:00";

    [ObservableProperty]
    private string _formDuracaoPreview = "= 1:00 hora (60 min)";

    partial void OnFormDuracaoChanged(string value)
    {
        if (ValidationHelper.ValidarDuracaoServico(value, out var minutos, out _))
        {
            FormDuracaoPreview = $"= {ValidationHelper.FormatarDuracao(minutos)} ({minutos} min)";
        }
        else
        {
            FormDuracaoPreview = "Exemplo: 1:30 para 1 hora e meia, ou 90 para minutos";
        }
    }

    [ObservableProperty]
    private string? _formMensagemErro;

    // Propriedades do Modal de Exclusão
    [ObservableProperty]
    private bool _isModalExcluirAberto;

    [ObservableProperty]
    private ServicoCardItem? _servicoParaExcluir;

    public ServicosViewModel(IServicoService servicoService)
    {
        _servicoService = servicoService;
        _ = CarregarServicosAsync();
    }

    [RelayCommand]
    public async Task CarregarServicosAsync()
    {
        try
        {
            Carregando = true;
            await FiltrarServicosAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ServicosViewModel] Erro ao carregar serviços: {ex}");
        }
        finally
        {
            Carregando = false;
        }
    }

    private async Task FiltrarServicosAsync()
    {
        var servicosDb = await _servicoService.ObterTodosAsync(TermoBusca, apenasAtivos: true);

        Servicos.Clear();
        foreach (var s in servicosDb)
        {
            Servicos.Add(new ServicoCardItem
            {
                Id = s.Id,
                Descricao = s.Descricao,
                Valor = s.Valor,
                ValorFormatado = s.Valor.ToString("C2", PtBrCulture),
                DuracaoMinutos = s.DuracaoMinutos,
                DuracaoFormatada = ValidationHelper.FormatarDuracao(s.DuracaoMinutos)
            });
        }

        TotalServicos = Servicos.Count;
        TotalServicosTexto = TotalServicos == 1 ? "1 serviço" : $"{TotalServicos} serviços";
        TemServicos = TotalServicos > 0;
        SemServicos = TotalServicos == 0;
    }

    [RelayCommand]
    public void AbrirNovoServico()
    {
        IsModoEdicao = false;
        ModalTitulo = "Novo serviço";
        FormServicoId = 0;
        FormDescricao = string.Empty;
        FormValor = "0,00";
        FormDuracao = "1:00";
        FormDuracaoPreview = "= 1:00 hora (60 min)";
        FormMensagemErro = null;
        IsModalServicoAberto = true;
    }

    [RelayCommand]
    public void AbrirEditarServico(ServicoCardItem item)
    {
        if (item == null) return;

        IsModoEdicao = true;
        ModalTitulo = "Editar serviço";
        FormServicoId = item.Id;
        FormDescricao = item.Descricao;
        FormValor = item.Valor.ToString("N2", PtBrCulture);
        FormDuracao = ValidationHelper.FormatarDuracaoParaEdicao(item.DuracaoMinutos);
        FormDuracaoPreview = $"= {ValidationHelper.FormatarDuracao(item.DuracaoMinutos)} ({item.DuracaoMinutos} min)";
        FormMensagemErro = null;
        IsModalServicoAberto = true;
    }

    [RelayCommand]
    public void FecharModalServico()
    {
        IsModalServicoAberto = false;
        FormMensagemErro = null;
    }

    [RelayCommand]
    public async Task SalvarServicoAsync()
    {
        try
        {
            if (!ValidationHelper.ValidarDescricaoServico(FormDescricao, out var erroDescricao))
            {
                FormMensagemErro = erroDescricao;
                return;
            }

            if (!ValidationHelper.ValidarValorServico(FormValor, out var valorDecimal, out var erroValor))
            {
                FormMensagemErro = erroValor;
                return;
            }

            if (!ValidationHelper.ValidarDuracaoServico(FormDuracao, out var duracaoMinutos, out var erroDuracao))
            {
                FormMensagemErro = erroDuracao;
                return;
            }

            FormMensagemErro = null;

            if (IsModoEdicao)
            {
                var servico = new Servico
                {
                    Id = FormServicoId,
                    Descricao = FormDescricao.Trim(),
                    Valor = valorDecimal,
                    DuracaoMinutos = duracaoMinutos,
                    Ativo = true
                };

                await _servicoService.AtualizarAsync(servico);
            }
            else
            {
                var servico = new Servico
                {
                    Descricao = FormDescricao.Trim(),
                    Valor = valorDecimal,
                    DuracaoMinutos = duracaoMinutos,
                    Ativo = true
                };

                await _servicoService.AdicionarAsync(servico);
            }

            IsModalServicoAberto = false;
            await FiltrarServicosAsync();
        }
        catch (ArgumentException ex)
        {
            FormMensagemErro = ex.Message;
        }
        catch (Exception ex)
        {
            FormMensagemErro = "Erro ao salvar serviço. Verifique os dados e tente novamente.";
            System.Diagnostics.Debug.WriteLine($"[ServicosViewModel] Erro ao salvar serviço: {ex}");
        }
    }

    [RelayCommand]
    public void AbrirExcluirServico(ServicoCardItem item)
    {
        if (item == null) return;
        ServicoParaExcluir = item;
        IsModalExcluirAberto = true;
    }

    [RelayCommand]
    public void FecharModalExcluir()
    {
        IsModalExcluirAberto = false;
        ServicoParaExcluir = null;
    }

    [RelayCommand]
    public async Task ConfirmarExclusaoAsync()
    {
        if (ServicoParaExcluir == null) return;

        try
        {
            await _servicoService.ExcluirAsync(ServicoParaExcluir.Id);
            IsModalExcluirAberto = false;
            ServicoParaExcluir = null;
            await FiltrarServicosAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ServicosViewModel] Erro ao excluir serviço: {ex}");
        }
    }
}

public class ServicoCardItem
{
    public int Id { get; set; }
    public string Descricao { get; set; } = string.Empty;
    public decimal Valor { get; set; }
    public string ValorFormatado { get; set; } = "R$ 0,00";
    public int DuracaoMinutos { get; set; }
    public string DuracaoFormatada { get; set; } = "0 minutos";
}
