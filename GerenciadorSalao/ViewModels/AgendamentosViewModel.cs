using CommunityToolkit.Mvvm.ComponentModel;
using GerenciadorSalao.Services;

namespace GerenciadorSalao.ViewModels;

public partial class AgendamentosViewModel : ObservableObject
{
    private readonly IAgendamentoService _agendamentoService;

    [ObservableProperty]
    private string _titulo = "Agenda de Horários";

    public AgendamentosViewModel(IAgendamentoService agendamentoService)
    {
        _agendamentoService = agendamentoService;
    }
}
