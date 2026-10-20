using System.Diagnostics;
using System.IO;
using System.Text;
using GerenciadorSalao.Helpers;
using GerenciadorSalao.Models;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Auth.OAuth2.Flows;
using Google.Apis.Auth.OAuth2.Responses;
using Google.Apis.Calendar.v3;
using Google.Apis.Calendar.v3.Data;
using Google.Apis.Services;
using Google.Apis.Util.Store;

namespace GerenciadorSalao.Services;

public class GoogleCalendarService : IGoogleCalendarService
{
    private static readonly string[] Scopes = { CalendarService.Scope.CalendarEvents };
    private const string ApplicationName = "Gerenciador de Salao de Beleza";
    private CalendarService? _calendarService;

    public string ObterCaminhoCredenciais()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var pastaApp = Path.Combine(localAppData, "GerenciadorSalao");
        if (!Directory.Exists(pastaApp))
            Directory.CreateDirectory(pastaApp);

        return Path.Combine(pastaApp, "google_credentials.json");
    }

    public string ObterDiretorioTokens()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(localAppData, "GerenciadorSalao", "GoogleTokens");
    }

    public bool ArquivoCredenciaisExiste()
    {
        var caminhoPrincipal = ObterCaminhoCredenciais();
        var caminhoBase = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "google_credentials.json");
        return File.Exists(caminhoPrincipal) || File.Exists(caminhoBase);
    }

    private string? ObterCaminhoArquivoCredenciaisExistente()
    {
        var caminhoPrincipal = ObterCaminhoCredenciais();
        if (File.Exists(caminhoPrincipal)) return caminhoPrincipal;

        var caminhoBase = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "google_credentials.json");
        if (File.Exists(caminhoBase)) return caminhoBase;

        return null;
    }

    public async Task<bool> EstaConectadoAsync()
    {
        try
        {
            var service = await ObterServicoAsync(iniciarFluxoInterativo: false);
            return service != null;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> InicializarOuAutenticarAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var service = await ObterServicoAsync(iniciarFluxoInterativo: true, cancellationToken);
            return service != null;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[GoogleCalendarService] Erro ao autenticar: {ex.Message}");
            return false;
        }
    }

    private async Task<CalendarService?> ObterServicoAsync(bool iniciarFluxoInterativo = true, CancellationToken cancellationToken = default)
    {
        if (_calendarService != null)
            return _calendarService;

        var arquivoCredenciais = ObterCaminhoArquivoCredenciaisExistente();
        if (arquivoCredenciais == null)
        {
            Debug.WriteLine("[GoogleCalendarService] Arquivo google_credentials.json não encontrado.");
            return null;
        }

        var tokenFolder = ObterDiretorioTokens();

        using var stream = new FileStream(arquivoCredenciais, FileMode.Open, FileAccess.Read);
        var secrets = GoogleClientSecrets.FromStream(stream).Secrets;

        UserCredential? credential;
        if (iniciarFluxoInterativo)
        {
            credential = await GoogleWebAuthorizationBroker.AuthorizeAsync(
                secrets,
                Scopes,
                "user",
                cancellationToken,
                new FileDataStore(tokenFolder, true));
        }
        else
        {
            // Apenas verifica se o token já existe no FileDataStore
            var dataStore = new FileDataStore(tokenFolder, true);
            var token = await dataStore.GetAsync<TokenResponse>("user");
            if (token == null)
                return null;

            credential = new UserCredential(new GoogleAuthorizationCodeFlow(
                new GoogleAuthorizationCodeFlow.Initializer
                {
                    ClientSecrets = secrets,
                    Scopes = Scopes,
                    DataStore = dataStore
                }), "user", token);
        }

        if (credential == null)
            return null;

        _calendarService = new CalendarService(new BaseClientService.Initializer
        {
            HttpClientInitializer = credential,
            ApplicationName = ApplicationName,
        });

        return _calendarService;
    }

    public async Task<string?> CriarEventoAsync(Agendamento agendamento)
    {
        try
        {
            var service = await ObterServicoAsync(iniciarFluxoInterativo: false);
            if (service == null)
                return null;

            var novoEvento = MontarEventoGoogle(agendamento);
            var request = service.Events.Insert(novoEvento, "primary");
            var eventoCriado = await request.ExecuteAsync();

            return eventoCriado?.Id;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[GoogleCalendarService] Falha ao criar evento: {ex.Message}");
            return null;
        }
    }

    public async Task<bool> AtualizarEventoAsync(Agendamento agendamento)
    {
        try
        {
            if (string.IsNullOrEmpty(agendamento.GoogleEventId))
            {
                var id = await CriarEventoAsync(agendamento);
                agendamento.GoogleEventId = id;
                return !string.IsNullOrEmpty(id);
            }

            var service = await ObterServicoAsync(iniciarFluxoInterativo: false);
            if (service == null)
                return false;

            var eventoAtualizado = MontarEventoGoogle(agendamento);
            var request = service.Events.Update(eventoAtualizado, "primary", agendamento.GoogleEventId);
            await request.ExecuteAsync();

            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[GoogleCalendarService] Falha ao atualizar evento: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> CancelarEventoAsync(string googleEventId)
    {
        try
        {
            if (string.IsNullOrEmpty(googleEventId))
                return true;

            var service = await ObterServicoAsync(iniciarFluxoInterativo: false);
            if (service == null)
                return false;

            var request = service.Events.Delete("primary", googleEventId);
            await request.ExecuteAsync();
            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[GoogleCalendarService] Falha ao cancelar evento: {ex.Message}");
            return false;
        }
    }

    public Task DesconectarAsync()
    {
        _calendarService = null;
        var tokenFolder = ObterDiretorioTokens();
        if (Directory.Exists(tokenFolder))
        {
            try
            {
                Directory.Delete(tokenFolder, recursive: true);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[GoogleCalendarService] Erro ao remover tokens: {ex.Message}");
            }
        }
        return Task.CompletedTask;
    }

    private static Event MontarEventoGoogle(Agendamento agendamento)
    {
        var nomeCliente = agendamento.Cliente != null ? agendamento.Cliente.NomeExibicao : "Cliente";
        var telefoneCliente = agendamento.Cliente?.Telefone ?? "Não informado";
        var dataInicio = agendamento.DataHora;
        var duracaoMinutos = agendamento.TempoTotalPrevistoMinutos > 0 ? agendamento.TempoTotalPrevistoMinutos : 30;
        var dataFim = dataInicio.AddMinutes(duracaoMinutos);

        var sbDescricao = new StringBuilder();
        sbDescricao.AppendLine($"Cliente: {nomeCliente}");
        sbDescricao.AppendLine($"Telefone: {telefoneCliente}");
        sbDescricao.AppendLine($"Tempo Total Previsto: {ValidationHelper.FormatarDuracao(duracaoMinutos)}");
        sbDescricao.AppendLine();

        if (agendamento.AgendamentoServicos != null && agendamento.AgendamentoServicos.Count > 0)
        {
            sbDescricao.AppendLine("Serviços Agendados:");
            foreach (var asv in agendamento.AgendamentoServicos)
            {
                if (asv.Servico != null)
                {
                    sbDescricao.AppendLine($"- {asv.Servico.Descricao} (R$ {asv.Servico.Valor:F2})");
                }
            }
            sbDescricao.AppendLine();
        }

        if (!string.IsNullOrWhiteSpace(agendamento.Observacao))
        {
            sbDescricao.AppendLine($"Observações: {agendamento.Observacao}");
        }

        return new Event
        {
            Summary = $"Salão - {nomeCliente}",
            Description = sbDescricao.ToString(),
            Start = new EventDateTime
            {
                DateTimeDateTimeOffset = new DateTimeOffset(dataInicio),
                TimeZone = TimeZoneInfo.Local.Id
            },
            End = new EventDateTime
            {
                DateTimeDateTimeOffset = new DateTimeOffset(dataFim),
                TimeZone = TimeZoneInfo.Local.Id
            }
        };
    }
}
