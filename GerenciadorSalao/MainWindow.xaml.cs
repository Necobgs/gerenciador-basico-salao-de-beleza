using System;
using System.Windows.Input;
using GerenciadorSalao.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Wpf.Ui.Controls;

namespace GerenciadorSalao;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : FluentWindow
{
    public MainWindow()
    {
        InitializeComponent();
        var vm = App.Services.GetRequiredService<MainViewModel>();
        DataContext = vm;
        vm.Inicializar();
    }

    private void BackdropSidebar_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is MainViewModel vm && vm.IsModalAberto)
        {
            vm.FecharModalAtivoCommand.Execute(null);
            e.Handled = true;
        }
    }

    protected override void OnDeactivated(EventArgs e)
    {
        base.OnDeactivated(e);
        if (DataContext is MainViewModel vm && vm.CurrentView is ComandasViewModel cvm)
        {
            cvm.IsItemPopupOpen = false;
            cvm.IsCalendarPopupOpen = false;
        }
    }
}