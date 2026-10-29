using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.RegularExpressions;
using GerenciadorSalao.Models.DTOs;

namespace GerenciadorSalao.Services;

/// <summary>
/// Provedor oficial CalDAV para sincronização com o Apple iCloud Calendar (iPhone).
/// Comunica diretamente com os servidores da Apple (caldav.icloud.com) via RFC 4791 / RFC 5545.
/// </summary>
public class ICloudCalDavProvider : IICloudCalendarProvider, IDisposable
{
    private const string CalDavBaseUrl = "https://caldav.icloud.com";
    private readonly HttpClient _httpClient;
    private string? _email;
    private string? _senhaApp;
    private string? _urlCalendarioPrincipal;

    public ICloudCalDavProvider(HttpClient? httpClient = null)
    {
        _httpClient = httpClient ?? new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(15)
        };
    }

    public void ConfigurarCredenciais(string email, string senhaApp)
    {
        _email = email?.Trim();
        _senhaApp = senhaApp?.Trim().Replace(" ", "");
        _urlCalendarioPrincipal = null; // reinicia descoberta
    }

    private void AplicarAutenticacao(HttpRequestMessage request)
    {
        if (!string.IsNullOrWhiteSpace(_email) && !string.IsNullOrWhiteSpace(_senhaApp))
        {
            var authBytes = Encoding.UTF8.GetBytes($"{_email}:{_senhaApp}");
            var base64 = Convert.ToBase64String(authBytes);
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", base64);
        }
    }

    public async Task<bool> VerificarDisponibilidadeAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(6));

            using var request = new HttpRequestMessage(HttpMethod.Options, CalDavBaseUrl);
            var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cts.Token);
            return response.IsSuccessStatusCode || response.StatusCode == HttpStatusCode.Unauthorized;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> ValidarCredenciaisAsync(string email, string senhaApp, CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(senhaApp))
                return false;

            ConfigurarCredenciais(email, senhaApp);

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(10));

            // Descoberta do principal para checar login
            var propfindXml = """
            <?xml version="1.0" encoding="utf-8" ?>
            <d:propfind xmlns:d="DAV:">
              <d:prop>
                <d:current-user-principal />
              </d:prop>
            </d:propfind>
            """;

            using var request = new HttpRequestMessage(new HttpMethod("PROPFIND"), CalDavBaseUrl + "/");
            request.Headers.Add("Depth", "0");
            request.Content = new StringContent(propfindXml, Encoding.UTF8, "application/xml");
            AplicarAutenticacao(request);

            var response = await _httpClient.SendAsync(request, cts.Token);
            return response.IsSuccessStatusCode || (int)response.StatusCode == 207;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ICloudCalDav] Erro ao validar credenciais: {ex.Message}");
            return false;
        }
    }

    public async Task<List<ICloudEventoRemoto>> ObterEventosAsync(DateTime inicio, DateTime fim, CancellationToken cancellationToken = default)
    {
        var eventos = new List<ICloudEventoRemoto>();
        try
        {
            var calendarUrl = await ObterOuDescobrirUrlCalendarioAsync(cancellationToken);
            if (string.IsNullOrEmpty(calendarUrl))
                return eventos;

            var strInicio = inicio.ToUniversalTime().ToString("yyyyMMddTHHmmssZ", CultureInfo.InvariantCulture);
            var strFim = fim.ToUniversalTime().ToString("yyyyMMddTHHmmssZ", CultureInfo.InvariantCulture);

            var reportXml = $"""
            <?xml version="1.0" encoding="utf-8" ?>
            <c:calendar-query xmlns:d="DAV:" xmlns:c="urn:ietf:params:xml:ns:caldav">
              <d:prop>
                <d:getetag />
                <c:calendar-data />
              </d:prop>
              <c:filter>
                <c:comp-filter name="VCALENDAR">
                  <c:comp-filter name="VEVENT">
                    <c:time-range start="{strInicio}" end="{strFim}" />
                  </c:comp-filter>
                </c:comp-filter>
              </c:filter>
            </c:calendar-query>
            """;

            using var request = new HttpRequestMessage(new HttpMethod("REPORT"), calendarUrl);
            request.Headers.Add("Depth", "1");
            request.Content = new StringContent(reportXml, Encoding.UTF8, "application/xml");
            AplicarAutenticacao(request);

            var response = await _httpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode && (int)response.StatusCode != 207)
                return eventos;

            var xml = await response.Content.ReadAsStringAsync(cancellationToken);
            eventos = ExtrairEventosDoMultiStatusXml(xml, calendarUrl);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ICloudCalDav] Erro ao obter eventos: {ex.Message}");
        }

        return eventos;
    }

    public async Task<ICloudEventoRemoto?> ObterEventoPorUidAsync(string uid, CancellationToken cancellationToken = default)
    {
        try
        {
            var calendarUrl = await ObterOuDescobrirUrlCalendarioAsync(cancellationToken);
            if (string.IsNullOrEmpty(calendarUrl))
                return null;

            var eventUrl = FormatarUrlEvento(calendarUrl, uid);
            using var request = new HttpRequestMessage(HttpMethod.Get, eventUrl);
            AplicarAutenticacao(request);

            var response = await _httpClient.SendAsync(request, cancellationToken);
            if (response.StatusCode == HttpStatusCode.NotFound)
                return null;

            if (!response.IsSuccessStatusCode)
                return null;

            var etag = response.Headers.ETag?.Tag?.Trim('\"');
            var ics = await response.Content.ReadAsStringAsync(cancellationToken);

            var evento = ParsearIcs(ics, uid, etag, eventUrl);
            return evento;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ICloudCalDav] Erro ao obter evento {uid}: {ex.Message}");
            return null;
        }
    }

    public async Task<ICloudRespostaOperacao> CriarEventoAsync(ICloudEventoRemoto evento, CancellationToken cancellationToken = default)
    {
        return await GravarEventoAsync(evento, ifMatchEtag: null, isCriacao: true, cancellationToken);
    }

    public async Task<ICloudRespostaOperacao> AtualizarEventoAsync(ICloudEventoRemoto evento, string? ifMatchEtag = null, CancellationToken cancellationToken = default)
    {
        return await GravarEventoAsync(evento, ifMatchEtag, isCriacao: false, cancellationToken);
    }

    private async Task<ICloudRespostaOperacao> GravarEventoAsync(ICloudEventoRemoto evento, string? ifMatchEtag, bool isCriacao, CancellationToken cancellationToken)
    {
        try
        {
            var calendarUrl = await ObterOuDescobrirUrlCalendarioAsync(cancellationToken);
            if (string.IsNullOrEmpty(calendarUrl))
                return ICloudRespostaOperacao.CriarFalha("URL do calendário iCloud não identificada.");

            var eventUrl = FormatarUrlEvento(calendarUrl, evento.Uid);
            var icsContent = MontarIcs(evento);

            using var request = new HttpRequestMessage(HttpMethod.Put, eventUrl);
            request.Content = new StringContent(icsContent, Encoding.UTF8, "text/calendar");

            if (!string.IsNullOrEmpty(ifMatchEtag))
            {
                request.Headers.TryAddWithoutValidation("If-Match", $"\"{ifMatchEtag.Trim('\"')}\"");
            }
            else if (isCriacao)
            {
                request.Headers.TryAddWithoutValidation("If-None-Match", "*");
            }

            AplicarAutenticacao(request);

            var response = await _httpClient.SendAsync(request, cancellationToken);

            if (response.IsSuccessStatusCode || response.StatusCode == HttpStatusCode.Created || response.StatusCode == HttpStatusCode.NoContent)
            {
                var etag = response.Headers.ETag?.Tag?.Trim('\"') ?? $"\"{DateTime.UtcNow.Ticks}\"";
                return ICloudRespostaOperacao.CriarSucesso(evento.Uid, etag, eventUrl, (int)response.StatusCode);
            }

            var erroDetalhe = await response.Content.ReadAsStringAsync(cancellationToken);
            return ICloudRespostaOperacao.CriarFalha($"Falha ao salvar evento no iCloud: HTTP {(int)response.StatusCode} {response.ReasonPhrase} - {erroDetalhe}", (int)response.StatusCode);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ICloudCalDav] Erro ao gravar evento {evento.Uid}: {ex.Message}");
            return ICloudRespostaOperacao.CriarFalha(ex.Message);
        }
    }

    public async Task<ICloudRespostaOperacao> ExcluirEventoAsync(string uid, string? ifMatchEtag = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var calendarUrl = await ObterOuDescobrirUrlCalendarioAsync(cancellationToken);
            if (string.IsNullOrEmpty(calendarUrl))
                return ICloudRespostaOperacao.CriarFalha("URL do calendário iCloud não identificada.");

            var eventUrl = FormatarUrlEvento(calendarUrl, uid);
            using var request = new HttpRequestMessage(HttpMethod.Delete, eventUrl);

            if (!string.IsNullOrEmpty(ifMatchEtag))
            {
                request.Headers.TryAddWithoutValidation("If-Match", $"\"{ifMatchEtag.Trim('\"')}\"");
            }

            AplicarAutenticacao(request);

            var response = await _httpClient.SendAsync(request, cancellationToken);
            if (response.IsSuccessStatusCode || response.StatusCode == HttpStatusCode.NotFound || response.StatusCode == HttpStatusCode.NoContent)
            {
                return ICloudRespostaOperacao.CriarSucesso(uid, null, eventUrl, (int)response.StatusCode);
            }

            return ICloudRespostaOperacao.CriarFalha($"Erro ao excluir no iCloud: HTTP {(int)response.StatusCode}", (int)response.StatusCode);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ICloudCalDav] Erro ao excluir evento {uid}: {ex.Message}");
            return ICloudRespostaOperacao.CriarFalha(ex.Message);
        }
    }

    private async Task<string?> ObterOuDescobrirUrlCalendarioAsync(CancellationToken cancellationToken)
    {
        if (!string.IsNullOrEmpty(_urlCalendarioPrincipal))
            return _urlCalendarioPrincipal;

        try
        {
            // 1. PROPFIND no CalDavBaseUrl para current-user-principal
            var principalXml = """
            <?xml version="1.0" encoding="utf-8" ?>
            <d:propfind xmlns:d="DAV:">
              <d:prop>
                <d:current-user-principal />
              </d:prop>
            </d:propfind>
            """;

            using var req1 = new HttpRequestMessage(new HttpMethod("PROPFIND"), CalDavBaseUrl + "/");
            req1.Headers.Add("Depth", "0");
            req1.Content = new StringContent(principalXml, Encoding.UTF8, "application/xml");
            AplicarAutenticacao(req1);

            var resp1 = await _httpClient.SendAsync(req1, cancellationToken);
            var xml1 = await resp1.Content.ReadAsStringAsync(cancellationToken);

            var principalHref = Regex.Match(xml1, @"<[^>]*current-user-principal[^>]*>\s*<[^>]*href[^>]*>([^<]+)<", RegexOptions.IgnoreCase).Groups[1].Value.Trim();
            if (string.IsNullOrEmpty(principalHref))
            {
                // Fallback padrão iCloud
                principalHref = $"/{_email?.Split('@')[0]}/principal/";
            }

            var principalFullUrl = principalHref.StartsWith("http", StringComparison.OrdinalIgnoreCase) 
                ? principalHref 
                : CalDavBaseUrl + (principalHref.StartsWith('/') ? "" : "/") + principalHref;

            // 2. PROPFIND no principal para calendar-home-set
            var homeXml = """
            <?xml version="1.0" encoding="utf-8" ?>
            <d:propfind xmlns:d="DAV:" xmlns:c="urn:ietf:params:xml:ns:caldav">
              <d:prop>
                <c:calendar-home-set />
              </d:prop>
            </d:propfind>
            """;

            using var req2 = new HttpRequestMessage(new HttpMethod("PROPFIND"), principalFullUrl);
            req2.Headers.Add("Depth", "0");
            req2.Content = new StringContent(homeXml, Encoding.UTF8, "application/xml");
            AplicarAutenticacao(req2);

            var resp2 = await _httpClient.SendAsync(req2, cancellationToken);
            var xml2 = await resp2.Content.ReadAsStringAsync(cancellationToken);

            var calendarHomeHref = Regex.Match(xml2, @"<[^>]*calendar-home-set[^>]*>\s*<[^>]*href[^>]*>([^<]+)<", RegexOptions.IgnoreCase).Groups[1].Value.Trim();
            if (string.IsNullOrEmpty(calendarHomeHref))
            {
                calendarHomeHref = principalHref.Replace("/principal/", "/calendars/");
            }

            var calendarHomeFullUrl = calendarHomeHref.StartsWith("http", StringComparison.OrdinalIgnoreCase)
                ? calendarHomeHref
                : CalDavBaseUrl + (calendarHomeHref.StartsWith('/') ? "" : "/") + calendarHomeHref;

            // 3. PROPFIND Depth 1 no calendar-home para listar coleções de calendário
            var propfindListXml = """
            <?xml version="1.0" encoding="utf-8" ?>
            <d:propfind xmlns:d="DAV:" xmlns:c="urn:ietf:params:xml:ns:caldav">
              <d:prop>
                <d:displayname />
                <d:resourcetype />
              </d:prop>
            </d:propfind>
            """;

            using var req3 = new HttpRequestMessage(new HttpMethod("PROPFIND"), calendarHomeFullUrl);
            req3.Headers.Add("Depth", "1");
            req3.Content = new StringContent(propfindListXml, Encoding.UTF8, "application/xml");
            AplicarAutenticacao(req3);

            var resp3 = await _httpClient.SendAsync(req3, cancellationToken);
            var xml3 = await resp3.Content.ReadAsStringAsync(cancellationToken);

            string shardBaseUrl = CalDavBaseUrl;
            if (Uri.TryCreate(calendarHomeFullUrl, UriKind.Absolute, out var parsedUri))
            {
                shardBaseUrl = $"{parsedUri.Scheme}://{parsedUri.Authority}";
            }

            // Procura coleções de calendário com displayname e href
            var responses = Regex.Matches(xml3, @"<[^>]*response[^>]*>[\s\S]*?</[^>]*response>", RegexOptions.IgnoreCase);

            string? chosenCalendarHref = null;
            string? fallbackCalendarHref = null;

            foreach (Match r in responses)
            {
                var block = r.Value;
                if (!Regex.IsMatch(block, @"<[^>]*resourcetype[^>]*>[\s\S]*?<[^>]*calendar[^>]*>", RegexOptions.IgnoreCase))
                    continue;

                var href = Regex.Match(block, @"<[^>]*href[^>]*>([^<]+)<", RegexOptions.IgnoreCase).Groups[1].Value.Trim();
                if (string.IsNullOrEmpty(href) || href.TrimEnd('/').EndsWith("/calendars", StringComparison.OrdinalIgnoreCase))
                    continue;

                var displayName = Regex.Match(block, @"<[^>]*displayname[^>]*>([^<]+)<", RegexOptions.IgnoreCase).Groups[1].Value.Trim();

                // Preferir calendário de Trabalho / Salão
                if (Regex.IsMatch(displayName, @"trabalho|sal[aã]o|work", RegexOptions.IgnoreCase))
                {
                    chosenCalendarHref = href;
                    break;
                }

                // Ou calendário Pessoal / Home
                if (Regex.IsMatch(displayName, @"pessoal|home", RegexOptions.IgnoreCase))
                {
                    fallbackCalendarHref = href;
                }
                else if (fallbackCalendarHref == null)
                {
                    fallbackCalendarHref = href;
                }
            }

            chosenCalendarHref ??= fallbackCalendarHref;

            if (string.IsNullOrEmpty(chosenCalendarHref))
            {
                chosenCalendarHref = calendarHomeHref.TrimEnd('/') + "/home/";
            }

            _urlCalendarioPrincipal = chosenCalendarHref.StartsWith("http", StringComparison.OrdinalIgnoreCase)
                ? chosenCalendarHref
                : shardBaseUrl + (chosenCalendarHref.StartsWith('/') ? "" : "/") + chosenCalendarHref;

            return _urlCalendarioPrincipal;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ICloudCalDav] Erro ao descobrir calendário: {ex.Message}");
            return null;
        }
    }

    private static string FormatarUrlEvento(string calendarUrl, string uid)
    {
        var cleanUrl = calendarUrl.TrimEnd('/');
        var cleanUid = uid.EndsWith(".ics", StringComparison.OrdinalIgnoreCase) ? uid : $"{uid}.ics";
        return $"{cleanUrl}/{cleanUid}";
    }

    private static string MontarIcs(ICloudEventoRemoto evento)
    {
        var sb = new StringBuilder();
        sb.AppendLine("BEGIN:VCALENDAR");
        sb.AppendLine("VERSION:2.0");
        sb.AppendLine("PRODID:-//GerenciadorSalao//SincronizadorIPhone//PT");
        sb.AppendLine("CALSCALE:GREGORIAN");
        sb.AppendLine("BEGIN:VEVENT");
        sb.AppendLine($"UID:{evento.Uid}");
        sb.AppendLine($"DTSTAMP:{DateTime.UtcNow:yyyyMMddTHHmmssZ}");
        sb.AppendLine($"DTSTART:{evento.DataHoraInicio.ToUniversalTime():yyyyMMddTHHmmssZ}");
        sb.AppendLine($"DTEND:{evento.DataHoraFim.ToUniversalTime():yyyyMMddTHHmmssZ}");
        sb.AppendLine($"SUMMARY:{EscaparIcs(evento.Titulo)}");

        if (!string.IsNullOrWhiteSpace(evento.Descricao))
        {
            sb.AppendLine($"DESCRIPTION:{EscaparIcs(evento.Descricao)}");
        }

        sb.AppendLine(evento.IsCancelado ? "STATUS:CANCELLED" : "STATUS:CONFIRMED");
        sb.AppendLine("END:VEVENT");
        sb.AppendLine("END:VCALENDAR");
        return sb.ToString();
    }

    private static string EscaparIcs(string texto)
    {
        return texto
            .Replace("\\", "\\\\")
            .Replace(";", "\\;")
            .Replace(",", "\\,")
            .Replace("\r\n", "\\n")
            .Replace("\n", "\\n");
    }

    private static string DesescaparIcs(string texto)
    {
        return texto
            .Replace("\\n", "\n")
            .Replace("\\,", ",")
            .Replace("\\;", ";")
            .Replace("\\\\", "\\");
    }

    private static List<ICloudEventoRemoto> ExtrairEventosDoMultiStatusXml(string xml, string baseCalendarUrl)
    {
        var dict = new Dictionary<string, ICloudEventoRemoto>(StringComparer.OrdinalIgnoreCase);
        var responses = Regex.Matches(xml, @"<[^>]*response[^>]*>([\s\S]*?)</[^>]*response>", RegexOptions.IgnoreCase);

        foreach (Match resp in responses)
        {
            var bloco = resp.Groups[1].Value;
            var href = Regex.Match(bloco, @"<[^>]*href[^>]*>([^<]+)<", RegexOptions.IgnoreCase).Groups[1].Value.Trim();
            var etag = Regex.Match(bloco, @"<[^>]*getetag[^>]*>([^<]+)<", RegexOptions.IgnoreCase).Groups[1].Value.Trim('"', ' ');
            var ics = Regex.Match(bloco, @"<[^>]*calendar-data[^>]*>([\s\S]*?)</[^>]*calendar-data>", RegexOptions.IgnoreCase).Groups[1].Value;

            if (string.IsNullOrWhiteSpace(ics))
                continue;

            // Descodifica entidades XML se houver
            ics = WebUtility.HtmlDecode(ics);

            var uid = Regex.Match(ics, @"\bUID:([^\r\n]+)", RegexOptions.IgnoreCase).Groups[1].Value.Trim();
            if (string.IsNullOrEmpty(uid))
            {
                uid = Path.GetFileNameWithoutExtension(href);
            }

            var ev = ParsearIcs(ics, uid, etag, href);
            if (ev != null && !string.IsNullOrWhiteSpace(ev.Uid))
            {
                // Se houver mais de uma resposta para o mesmo UID (ex.: componentes de recorrência ou multi-status do iCloud),
                // mantém o evento mais recente sem duplicar a lista
                if (!dict.TryGetValue(ev.Uid, out var existente) || (ev.UltimaModificacao ?? DateTime.MinValue) >= (existente.UltimaModificacao ?? DateTime.MinValue))
                {
                    dict[ev.Uid] = ev;
                }
            }
        }

        return dict.Values.ToList();
    }

    private static ICloudEventoRemoto? ParsearIcs(string ics, string fallbackUid, string? etag, string? url)
    {
        try
        {
            var uidMatch = Regex.Match(ics, @"\bUID:([^\r\n]+)", RegexOptions.IgnoreCase);
            var uid = uidMatch.Success ? uidMatch.Groups[1].Value.Trim() : fallbackUid;

            var summaryMatch = Regex.Match(ics, @"\bSUMMARY:([^\r\n]+)", RegexOptions.IgnoreCase);
            var summary = summaryMatch.Success ? DesescaparIcs(summaryMatch.Groups[1].Value.Trim()) : "Agendamento iPhone";

            var descMatch = Regex.Match(ics, @"\bDESCRIPTION:([^\r\n]+(?:\r?\n [^\r\n]+)*)", RegexOptions.IgnoreCase);
            var description = descMatch.Success ? DesescaparIcs(Regex.Replace(descMatch.Groups[1].Value, @"\r?\n ", "")) : null;

            var statusMatch = Regex.Match(ics, @"\bSTATUS:([^\r\n]+)", RegexOptions.IgnoreCase);
            var status = statusMatch.Success ? statusMatch.Groups[1].Value.Trim() : "CONFIRMED";
            var isCancelado = string.Equals(status, "CANCELLED", StringComparison.OrdinalIgnoreCase);

            var dtStartMatch = Regex.Match(ics, @"\bDTSTART[^:]*:([^\r\n]+)", RegexOptions.IgnoreCase);
            var dtEndMatch = Regex.Match(ics, @"\bDTEND[^:]*:([^\r\n]+)", RegexOptions.IgnoreCase);

            DateTime inicio = DateTime.Today.AddHours(9);
            DateTime fim = inicio.AddMinutes(30);

            if (dtStartMatch.Success && TryParseIcsDate(dtStartMatch.Groups[1].Value.Trim(), out var dtInicio))
            {
                inicio = dtInicio;
                if (dtEndMatch.Success && TryParseIcsDate(dtEndMatch.Groups[1].Value.Trim(), out var dtFim))
                {
                    fim = dtFim;
                }
                else
                {
                    fim = inicio.AddMinutes(30);
                }
            }

            return new ICloudEventoRemoto
            {
                Uid = uid,
                ETag = etag,
                Url = url,
                Titulo = summary,
                Descricao = description,
                DataHoraInicio = inicio,
                DataHoraFim = fim,
                IsCancelado = isCancelado,
                RawIcs = ics
            };
        }
        catch
        {
            return null;
        }
    }

    private static bool TryParseIcsDate(string raw, out DateTime data)
    {
        data = DateTime.MinValue;
        string[] formats =
        {
            "yyyyMMddTHHmmssZ",
            "yyyyMMddTHHmmss",
            "yyyyMMdd"
        };

        foreach (var fmt in formats)
        {
            if (DateTime.TryParseExact(raw, fmt, CultureInfo.InvariantCulture, 
                raw.EndsWith('Z') ? DateTimeStyles.AdjustToUniversal : DateTimeStyles.None, out var dt))
            {
                data = raw.EndsWith('Z') ? dt.ToLocalTime() : dt;
                return true;
            }
        }

        return DateTime.TryParse(raw, out data);
    }

    public void Dispose()
    {
        _httpClient.Dispose();
    }
}
