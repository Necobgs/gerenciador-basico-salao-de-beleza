using System.Windows;
using GerenciadorSalao.Data;
using GerenciadorSalao.Services;
using Microsoft.Extensions.DependencyInjection;

namespace GerenciadorSalao;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    public static IServiceProvider Services { get; private set; } = null!;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var serviceCollection = new ServiceCollection();
        ConfigureServices(serviceCollection);

        Services = serviceCollection.BuildServiceProvider();

        // Inicializa e aplica EnsureCreated/Seeds controlados no SQLite
        using var scope = Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        DbInitializer.Inicializar(dbContext);
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        // Contexto de Dados
        services.AddDbContext<AppDbContext>();

        // Serviços de Negócio
        services.AddScoped<IClienteService, ClienteService>();
        services.AddScoped<IServicoService, ServicoService>();
        services.AddScoped<IProdutoService, ProdutoService>();
        services.AddScoped<IAgendamentoService, AgendamentoService>();
        services.AddScoped<IComandaService, ComandaService>();
        services.AddScoped<IRelatorioService, RelatorioService>();
        services.AddScoped<IGoogleCalendarService, GoogleCalendarService>();
    }
}
