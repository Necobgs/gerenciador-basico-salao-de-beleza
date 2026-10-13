using CommunityToolkit.Mvvm.ComponentModel;
using GerenciadorSalao.Services;

namespace GerenciadorSalao.ViewModels;

public partial class ComandasViewModel : ObservableObject
{
    private readonly IComandaService _comandaService;

    [ObservableProperty]
    private string _titulo = "Controle de Comandas";

    public ComandasViewModel(IComandaService comandaService)
    {
        _comandaService = comandaService;
    }
}
