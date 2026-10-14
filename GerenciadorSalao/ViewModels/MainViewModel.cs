using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;

namespace GerenciadorSalao.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly IServiceProvider _serviceProvider;

    [ObservableProperty]
    private object? _currentView;

    [ObservableProperty]
    private string _currentPageTitle = "Visão geral";

    [ObservableProperty]
    private string _currentNavTag = "Dashboard";

    [ObservableProperty]
    private string _salaoNome = "Karla Borges";

    public MainViewModel(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public void Inicializar()
    {
        Navigate("Dashboard");
    }

    [RelayCommand]
    public void Navigate(string target)
    {
        CurrentNavTag = target;

        switch (target)
        {
            case "Dashboard":
                CurrentPageTitle = "Visão geral";
                var dashVm = _serviceProvider.GetRequiredService<DashboardViewModel>();
                dashVm.NavigationRequested = Navigate;
                CurrentView = dashVm;
                break;
            case "Agendamentos":
                CurrentPageTitle = "Agenda";
                CurrentView = _serviceProvider.GetRequiredService<AgendamentosViewModel>();
                break;
            case "Clientes":
                CurrentPageTitle = "Clientes";
                CurrentView = _serviceProvider.GetRequiredService<ClientesViewModel>();
                break;
            case "Comandas":
                CurrentPageTitle = "Comandas";
                CurrentView = _serviceProvider.GetRequiredService<ComandasViewModel>();
                break;
            case "Servicos":
                CurrentPageTitle = "Serviços";
                CurrentView = _serviceProvider.GetRequiredService<ServicosViewModel>();
                break;
            case "Produtos":
                CurrentPageTitle = "Produtos";
                CurrentView = _serviceProvider.GetRequiredService<ProdutosViewModel>();
                break;
            case "Relatorios":
                CurrentPageTitle = "Relatórios";
                CurrentView = _serviceProvider.GetRequiredService<RelatoriosViewModel>();
                break;
        }
    }
}
