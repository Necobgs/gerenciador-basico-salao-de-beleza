using CommunityToolkit.Mvvm.ComponentModel;
using GerenciadorSalao.Services;

namespace GerenciadorSalao.ViewModels;

public partial class ServicosViewModel : ObservableObject
{
    private readonly IServicoService _servicoService;

    [ObservableProperty]
    private string _titulo = "Catálogo de Serviços";

    public ServicosViewModel(IServicoService servicoService)
    {
        _servicoService = servicoService;
    }
}
