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
            // Configura Cultura Global pt-BR para XAML e Threads (Garante 'R$' e formatação brasileira padrão)
            var ptBr = new System.Globalization.CultureInfo("pt-BR");
            System.Threading.Thread.CurrentThread.CurrentCulture = ptBr;
            System.Threading.Thread.CurrentThread.CurrentUICulture = ptBr;
            System.Globalization.CultureInfo.DefaultThreadCurrentCulture = ptBr;
            System.Globalization.CultureInfo.DefaultThreadCurrentUICulture = ptBr;
            FrameworkElement.LanguageProperty.OverrideMetadata(
                typeof(FrameworkElement),
                new FrameworkPropertyMetadata(System.Windows.Markup.XmlLanguage.GetLanguage("pt-BR"))
            );

            base.OnStartup(e);

            var serviceCollection = new ServiceCollection();
            ConfigureServices(serviceCollection);

            Services = serviceCollection.BuildServiceProvider();

            // Inicializa e aplica EnsureCreated/Seeds controlados no SQLite
            using var scope = Services.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            DbInitializer.Inicializar(dbContext);

            // Inicia monitoramento de conectividade e sincronização em segundo plano
            var iCloudSync = Services.GetRequiredService<IICloudSyncService>();
            iCloudSync.IniciarMonitoramento();

            // Suporte para execução direta dos testes automatizados via CLI (--run-tests)
            if (e.Args.Any(a => a.Equals("--run-tests", StringComparison.OrdinalIgnoreCase) || 
                                a.Equals("--test-icloud-sync", StringComparison.OrdinalIgnoreCase)))
            {
                Task.Run(async () =>
                {
                    GerenciadorSalao.Services.Tests.ValidationHelperTests.ExecutarTestes(msg => Console.WriteLine(msg));
                    var resultados = await GerenciadorSalao.Services.Tests.ICloudSyncAcceptanceTests.ExecutarTodosOsTestesAsync(msg => Console.WriteLine(msg));
                    int falhas = resultados.Count(r => !r.Passou);
                    Environment.Exit(falhas > 0 ? 1 : 0);
                }).Wait();
                return;
            }

            if (e.Args.Any(a => a.Equals("--test-time-mask", StringComparison.OrdinalIgnoreCase)))
            {
                GerenciadorSalao.Services.Tests.ValidationHelperTests.ExecutarTestes(msg => Console.WriteLine(msg));
                Environment.Exit(0);
                return;
            }

            if (e.Args.Any(a => a.Equals("--verify-modal", StringComparison.OrdinalIgnoreCase)))
            {
                Task.Run(async () =>
                {
                    try
                    {
                        var vm = Services.GetRequiredService<AgendamentosViewModel>();
                        Console.WriteLine("Testando abertura do modal iCloud...");
                        await vm.AbrirModalICloudAsync();
                        Console.WriteLine($"SUCESSO: Modal aberto! IsModalICloudAberto={vm.IsModalICloudAberto}, Pendentes={vm.ListaOperacoesPendentes.Count}, Conflitos={vm.ListaConflitos.Count}");
                        Environment.Exit(0);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"ERRO AO ABRIR MODAL: {ex}");
                        Environment.Exit(1);
                    }
                }).Wait();
                return;
            }
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

        // Serviços de Integração iPhone / iCloud Calendar
        services.AddSingleton<IICloudCalendarProvider, ICloudCalDavProvider>();
        services.AddSingleton<IICloudSyncService, ICloudSyncService>();

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
