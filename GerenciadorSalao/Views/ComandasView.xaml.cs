using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using GerenciadorSalao.ViewModels;

namespace GerenciadorSalao.Views;

public partial class ComandasView : UserControl
{
    public ComandasView()
    {
        InitializeComponent();
    }

    private void ComandasView_Unloaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is ComandasViewModel vm)
        {
            vm.IsClientePopupOpen = false;
            vm.IsItemPopupOpen = false;
            vm.IsCalendarPopupOpen = false;
        }
    }

    private void ComandasView_PreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is ComandasViewModel vm)
        {
            if (vm.IsClientePopupOpen)
            {
                if (e.OriginalSource is DependencyObject dep)
                {
                    if (IsElementOrDescendant(dep, BorderInputCliente) || IsElementOrDescendant(dep, PopupClientes?.Child))
                    {
                        // Permite interação normal dentro do campo e popup de clientes
                    }
                    else
                    {
                        vm.IsClientePopupOpen = false;
                    }
                }
            }

            if (vm.IsItemPopupOpen)
            {
                if (e.OriginalSource is DependencyObject dep)
                {
                    // Se o clique ocorreu dentro do BorderInputItem ou do PopupItens, permite interação normal
                    if (IsElementOrDescendant(dep, BorderInputItem) || IsElementOrDescendant(dep, PopupItens?.Child))
                    {
                        return;
                    }
                }

                // Clicou em qualquer lugar fora do campo e do popup: fecha imediatamente
                vm.IsItemPopupOpen = false;
                Keyboard.ClearFocus();
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

    private void FecharModalNovaComanda_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is ComandasViewModel vm)
        {
            vm.FecharModalNovaComandaCommand.Execute(null);
            e.Handled = true;
        }
    }

    private void FecharModalEdicao_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is ComandasViewModel vm)
        {
            vm.IsClientePopupOpen = false;
            vm.IsItemPopupOpen = false;
            vm.FecharModalEdicaoCommand.Execute(null);
            e.Handled = true;
        }
    }

    private void FecharModalExcluir_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is ComandasViewModel vm)
        {
            vm.FecharModalExcluirCommand.Execute(null);
            e.Handled = true;
        }
    }

    private void Calendar_SelectedDatesChanged(object sender, SelectionChangedEventArgs e)
    {
        if (DataContext is ComandasViewModel vm)
        {
            vm.IsCalendarPopupOpen = false;
        }
    }

    private void BorderInputItem_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        // Se o clique veio da seta de toggle (ToggleButton), permite o toggle normal do botão
        if (e.OriginalSource is DependencyObject dep && BtnToggleItemPopup != null)
        {
            if (dep is Visual visual && (BtnToggleItemPopup == dep || BtnToggleItemPopup.IsAncestorOf(visual)))
            {
                return;
            }
        }

        if (DataContext is ComandasViewModel vm && vm.PodeEditar)
        {
            if (!TxtBuscaItem.IsKeyboardFocused)
            {
                TxtBuscaItem.Focus();
            }

            Dispatcher.BeginInvoke(new Action(() =>
            {
                vm.IsItemPopupOpen = true;
            }), System.Windows.Threading.DispatcherPriority.Input);
        }
    }

    private void TxtBuscaItem_GotFocus(object sender, RoutedEventArgs e)
    {
        if (DataContext is ComandasViewModel vm && vm.PodeEditar)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                vm.IsItemPopupOpen = true;
            }), System.Windows.Threading.DispatcherPriority.Input);
        }
    }

    private void TxtBuscaItem_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (DataContext is not ComandasViewModel vm) return;

        if (e.Key == Key.Enter)
        {
            e.Handled = true;
            if (vm.AdicionarItemAoFormularioCommand.CanExecute(null))
            {
                vm.AdicionarItemAoFormularioCommand.Execute(null);
            }
        }
        else if (e.Key == Key.Escape)
        {
            vm.IsItemPopupOpen = false;
        }
        else if (e.Key == Key.Down)
        {
            if (!vm.IsItemPopupOpen)
            {
                vm.IsItemPopupOpen = true;
            }
            else if (vm.ItensDisponiveis.Count > 0)
            {
                var idx = vm.ItemDisponivelSelecionado != null ? vm.ItensDisponiveis.IndexOf(vm.ItemDisponivelSelecionado) : -1;
                if (idx < vm.ItensDisponiveis.Count - 1)
                {
                    vm.ItemDisponivelSelecionado = vm.ItensDisponiveis[idx + 1];
                }
            }
            e.Handled = true;
        }
        else if (e.Key == Key.Up && vm.IsItemPopupOpen && vm.ItensDisponiveis.Count > 0)
        {
            var idx = vm.ItemDisponivelSelecionado != null ? vm.ItensDisponiveis.IndexOf(vm.ItemDisponivelSelecionado) : 0;
            if (idx > 0)
            {
                vm.ItemDisponivelSelecionado = vm.ItensDisponiveis[idx - 1];
            }
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

        if (DataContext is ComandasViewModel vm && vm.PodeEditar)
        {
            if (!TxtBuscaCliente.IsKeyboardFocused)
            {
                TxtBuscaCliente.Focus();
                TxtBuscaCliente.SelectAll();
            }

            Dispatcher.BeginInvoke(new Action(() =>
            {
                vm.IsClientePopupOpen = true;
            }), System.Windows.Threading.DispatcherPriority.Input);
        }
    }

    private void TxtBuscaCliente_GotFocus(object sender, RoutedEventArgs e)
    {
        if (DataContext is ComandasViewModel vm && vm.PodeEditar)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                vm.IsClientePopupOpen = true;
            }), System.Windows.Threading.DispatcherPriority.Input);
        }
    }

    private void TxtBuscaCliente_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (DataContext is not ComandasViewModel vm || !vm.PodeEditar) return;

        if (e.Key == Key.Escape)
        {
            vm.IsClientePopupOpen = false;
        }
        else if (e.Key == Key.Down)
        {
            vm.IsClientePopupOpen = true;
        }
    }

    private void TxtBuscaCliente_LostFocus(object sender, RoutedEventArgs e)
    {
        if (DataContext is ComandasViewModel vm)
        {
            if (vm.FormClienteSelecionado != null && vm.BuscaClienteTexto != vm.FormClienteSelecionado.NomeExibicao)
            {
                vm.RestaurarTextoClienteSelecionado();
            }
        }
    }
}