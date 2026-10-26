using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using GerenciadorSalao.ViewModels;

namespace GerenciadorSalao.Views;

public partial class AgendamentosView : UserControl
{
    public AgendamentosView()
    {
        InitializeComponent();
    }

    private void AgendamentosView_Unloaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is AgendamentosViewModel vm)
        {
            vm.IsClientePopupOpen = false;
            vm.IsServicoPopupOpen = false;
            vm.IsCalendarOpen = false;
        }
    }

    private void AgendamentosView_PreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is AgendamentosViewModel vm)
        {
            if (vm.IsClientePopupOpen)
            {
                if (e.OriginalSource is DependencyObject dep)
                {
                    if (IsElementOrDescendant(dep, BorderInputCliente) || IsElementOrDescendant(dep, PopupClientes?.Child))
                    {
                        // Permite interação normal dentro do campo e dropdown de clientes
                    }
                    else
                    {
                        vm.IsClientePopupOpen = false;
                    }
                }
            }

            if (vm.IsServicoPopupOpen)
            {
                if (e.OriginalSource is DependencyObject dep)
                {
                    if (IsElementOrDescendant(dep, BorderInputServico) || IsElementOrDescendant(dep, PopupServicos?.Child))
                    {
                        // Permite interação normal dentro do campo e dropdown de serviços
                    }
                    else
                    {
                        vm.IsServicoPopupOpen = false;
                    }
                }
            }
        }
    }

    private static bool IsElementOrDescendant(DependencyObject? child, DependencyObject? parent)
    {
        if (parent == null || child == null) return false;
        if (child == parent) return true;

        if (parent is Visual parentVisual && child is Visual childVisual)
        {
            try
            {
                if (parentVisual.IsAncestorOf(childVisual)) return true;
            }
            catch { }
        }

        var current = child;
        while (current != null)
        {
            if (current == parent) return true;
            current = VisualTreeHelper.GetParent(current) ?? LogicalTreeHelper.GetParent(current);
        }
        return false;
    }

    private void FecharModalNovoAgendamento_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is AgendamentosViewModel vm)
        {
            vm.IsClientePopupOpen = false;
            vm.IsServicoPopupOpen = false;
            vm.FecharModalNovoAgendamentoCommand.Execute(null);
            e.Handled = true;
        }
    }

    private void FecharModalExcluir_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is AgendamentosViewModel vm)
        {
            vm.FecharModalExcluirCommand.Execute(null);
            e.Handled = true;
        }
    }

    private void FecharModalICloud_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is AgendamentosViewModel vm)
        {
            vm.FecharModalICloudCommand.Execute(null);
            e.Handled = true;
        }
    }

    // ===================== CAMPO DE CLIENTE PESQUISÁVEL =====================
    private void BorderInputCliente_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is DependencyObject dep && BtnToggleClientePopup != null)
        {
            if (dep is Visual visual && (BtnToggleClientePopup == dep || BtnToggleClientePopup.IsAncestorOf(visual)))
            {
                return;
            }
        }

        if (DataContext is AgendamentosViewModel vm)
        {
            if (!TxtBuscaCliente.IsKeyboardFocused)
            {
                TxtBuscaCliente.Focus();
            }

            Dispatcher.BeginInvoke(new Action(() =>
            {
                vm.IsClientePopupOpen = true;
            }), System.Windows.Threading.DispatcherPriority.Input);
        }
    }

    private void TxtBuscaCliente_GotFocus(object sender, RoutedEventArgs e)
    {
        if (DataContext is AgendamentosViewModel vm)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                vm.IsClientePopupOpen = true;
            }), System.Windows.Threading.DispatcherPriority.Input);
        }
    }

    private void TxtBuscaCliente_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (DataContext is not AgendamentosViewModel vm) return;

        if (e.Key == Key.Escape)
        {
            vm.IsClientePopupOpen = false;
        }
        else if (e.Key == Key.Down)
        {
            vm.IsClientePopupOpen = true;
        }
    }

    // ===================== CAMPO DE SERVIÇO PESQUISÁVEL =====================
    private void BorderInputServico_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is DependencyObject dep && BtnToggleServicoPopup != null)
        {
            if (dep is Visual visual && (BtnToggleServicoPopup == dep || BtnToggleServicoPopup.IsAncestorOf(visual)))
            {
                return;
            }
        }

        if (DataContext is AgendamentosViewModel vm)
        {
            if (!TxtBuscaServico.IsKeyboardFocused)
            {
                TxtBuscaServico.Focus();
            }

            Dispatcher.BeginInvoke(new Action(() =>
            {
                vm.IsServicoPopupOpen = true;
            }), System.Windows.Threading.DispatcherPriority.Input);
        }
    }

    private void TxtBuscaServico_GotFocus(object sender, RoutedEventArgs e)
    {
        if (DataContext is AgendamentosViewModel vm)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                vm.IsServicoPopupOpen = true;
            }), System.Windows.Threading.DispatcherPriority.Input);
        }
    }

    private void TxtBuscaServico_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (DataContext is not AgendamentosViewModel vm) return;

        if (e.Key == Key.Escape)
        {
            vm.IsServicoPopupOpen = false;
        }
        else if (e.Key == Key.Down)
        {
            vm.IsServicoPopupOpen = true;
        }
    }
}
