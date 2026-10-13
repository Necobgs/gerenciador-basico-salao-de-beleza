using System.IO;
using System.Windows;
using GerenciadorSalao.Data;
using GerenciadorSalao.Services;
using GerenciadorSalao.ViewModels;
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
        AppDomain.CurrentDomain.UnhandledException += (s, args) =>
        {
            try
            {
                var pastaApp = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GerenciadorSalao");
                Directory.CreateDirectory(pastaApp);
                File.WriteAllText(Path.Combine(pastaApp, "crash.log"), args.ExceptionObject.ToString());
            }
            catch { }
        };

        this.DispatcherUnhandledException += (s, args) =>
        {
            try
            {
                var pastaApp = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GerenciadorSalao");
                Directory.CreateDirectory(pastaApp);
                File.WriteAllText(Path.Combine(pastaApp, "dispatcher_crash.log"), args.Exception.ToString());
            }
            catch { }
        };

        try
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
        catch (Exception ex)
        {
            System.IO.File.WriteAllText("startup_error.log", $"ERRO ONSTARTUP: {ex}");
            MessageBox.Show(ex.ToString(), "Erro na Inicialização");
            throw;
        }
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

        // ViewModels
        services.AddSingleton<MainViewModel>();
        services.AddTransient<DashboardViewModel>();
        services.AddTransient<AgendamentosViewModel>();
        services.AddTransient<ClientesViewModel>();
        services.AddTransient<ComandasViewModel>();
        services.AddTransient<ServicosViewModel>();
        services.AddTransient<ProdutosViewModel>();
        services.AddTransient<RelatoriosViewModel>();
    }
}
