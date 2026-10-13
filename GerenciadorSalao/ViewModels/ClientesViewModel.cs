using CommunityToolkit.Mvvm.ComponentModel;
using GerenciadorSalao.Services;

namespace GerenciadorSalao.ViewModels;

public partial class ClientesViewModel : ObservableObject
{
    private readonly IClienteService _clienteService;

    [ObservableProperty]
    private string _titulo = "Gestão de Clientes";

    public ClientesViewModel(IClienteService clienteService)
    {
        _clienteService = clienteService;
    }
}
