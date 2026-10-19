using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GerenciadorSalao.Helpers;
using GerenciadorSalao.Models;
using GerenciadorSalao.Services;

namespace GerenciadorSalao.ViewModels;

public partial class ProdutosViewModel : ObservableObject
{
    private readonly IProdutoService _produtoService;
    private static readonly CultureInfo PtBrCulture = new("pt-BR");

    [ObservableProperty]
    private string _titulo = "Produtos";

    [ObservableProperty]
    private string _subtitulo = "Uma seleção de cuidados para continuar em casa.";

    [ObservableProperty]
    private string _termoBusca = string.Empty;

    partial void OnTermoBuscaChanged(string value)
    {
        _ = FiltrarProdutosAsync();
    }

    [ObservableProperty]
    private ObservableCollection<ProdutoCardItem> _produtos = new();

    [ObservableProperty]
    private int _totalProdutos;

    [ObservableProperty]
    private string _totalProdutosTexto = "0 produtos";

    [ObservableProperty]
    private bool _temProdutos;

    [ObservableProperty]
    private bool _semProdutos = true;

    [ObservableProperty]
    private bool _carregando;

    // Propriedades do Modal Adicionar / Editar Produto
    [ObservableProperty]
    private bool _isModalProdutoAberto;

    [ObservableProperty]
    private bool _isModoEdicao;

    [ObservableProperty]
    private string _modalTitulo = "Novo produto";

    [ObservableProperty]
    private int _formProdutoId;

    [ObservableProperty]
    private string _formDescricao = string.Empty;

    [ObservableProperty]
    private string _formValor = string.Empty;

    [ObservableProperty]
    private string? _formMensagemErro;

    // Propriedades do Modal de Exclusão
    [ObservableProperty]
    private bool _isModalExcluirAberto;

    [ObservableProperty]
    private ProdutoCardItem? _produtoParaExcluir;

    public ProdutosViewModel(IProdutoService produtoService)
    {
        _produtoService = produtoService;
        _ = CarregarProdutosAsync();
    }

    [RelayCommand]
    public async Task CarregarProdutosAsync()
    {
        try
        {
            Carregando = true;
            await FiltrarProdutosAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ProdutosViewModel] Erro ao carregar produtos: {ex}");
        }
        finally
        {
            Carregando = false;
        }
    }

    private async Task FiltrarProdutosAsync()
    {
        var produtosDb = await _produtoService.ObterTodosAsync(TermoBusca, apenasAtivos: true);

        Produtos.Clear();
        foreach (var p in produtosDb)
        {
            Produtos.Add(new ProdutoCardItem
            {
                Id = p.Id,
                Descricao = p.Descricao,
                Valor = p.Valor,
                ValorFormatado = p.Valor.ToString("C2", PtBrCulture),
                Estoque = p.Estoque
            });
        }

        TotalProdutos = Produtos.Count;
        TotalProdutosTexto = TotalProdutos == 1 ? "1 produto" : $"{TotalProdutos} produtos";
        TemProdutos = TotalProdutos > 0;
        SemProdutos = TotalProdutos == 0;
    }

    [RelayCommand]
    public void AbrirNovoProduto()
    {
        IsModoEdicao = false;
        ModalTitulo = "Novo produto";
        FormProdutoId = 0;
        FormDescricao = string.Empty;
        FormValor = string.Empty;
        FormMensagemErro = null;
        IsModalProdutoAberto = true;
    }

    [RelayCommand]
    public void AbrirEditarProduto(ProdutoCardItem item)
    {
        if (item == null) return;

        IsModoEdicao = true;
        ModalTitulo = "Editar produto";
        FormProdutoId = item.Id;
        FormDescricao = item.Descricao;
        FormValor = item.Valor.ToString("N2", PtBrCulture);
        FormMensagemErro = null;
        IsModalProdutoAberto = true;
    }

    [RelayCommand]
    public void FecharModalProduto()
    {
        IsModalProdutoAberto = false;
        FormMensagemErro = null;
    }

    [RelayCommand]
    public async Task SalvarProdutoAsync()
    {
        try
        {
            if (!ValidationHelper.ValidarDescricaoProduto(FormDescricao, out var erroDescricao))
            {
                FormMensagemErro = erroDescricao;
                return;
            }

            if (!ValidationHelper.ValidarValorProduto(FormValor, out var valorDecimal, out var erroValor))
            {
                FormMensagemErro = erroValor;
                return;
            }

            FormMensagemErro = null;

            if (IsModoEdicao)
            {
                var produto = new Produto
                {
                    Id = FormProdutoId,
                    Descricao = FormDescricao.Trim(),
                    Valor = valorDecimal,
                    Ativo = true
                };

                await _produtoService.AtualizarAsync(produto);
            }
            else
            {
                var produto = new Produto
                {
                    Descricao = FormDescricao.Trim(),
                    Valor = valorDecimal,
                    Ativo = true,
                    Estoque = 10
                };

                await _produtoService.AdicionarAsync(produto);
            }

            IsModalProdutoAberto = false;
            await FiltrarProdutosAsync();
        }
        catch (ArgumentException ex)
        {
            FormMensagemErro = ex.Message;
        }
        catch (Exception ex)
        {
            FormMensagemErro = "Erro ao salvar produto. Verifique os dados e tente novamente.";
            System.Diagnostics.Debug.WriteLine($"[ProdutosViewModel] Erro ao salvar: {ex}");
        }
    }

    [RelayCommand]
    public void AbrirExcluirProduto(ProdutoCardItem item)
    {
        if (item == null) return;
        ProdutoParaExcluir = item;
        IsModalExcluirAberto = true;
    }

    [RelayCommand]
    public void FecharModalExcluir()
    {
        IsModalExcluirAberto = false;
        ProdutoParaExcluir = null;
    }

    [RelayCommand]
    public async Task ConfirmarExclusaoAsync()
    {
        if (ProdutoParaExcluir == null) return;

        try
        {
            await _produtoService.ExcluirAsync(ProdutoParaExcluir.Id);
            IsModalExcluirAberto = false;
            ProdutoParaExcluir = null;
            await FiltrarProdutosAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ProdutosViewModel] Erro ao excluir: {ex}");
        }
    }
}

public class ProdutoCardItem
{
    public int Id { get; set; }
    public string Descricao { get; set; } = string.Empty;
    public decimal Valor { get; set; }
    public string ValorFormatado { get; set; } = "R$ 0,00";
    public int Estoque { get; set; }
}
