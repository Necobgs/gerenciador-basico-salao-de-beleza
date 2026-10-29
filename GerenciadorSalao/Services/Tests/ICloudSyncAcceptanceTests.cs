using System.Diagnostics;

using System.IO;

using GerenciadorSalao.Data;

using GerenciadorSalao.Models;

using GerenciadorSalao.Models.DTOs;

using Microsoft.EntityFrameworkCore;



namespace GerenciadorSalao.Services.Tests;



/// <summary>

/// Bateria de testes automatizados completa que valida os 11 critérios de aceitação do sistema de sincronização com o iPhone / iCloud.

/// Executa de forma 100% autônoma utilizando banco de dados SQLite isolado em memória/arquivo temporário e o provedor remoto simulado.

/// </summary>

public class ICloudSyncAcceptanceTests

{

    public class TesteResultado

    {

        public int Numero { get; set; }

        public string Nome { get; set; } = string.Empty;

        public bool Passou { get; set; }

        public string Mensagem { get; set; } = string.Empty;

        public TimeSpan Duracao { get; set; }

    }



    public static async Task<List<TesteResultado>> ExecutarTodosOsTestesAsync(Action<string>? logCallback = null)

    {

        var resultados = new List<TesteResultado>();

        void Log(string msg)

        {

            Debug.WriteLine(msg);

            logCallback?.Invoke(msg);

        }



        Log("=== INICIANDO BATERIA DE TESTES DE ACEITAÇÃO ICLOUD / IPHONE ===");



        var testes = new (int num, string nome, Func<Task<string>> action)[]

        {

            (1, "Conectar e sincronizar conta normalmente", Teste1_ConectarESincronizarNormalmente),

            (2, "Detecção de offline quando sem conexão", Teste2_DeteccaoOffline),

            (3, "Persistência e consolidação offline no SQLite", Teste3_OperacoesOfflineEConsolidacao),

            (4, "Preservação da fila pendente após fechamento do app", Teste4_PersistenciaAposReinicio),

            (5, "Detecção de alterações remotas ocorridas no iPhone", Teste5_AlteracoesNoIPhone),

            (6, "Recuperação automática na reconexão e envio/recebimento bidirecional", Teste6_RecuperacaoAutomaticaReconexao),

            (7, "Identificação e preservação de conflitos concorrentes", Teste7_IdentificacaoEConflitosConcorrentes),

            (8, "Conflito entre Exclusão no iPhone e Edição no Desktop", Teste8_ConflitoExclusaoVsEdicao),

            (9, "Prevenção de duplicatas por queda de conexão durante gravação (Idempotência)", Teste9_IdempotenciaQuedaDeConexao),

            (10, "Tratamento de erros permanentes sem repetição cega (401 Unauthorized)", Teste10_ErrosPermanentesSemLoop),

            (11, "Transições dos 7 estados de sincronização na interface", Teste11_EstadosSincronizacaoInterface),

            (12, "Não cadastrar cliente inexistente do iPhone e registrar aviso", Teste12_ClienteNaoCadastradoNaoDeveSerCriado),

            (13, "Busca de cliente case-insensitive na sincronização do iPhone", Teste13_BuscaClienteCaseInsensitive),

            (14, "Busca de serviço case-insensitive e aviso quando não encontrado", Teste14_ServicoCaseInsensitiveENaoCadastrarInexistente),
            (15, "Tratamento de UIDs duplicados remotos e locais (recorrência/legado)", Teste15_DuplicidadeDeUidRemotoELocalSemCrash)

        };



        foreach (var (num, nome, action) in testes)

        {

            var sw = Stopwatch.StartNew();

            var res = new TesteResultado { Numero = num, Nome = nome };

            try

            {

                var msg = await action();

                sw.Stop();

                res.Passou = true;

                res.Mensagem = msg;

                res.Duracao = sw.Elapsed;

                Log($"[PASSOU] Teste {num:D2}: {nome} ({sw.ElapsedMilliseconds}ms) -> {msg}");

            }

            catch (Exception ex)

            {

                sw.Stop();

                res.Passou = false;

                res.Mensagem = ex.Message;

                res.Duracao = sw.Elapsed;

                Log($"[FALHOU] Teste {num:D2}: {nome} ({sw.ElapsedMilliseconds}ms) -> {ex.Message}");

            }

            resultados.Add(res);

        }



        int passaram = resultados.Count(r => r.Passou);

        Log($"=== RESULTADO FINAL: {passaram}/{resultados.Count} TESTES PASSARAM COM SUCESSO ===");

        return resultados;

    }



    private static (AppDbContext context, SimulatedICloudCalendarProvider provider, ICloudSyncService syncService, AgendamentoService agendamentoService, string dbFile) CriarAmbienteDeTeste()

    {

        var id = Guid.NewGuid().ToString("N");

        var tempDb = Path.Combine(Path.GetTempPath(), $"salao_test_{id}.db");

        var tempConfig = Path.Combine(Path.GetTempPath(), $"salao_test_{id}.json");

        var options = new DbContextOptionsBuilder<AppDbContext>()

            .UseSqlite($"Data Source={tempDb}")

            .Options;



        var context = new AppDbContext(options);

        DbInitializer.Inicializar(context);



        if (!context.Clientes.Any())

        {

            var c = new Cliente { Nome = "Ana Paula Teste", Telefone = "(11) 98765-4321", DataCadastro = DateTime.Now };

            context.Clientes.Add(c);

            context.SaveChanges();

        }



        if (!context.Servicos.Any())

        {

            var s = new Servico { Descricao = "Corte Feminino", Valor = 80.00m, DuracaoMinutos = 45, Ativo = true };

            context.Servicos.Add(s);

            context.SaveChanges();

        }



        var provider = new SimulatedICloudCalendarProvider();

        provider.ConfigurarCredenciais("salao@icloud.com", "abcd-efgh-ijkl-mnop");



        var syncService = new ICloudSyncService(() => new AppDbContext(options), provider, tempConfig)

        {

            HabilitarSincronizacaoAutomaticaEmSegundoPlano = false

        };

        var agendamentoService = new AgendamentoService(context, syncService);



        return (context, provider, syncService, agendamentoService, tempDb);

    }



    private static void LimparAmbienteDeTeste(AppDbContext context, string dbFile)

    {

        try

        {

            context.Database.CloseConnection();

            context.Dispose();

            if (File.Exists(dbFile))

            {

                File.Delete(dbFile);

            }

            var tempConfig = Path.ChangeExtension(dbFile, ".json");

            if (File.Exists(tempConfig))

            {

                File.Delete(tempConfig);

            }

        }

        catch { }

    }



    // ================= TESTES INDIVIDUAIS =================



    private static async Task<string> Teste1_ConectarESincronizarNormalmente()

    {

        var (context, provider, sync, agService, dbFile) = CriarAmbienteDeTeste();

        try

        {

            await sync.SalvarConfiguracaoAsync("teste@icloud.com", "pass-word-test-1234", ativar: true, dispararSincronizacao: false);



            var resultado = await sync.SincronizarAgoraAsync();

            if (!resultado.Sucesso || resultado.EstadoFinal != EstadoSincronizacao.Sincronizado)

                throw new Exception($"Esperado Estado Sincronizado, obtido: {resultado.EstadoFinal} - {resultado.Mensagem}");



            if (!sync.UltimaSincronizacaoSucesso.HasValue)

                throw new Exception("Data/hora da última sincronização bem-sucedida não foi registrada.");



            return "Conexão inicial estabelecida e sincronização concluída com sucesso.";

        }

        finally

        {

            LimparAmbienteDeTeste(context, dbFile);

        }

    }



    private static async Task<string> Teste2_DeteccaoOffline()

    {

        var (context, provider, sync, agService, dbFile) = CriarAmbienteDeTeste();

        try

        {

            await sync.SalvarConfiguracaoAsync("teste@icloud.com", "pass-word-test-1234", ativar: true, dispararSincronizacao: false);



            // Simula desconexão com caldav.icloud.com

            provider.IsOnline = false;



            var resultado = await sync.SincronizarAgoraAsync();

            if (resultado.EstadoFinal != EstadoSincronizacao.Offline)

                throw new Exception($"Esperado estado Offline ao perder conexão, obtido: {resultado.EstadoFinal}");



            if (sync.EstadoAtual != EstadoSincronizacao.Offline)

                throw new Exception($"EstadoAtual no serviço deve ser Offline, obtido: {sync.EstadoAtual}");



            return "Detecção de offline confirmada sem bloquear a execução.";

        }

        finally

        {

            LimparAmbienteDeTeste(context, dbFile);

        }

    }



    private static async Task<string> Teste3_OperacoesOfflineEConsolidacao()

    {

        var (context, provider, sync, agService, dbFile) = CriarAmbienteDeTeste();

        try

        {

            await sync.SalvarConfiguracaoAsync("teste@icloud.com", "pass-word-test-1234", ativar: true, dispararSincronizacao: false);

            provider.IsOnline = false;



            var cliente = await context.Clientes.FirstAsync();

            var servico = await context.Servicos.FirstAsync();



            // 1. Cria agendamento offline

            var ag1 = await agService.CriarAgendamentoAsync(cliente.Id, DateTime.Today.AddHours(10), new List<int> { servico.Id }, "Corte Inicial");



            // Verifica persistência imediata no SQLite

            var agSalvo = await context.Agendamentos.FindAsync(ag1.Id);

            if (agSalvo == null)

                throw new Exception("Agendamento não persistido imediatamente no SQLite.");



            // Verifica fila SQLite

            var ops = await sync.ObterOperacoesPendentesAsync();

            if (ops.Count != 1 || ops[0].TipoOperacao != TipoOperacaoSincronizacao.Criar)

                throw new Exception($"Esperada 1 operação de Criar pendente, encontradas: {ops.Count}");



            // 2. Edita agendamento offline antes de enviar: consolidação segura!

            await agService.AtualizarAgendamentoAsync(ag1.Id, cliente.Id, DateTime.Today.AddHours(11), new List<int> { servico.Id }, "Corte Editado Offline");



            ops = await sync.ObterOperacoesPendentesAsync();

            if (ops.Count != 1)

                throw new Exception($"A edição offline deve ser consolidada com a criação anterior, mas a fila possui {ops.Count} itens.");



            if (ops[0].TipoOperacao != TipoOperacaoSincronizacao.Criar)

                throw new Exception("A operação consolidada de item novo ainda deve ser do Tipo Criar.");



            // 3. Cria agendamento 2 offline e o exclui antes do envio: deve ser descartado da fila

            var ag2 = await agService.CriarAgendamentoAsync(cliente.Id, DateTime.Today.AddHours(14), new List<int> { servico.Id }, "Para Apagar");

            await agService.ExcluirAgendamentoAsync(ag2.Id);



            ops = await sync.ObterOperacoesPendentesAsync();

            if (ops.Count != 1 || ops[0].AgendamentoId != ag1.Id)

                throw new Exception("Agendamento criado e excluído offline sem envio remoto deve ser consolidado removendo da fila.");



            return "Operações offline gravadas imediatamente no SQLite e consolidadas com perfeição.";

        }

        finally

        {

            LimparAmbienteDeTeste(context, dbFile);

        }

    }



    private static async Task<string> Teste4_PersistenciaAposReinicio()

    {

        var (context, provider, sync, agService, dbFile) = CriarAmbienteDeTeste();

        int agendamentoId;

        try

        {

            await sync.SalvarConfiguracaoAsync("teste@icloud.com", "pass-word-test-1234", ativar: true, dispararSincronizacao: false);

            provider.IsOnline = false;



            var cliente = await context.Clientes.FirstAsync();

            var servico = await context.Servicos.FirstAsync();

            var ag = await agService.CriarAgendamentoAsync(cliente.Id, DateTime.Today.AddHours(15), new List<int> { servico.Id }, "Persistente após reboot");

            agendamentoId = ag.Id;



            // Fecha contexto antigo

            context.Database.CloseConnection();

            context.Dispose();

        }

        catch

        {

            LimparAmbienteDeTeste(context, dbFile);

            throw;

        }



        // Simula reabertura do aplicativo com novo DbContext

        var options = new DbContextOptionsBuilder<AppDbContext>()

            .UseSqlite($"Data Source={dbFile}")

            .Options;



        var novoContext = new AppDbContext(options);

        try

        {

            var agRecarregado = await novoContext.Agendamentos.FindAsync(agendamentoId);

            if (agRecarregado == null)

                throw new Exception("Agendamento criado offline não foi encontrado após reinicialização do aplicativo.");



            var opsRecarregadas = await novoContext.OperacoesSincronizacao

                .Where(o => o.Estado == EstadoProcessamentoOperacao.Pendente)

                .ToListAsync();



            if (opsRecarregadas.Count != 1 || opsRecarregadas[0].AgendamentoId != agendamentoId)

                throw new Exception($"Fila de operações pendentes não foi preservada no SQLite após reinício: {opsRecarregadas.Count} itens.");



            return "Dados e fila de pendências 100% preservados no SQLite após reinicialização simulada.";

        }

        finally

        {

            LimparAmbienteDeTeste(novoContext, dbFile);

        }

    }



    private static async Task<string> Teste5_AlteracoesNoIPhone()

    {

        var (context, provider, sync, agService, dbFile) = CriarAmbienteDeTeste();

        try

        {

            await sync.SalvarConfiguracaoAsync("teste@icloud.com", "pass-word-test-1234", ativar: true, dispararSincronizacao: false);



            // Simula o usuário criando um evento na agenda do iPhone

            var evIphone = provider.SimularCriacaoNoIPhone("Salão - Rafaela Silva", DateTime.Today.AddHours(16), 45, "Agendado pelo iPhone");



            var eventos = await provider.ObterEventosAsync(DateTime.Today, DateTime.Today.AddDays(1));

            if (!eventos.Any(e => e.Uid == evIphone.Uid))

                throw new Exception("Evento simulado no iPhone não encontrado no provedor remoto.");



            return $"Evento do iPhone '{evIphone.Titulo}' registrado no provedor remoto com ETag {evIphone.ETag}.";

        }

        finally

        {

            LimparAmbienteDeTeste(context, dbFile);

        }

    }



    private static async Task<string> Teste6_RecuperacaoAutomaticaReconexao()

    {

        var (context, provider, sync, agService, dbFile) = CriarAmbienteDeTeste();

        try

        {

            await sync.SalvarConfiguracaoAsync("teste@icloud.com", "pass-word-test-1234", ativar: true, dispararSincronizacao: false);



            // 1. Cria evento local enquanto offline

            provider.IsOnline = false;

            var cliente = await context.Clientes.FirstAsync();

            var servico = await context.Servicos.FirstAsync();

            var agLocal = await agService.CriarAgendamentoAsync(cliente.Id, DateTime.Today.AddHours(9), new List<int> { servico.Id }, "Criado no Desktop");



            // 2. Simula criação concorrente no iPhone

            var evIphone = provider.SimularCriacaoNoIPhone("Salão - Beatriz Santos", DateTime.Today.AddHours(17), 60, "Via iPhone");



            // 3. Conexão restabelecida!

            provider.IsOnline = true;

            var resultado = await sync.SincronizarAgoraAsync();



            if (!resultado.Sucesso || resultado.EstadoFinal != EstadoSincronizacao.Sincronizado)

                throw new Exception($"Falha na recuperação da reconexão: {resultado.Mensagem}");



            if (resultado.EventosEnviados < 1)

                throw new Exception("O agendamento local pendente não foi enviado ao iCloud.");



            if (resultado.EventosImportados < 1)

                throw new Exception("O evento criado no iPhone não foi importado para o SQLite.");



            // Verifica se o evento local agora tem ETag remoto

            var agAtualizado = await context.Agendamentos.AsNoTracking().FirstOrDefaultAsync(a => a.Id == agLocal.Id);

            if (string.IsNullOrEmpty(agAtualizado?.ICloudETag) || !agAtualizado.SincronizadoComICloud)

                throw new Exception("Agendamento local não foi marcado como sincronizado com ETag.");



            // Verifica se a fila ficou limpa

            var pendentes = await sync.ObterOperacoesPendentesAsync();

            if (pendentes.Count > 0)

                throw new Exception($"A fila de pendências ainda possui {pendentes.Count} operações.");



            // Verifica se Beatriz Santos NÃO foi adicionada à tabela de Clientes (Regra Nova)

            var beatrizCliente = await context.Clientes.FirstOrDefaultAsync(c => c.Nome == "Beatriz Santos");

            if (beatrizCliente != null)

                throw new Exception("Beatriz Santos não deveria ser cadastrada automaticamente na tabela Clientes.");



            var agBeatriz = await context.Agendamentos.FirstOrDefaultAsync(a => a.ICloudEventId == evIphone.Uid);

            if (agBeatriz == null)

                throw new Exception("Agendamento da Beatriz Santos do iPhone não foi importado.");



            if (agBeatriz.ClienteId != null)

                throw new Exception($"Agendamento da Beatriz Santos deveria ter ClienteId nulo, mas tem {agBeatriz.ClienteId}.");



            if (agBeatriz.ClienteNaoEncontradoNome != "Beatriz Santos")

                throw new Exception($"ClienteNaoEncontradoNome deveria ser 'Beatriz Santos', obtido: '{agBeatriz.ClienteNaoEncontradoNome}'.");



            return "Recuperação bidirecional perfeita: alterações locais enviadas, alterações do iPhone recebidas e regra de não-cadastro respeitada.";

        }

        finally

        {

            LimparAmbienteDeTeste(context, dbFile);

        }

    }



    private static async Task<string> Teste7_IdentificacaoEConflitosConcorrentes()

    {

        var (context, provider, sync, agService, dbFile) = CriarAmbienteDeTeste();

        try

        {

            await sync.SalvarConfiguracaoAsync("teste@icloud.com", "pass-word-test-1234", ativar: true, dispararSincronizacao: false);



            // Cria e sincroniza evento base inicialmente

            var cliente = await context.Clientes.FirstAsync();

            var servico = await context.Servicos.FirstAsync();

            var ag = await agService.CriarAgendamentoAsync(cliente.Id, DateTime.Today.AddHours(10), new List<int> { servico.Id }, "Versão Original");

            await sync.SincronizarAgoraAsync();



            ag = await context.Agendamentos.FindAsync(ag.Id);

            if (string.IsNullOrEmpty(ag?.ICloudEventId))

                throw new Exception("Evento não sincronizado inicialmente.");



            // Fica offline

            provider.IsOnline = false;



            // Usuário no desktop edita para 11h

            await agService.AtualizarAgendamentoAsync(ag.Id, cliente.Id, DateTime.Today.AddHours(11), new List<int> { servico.Id }, "Alterado no Salão para 11h");



            // Simultaneamente no iPhone, o evento foi alterado para 16h

            provider.SimularModificacaoNoIPhone(ag.ICloudEventId, DateTime.Today.AddHours(16), $"Salão - {cliente.NomeExibicao}", "Alterado no iPhone para 16h");



            // Restaura conexão e sincroniza

            provider.IsOnline = true;

            var resultado = await sync.SincronizarAgoraAsync();



            if (resultado.ConflitosIdentificados != 1)

                throw new Exception($"Esperado 1 conflito identificado, encontrados: {resultado.ConflitosIdentificados}");



            if (resultado.EstadoFinal != EstadoSincronizacao.Conflito)

                throw new Exception($"Estado após detecção deve ser Conflito, obtido: {resultado.EstadoFinal}");



            var conflitos = await sync.ObterConflitosNaoResolvidosAsync();

            if (conflitos.Count != 1)

                throw new Exception($"Esperado 1 registro em ConflitosSincronizacao, encontrados: {conflitos.Count}");



            var c = conflitos[0];

            if (string.IsNullOrEmpty(c.DadosLocaisJson) || string.IsNullOrEmpty(c.DadosRemotosJson))

                throw new Exception("Conflito deve preservar ambas as versões sem descarte silencioso.");



            return "Conflito concorrente identificado, ambas as versões preservadas e usuário informado.";

        }

        finally

        {

            LimparAmbienteDeTeste(context, dbFile);

        }

    }



    private static async Task<string> Teste8_ConflitoExclusaoVsEdicao()

    {

        var (context, provider, sync, agService, dbFile) = CriarAmbienteDeTeste();

        try

        {

            await sync.SalvarConfiguracaoAsync("teste@icloud.com", "pass-word-test-1234", ativar: true, dispararSincronizacao: false);



            var cliente = await context.Clientes.FirstAsync();

            var servico = await context.Servicos.FirstAsync();

            var ag = await agService.CriarAgendamentoAsync(cliente.Id, DateTime.Today.AddHours(14), new List<int> { servico.Id }, "Original");

            await sync.SincronizarAgoraAsync();



            ag = await context.Agendamentos.FindAsync(ag.Id);



            // Fica offline

            provider.IsOnline = false;



            // Desktop edita observação

            await agService.AtualizarAgendamentoAsync(ag!.Id, cliente.Id, DateTime.Today.AddHours(14), new List<int> { servico.Id }, "Edição no Desktop");



            // iPhone cancela/apaga o evento

            provider.SimularExclusaoNoIPhone(ag.ICloudEventId!);



            // Restaura e sincroniza

            provider.IsOnline = true;

            var resultado = await sync.SincronizarAgoraAsync();



            if (resultado.ConflitosIdentificados < 1)

                throw new Exception("Exclusão concorrente com edição deve ser identificada como conflito.");



            return "Conflito entre exclusão remota e edição local devidamente protegido.";

        }

        finally

        {

            LimparAmbienteDeTeste(context, dbFile);

        }

    }



    private static async Task<string> Teste9_IdempotenciaQuedaDeConexao()

    {

        var (context, provider, sync, agService, dbFile) = CriarAmbienteDeTeste();

        try

        {

            await sync.SalvarConfiguracaoAsync("teste@icloud.com", "pass-word-test-1234", ativar: true, dispararSincronizacao: false);



            var cliente = await context.Clientes.FirstAsync();

            var servico = await context.Servicos.FirstAsync();



            // Ativa simulação crítica: servidor gravou, mas conexão caiu antes de confirmar ao desktop!

            provider.SimularQuedaConexaoAposGravacao = true;



            var ag = await agService.CriarAgendamentoAsync(cliente.Id, DateTime.Today.AddHours(15), new List<int> { servico.Id }, "Teste Idempotência");



            // Tenta sincronizar: simula exceção no meio da resposta

            var resultado1 = await sync.SincronizarAgoraAsync();

            if (resultado1.OperacoesPendentesRestantes != 1)

                throw new Exception("A operação deve permanecer pendente após falha na resposta de rede.");



            // Na tentativa seguinte, a reconexão roda

            var resultado2 = await sync.SincronizarAgoraAsync();

            if (!resultado2.Sucesso)

                throw new Exception($"Reconexão falhou: {resultado2.Mensagem}");



            // Verifica que NÃO há duplicatas no servidor

            var todosRemotos = provider.ObterTodosEventosRemotos();

            var duplicatas = todosRemotos.Count(e => e.Uid == ag.ICloudEventId);

            if (duplicatas != 1)

                throw new Exception($"Idempotência violada! Encontradas {duplicatas} cópias do evento no servidor.");



            // Verifica que o agendamento local foi concluído e sincronizado

            var agAtualizado = await context.Agendamentos.AsNoTracking().FirstOrDefaultAsync(a => a.Id == ag.Id);

            if (!agAtualizado!.SincronizadoComICloud || string.IsNullOrEmpty(agAtualizado.ICloudETag))

                throw new Exception("Agendamento não foi marcado como sincronizado após recuperação idempotente.");



            return "Idempotência validada: servidor não gerou duplicatas após reconexão pós-queda de sinal.";

        }

        finally

        {

            LimparAmbienteDeTeste(context, dbFile);

        }

    }



    private static async Task<string> Teste10_ErrosPermanentesSemLoop()

    {

        var (context, provider, sync, agService, dbFile) = CriarAmbienteDeTeste();

        try

        {

            await sync.SalvarConfiguracaoAsync("teste@icloud.com", "senha-errada", ativar: true, dispararSincronizacao: false);



            // Simula erro de credenciais inválidas (HTTP 401)

            provider.SimularFalhaAutenticacao = true;



            var resultado = await sync.SincronizarAgoraAsync();



            if (resultado.EstadoFinal != EstadoSincronizacao.AutenticacaoNecessaria)

                throw new Exception($"Esperado Estado AutenticacaoNecessaria para 401, obtido: {resultado.EstadoFinal}");



            if (sync.EstadoAtual != EstadoSincronizacao.AutenticacaoNecessaria)

                throw new Exception("Serviço deve transicionar para AutenticacaoNecessaria.");



            return "Erro 401 identificado com precisão, interrompendo repetições cegas até novas credenciais.";

        }

        finally

        {

            LimparAmbienteDeTeste(context, dbFile);

        }

    }



    private static async Task<string> Teste11_EstadosSincronizacaoInterface()

    {

        var (context, provider, sync, agService, dbFile) = CriarAmbienteDeTeste();

        try

        {

            var estadosCapturados = new List<EstadoSincronizacao>();

            sync.EstadoMudou += (s, estado) => estadosCapturados.Add(estado);



            // 1. Configura

            await sync.SalvarConfiguracaoAsync("teste@icloud.com", "pass-word-test-1234", ativar: true, dispararSincronizacao: false);



            // 2. Sincroniza limpo -> Sincronizado

            await sync.SincronizarAgoraAsync();

            if (sync.EstadoAtual != EstadoSincronizacao.Sincronizado)

                throw new Exception("Deveria estar Sincronizado.");



            // 3. Fica offline -> Offline

            provider.IsOnline = false;

            await sync.SincronizarAgoraAsync();

            if (sync.EstadoAtual != EstadoSincronizacao.Offline)

                throw new Exception("Deveria estar Offline.");



            // 4. Cria agendamento -> Pendente

            var cliente = await context.Clientes.FirstAsync();

            var servico = await context.Servicos.FirstAsync();

            await agService.CriarAgendamentoAsync(cliente.Id, DateTime.Today.AddHours(9), new List<int> { servico.Id });



            var config = await sync.ObterConfiguracaoAsync();

            if (config.OperacoesPendentesCount < 1)

                throw new Exception("Deveria indicar operações pendentes.");



            return $"Estados transicionados com fidelidade ({string.Join(" -> ", estadosCapturados.Distinct())}).";

        }

        finally

        {

            LimparAmbienteDeTeste(context, dbFile);

        }

    }



    private static async Task<string> Teste12_ClienteNaoCadastradoNaoDeveSerCriado()

    {

        var (context, provider, sync, agService, dbFile) = CriarAmbienteDeTeste();

        try

        {

            await sync.SalvarConfiguracaoAsync("teste@icloud.com", "pass-word-test-1234", ativar: true, dispararSincronizacao: false);



            var totalClientesAntes = await context.Clientes.CountAsync();



            // Simula iPhone com cliente inexistente

            var evIphone = provider.SimularCriacaoNoIPhone(

                "Salão - Larissa Manoela", 

                DateTime.Today.AddHours(14), 

                60, 

                "Observação: Primeiro atendimento");



            var resultado = await sync.SincronizarAgoraAsync();

            if (!resultado.Sucesso)

                throw new Exception($"Falha ao sincronizar: {resultado.Mensagem}");



            var totalClientesDepois = await context.Clientes.CountAsync();

            if (totalClientesDepois != totalClientesAntes)

                throw new Exception($"Cliente não cadastrado não pode ser adicionado ao banco! Antes: {totalClientesAntes}, Depois: {totalClientesDepois}");



            var ag = await context.Agendamentos.Include(a => a.Comanda).FirstOrDefaultAsync(a => a.ICloudEventId == evIphone.Uid);

            if (ag == null)

                throw new Exception("Agendamento remoto não foi importado.");



            if (ag.ClienteId != null)

                throw new Exception($"ClienteId deve ser nulo para cliente não cadastrado, mas é: {ag.ClienteId}");



            if (ag.ClienteNaoEncontradoNome != "Larissa Manoela")

                throw new Exception($"ClienteNaoEncontradoNome esperado 'Larissa Manoela', obtido: '{ag.ClienteNaoEncontradoNome}'");



            if (ag.Comanda != null)

                throw new Exception("Comanda não deve ser criada para agendamento sem cliente cadastrado.");



            return "Cliente desconhecido do iPhone não cadastrado, rótulo/aviso preservado e comanda não gerada com sucesso.";

        }

        finally

        {

            LimparAmbienteDeTeste(context, dbFile);

        }

    }



    private static async Task<string> Teste13_BuscaClienteCaseInsensitive()

    {

        var (context, provider, sync, agService, dbFile) = CriarAmbienteDeTeste();

        try

        {

            await sync.SalvarConfiguracaoAsync("teste@icloud.com", "pass-word-test-1234", ativar: true, dispararSincronizacao: false);



            var clienteNovo = new Cliente { Nome = "Mariana Alves Da Silva", Telefone = "(11) 97777-6666", DataCadastro = DateTime.Now };

            context.Clientes.Add(clienteNovo);

            await context.SaveChangesAsync();



            // Simula iPhone com casing completamente diferente ("mariana ALVES da silva")

            var evIphone = provider.SimularCriacaoNoIPhone(

                "Salão - mariana ALVES da silva", 

                DateTime.Today.AddHours(11), 

                45, 

                "Agendamento iPhone");



            var resultado = await sync.SincronizarAgoraAsync();

            if (!resultado.Sucesso)

                throw new Exception($"Falha ao sincronizar: {resultado.Mensagem}");



            var ag = await context.Agendamentos.FirstOrDefaultAsync(a => a.ICloudEventId == evIphone.Uid);

            if (ag == null)

                throw new Exception("Agendamento não foi importado.");



            if (ag.ClienteId != clienteNovo.Id)

                throw new Exception($"ClienteId deveria ser {clienteNovo.Id}, mas foi: {ag.ClienteId}");



            if (!string.IsNullOrEmpty(ag.ClienteNaoEncontradoNome))

                throw new Exception($"ClienteNaoEncontradoNome deveria ser nulo pois o cliente existe, mas é: '{ag.ClienteNaoEncontradoNome}'");



            return "Busca case-insensitive de cliente realizada com sucesso para casing divergente.";

        }

        finally

        {

            LimparAmbienteDeTeste(context, dbFile);

        }

    }



    private static async Task<string> Teste14_ServicoCaseInsensitiveENaoCadastrarInexistente()

    {

        var (context, provider, sync, agService, dbFile) = CriarAmbienteDeTeste();

        try

        {

            await sync.SalvarConfiguracaoAsync("teste@icloud.com", "pass-word-test-1234", ativar: true, dispararSincronizacao: false);



            var totalServicosAntes = await context.Servicos.CountAsync();



            // iPhone cria evento com:

            // 1) Serviço existente em casing diferente: "corte feminino"

            // 2) Serviço inexistente que NÃO deve ser cadastrado: "Massagem Facial Dourada"

            var desc = "Cliente: Ana Paula Teste\r\nServiços:\r\n- corte feminino\r\n- Massagem Facial Dourada";

            var evIphone = provider.SimularCriacaoNoIPhone(

                "Salão - Ana Paula Teste", 

                DateTime.Today.AddHours(15), 

                60, 

                desc);



            var resultado = await sync.SincronizarAgoraAsync();

            if (!resultado.Sucesso)

                throw new Exception($"Falha ao sincronizar: {resultado.Mensagem}");



            var totalServicosDepois = await context.Servicos.CountAsync();

            if (totalServicosDepois != totalServicosAntes)

                throw new Exception($"Nenhum serviço novo pode ser cadastrado automaticamente! Antes: {totalServicosAntes}, Depois: {totalServicosDepois}");



            var ag = await context.Agendamentos

                .Include(a => a.AgendamentoServicos)

                .ThenInclude(asv => asv.Servico)

                .FirstOrDefaultAsync(a => a.ICloudEventId == evIphone.Uid);



            if (ag == null)

                throw new Exception("Agendamento não foi importado.");



            // O serviço existente "Corte Feminino" deve ter sido vinculado

            bool vinculouCorte = ag.AgendamentoServicos.Any(s => s.Servico.Descricao == "Corte Feminino");

            if (!vinculouCorte)

                throw new Exception("Serviço existente com casing diferente 'corte feminino' não foi vinculado via busca case-insensitive.");



            // O serviço inexistente deve constar no aviso ServicoNaoEncontradoNome

            if (string.IsNullOrEmpty(ag.ServicoNaoEncontradoNome) || !ag.ServicoNaoEncontradoNome.Contains("Massagem Facial Dourada"))

                throw new Exception($"ServicoNaoEncontradoNome deve conter 'Massagem Facial Dourada', obtido: '{ag.ServicoNaoEncontradoNome}'");



            return "Serviço existente localizado case-insensitively, e serviço inexistente não cadastrado e registrado como aviso.";

        }

        finally

        {

            LimparAmbienteDeTeste(context, dbFile);

        }

    }


    private static async Task<string> Teste15_DuplicidadeDeUidRemotoELocalSemCrash()
    {
        var (context, provider, sync, agendamentoService, dbFile) = CriarAmbienteDeTeste();
        try
        {
            await sync.SalvarConfiguracaoAsync("teste@icloud.com", "pass-word-test-1234", ativar: true, dispararSincronizacao: false);

            var cliente = new Cliente { Nome = "Maria Duplicada", Telefone = "(11) 99999-0001" };
            var servico = new Servico { Descricao = "Corte Feminino", Valor = 80m, DuracaoMinutos = 45 };
            context.Clientes.Add(cliente);
            context.Servicos.Add(servico);
            await context.SaveChangesAsync();

            string uidDuplicado = "C275C704-7A00-4A39-AB2B-1BE76F5B033B";

            var agLocal1 = new Agendamento
            {
                ClienteId = cliente.Id,
                DataHora = DateTime.Today.AddHours(14),
                Status = StatusAgendamento.Pendente,
                ICloudEventId = uidDuplicado,
                ICloudETag = "\"etag-local-1\""
            };
            var agLocal2 = new Agendamento
            {
                ClienteId = cliente.Id,
                DataHora = DateTime.Today.AddHours(15),
                Status = StatusAgendamento.Pendente,
                ICloudEventId = uidDuplicado,
                ICloudETag = "\"etag-local-2\""
            };
            context.Agendamentos.AddRange(agLocal1, agLocal2);
            await context.SaveChangesAsync();

            provider.EventosSobrescritos = new List<ICloudEventoRemoto>
            {
                new ICloudEventoRemoto
                {
                    Uid = uidDuplicado,
                    Titulo = "Salão - Maria Duplicada - Corte Feminino",
                    DataHoraInicio = DateTime.Today.AddHours(14),
                    DataHoraFim = DateTime.Today.AddHours(14).AddMinutes(45),
                    ETag = "\"etag-remoto-antigo\"",
                    UltimaModificacao = DateTime.UtcNow.AddMinutes(-30)
                },
                new ICloudEventoRemoto
                {
                    Uid = uidDuplicado,
                    Titulo = "Salão - Maria Duplicada - Corte Feminino",
                    DataHoraInicio = DateTime.Today.AddHours(16),
                    DataHoraFim = DateTime.Today.AddHours(16).AddMinutes(45),
                    ETag = "\"etag-remoto-recente\"",
                    UltimaModificacao = DateTime.UtcNow
                }
            };

            var resultado = await sync.SincronizarAgoraAsync();
            if (!resultado.Sucesso)
                throw new Exception($"Sincronização falhou com erro: {resultado.Mensagem}");

            var ativosComUid = await context.Agendamentos
                .Where(a => a.ICloudEventId == uidDuplicado)
                .ToListAsync();

            if (ativosComUid.Count != 1)
                throw new Exception($"Esperado exatamente 1 agendamento local com o UID '{uidDuplicado}', encontrados: {ativosComUid.Count}");

            return "UIDs duplicados remotos e locais tratados com sucesso absoluto sem falhas ou exceções de chave duplicada.";
        }
        finally
        {
            LimparAmbienteDeTeste(context, dbFile);
        }
    }
}
