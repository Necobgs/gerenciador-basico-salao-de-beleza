using System;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GerenciadorSalao.Helpers;
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

    [ObservableProperty]
    private bool _isSidebarOpen = true;

    [ObservableProperty]
    private bool _isModalAberto;

    [RelayCommand]
    public void ToggleSidebar()
    {
        // Se houver modal aberto, não permite recolher/expandir sidebar
        if (IsModalAberto) return;
        IsSidebarOpen = !IsSidebarOpen;
    }

    [RelayCommand]
    public void FecharModalAtivo()
    {
        if (CurrentView is IModalHost host && host.IsModalAberto)
        {
            host.FecharModalAtivo();
        }
    }

    public MainViewModel(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public void Inicializar()
    {
        Navigate("Dashboard");
    }

    partial void OnCurrentViewChanging(object? oldValue, object? newValue)
    {
        if (oldValue is INotifyPropertyChanged oldInpc)
        {
            oldInpc.PropertyChanged -= CurrentView_PropertyChanged;
        }

        if (oldValue is ComandasViewModel oldComandas)
        {
            oldComandas.IsItemPopupOpen = false;
            oldComandas.IsCalendarPopupOpen = false;
        }

        if (oldValue is IModalHost oldHost && oldHost.IsModalAberto)
        {
            oldHost.FecharModalAtivo();
        }
    }

    partial void OnCurrentViewChanged(object? value)
    {
        if (value is INotifyPropertyChanged inpc)
        {
            inpc.PropertyChanged += CurrentView_PropertyChanged;
        }

        AtualizarStatusModal();
    }

    private void CurrentView_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(IModalHost.IsModalAberto))
        {
            AtualizarStatusModal();
        }
    }

    private void AtualizarStatusModal()
    {
        IsModalAberto = (CurrentView as IModalHost)?.IsModalAberto ?? false;
    }

    [RelayCommand]
    public void Navigate(string target)
    {
        // Impede qualquer navegação se houver um modal em aberto
        if (IsModalAberto) return;

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
                var agendamentosVm = _serviceProvider.GetRequiredService<AgendamentosViewModel>();
                agendamentosVm.NavigationRequested = Navigate;
                CurrentView = agendamentosVm;
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
