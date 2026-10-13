using CommunityToolkit.Mvvm.ComponentModel;
using GerenciadorSalao.Services;

namespace GerenciadorSalao.ViewModels;

public partial class ProdutosViewModel : ObservableObject
{
    private readonly IProdutoService _produtoService;

    [ObservableProperty]
    private string _titulo = "Catálogo de Produtos";

    public ProdutosViewModel(IProdutoService produtoService)
    {
        _produtoService = produtoService;
    }
}
