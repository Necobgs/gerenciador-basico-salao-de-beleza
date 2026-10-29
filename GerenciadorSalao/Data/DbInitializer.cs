using GerenciadorSalao.Models;
using Microsoft.EntityFrameworkCore;

namespace GerenciadorSalao.Data;

public static class DbInitializer
{
    public static void Inicializar(AppDbContext context)
    {
        // Garante que o banco de dados e as tabelas existam
        context.Database.EnsureCreated();

        // Migração defensiva para colunas novas em Servicos (Especificacao e DuracaoFormatada)
        try
        {
            using var cmd = context.Database.GetDbConnection().CreateCommand();
            context.Database.OpenConnection();
            cmd.CommandText = "PRAGMA table_info(Servicos);";
            using var reader = cmd.ExecuteReader();
            bool hasEspecificacao = false;
            bool hasDuracaoFormatada = false;
            while (reader.Read())
            {
                var colName = reader.GetString(1);
                if (string.Equals(colName, "Especificacao", StringComparison.OrdinalIgnoreCase))
                    hasEspecificacao = true;
                if (string.Equals(colName, "DuracaoFormatada", StringComparison.OrdinalIgnoreCase))
                    hasDuracaoFormatada = true;
            }
            reader.Close();

            if (!hasEspecificacao)
            {
                cmd.CommandText = "ALTER TABLE Servicos ADD COLUMN Especificacao TEXT NULL;";
                cmd.ExecuteNonQuery();
            }
            if (!hasDuracaoFormatada)
            {
                cmd.CommandText = "ALTER TABLE Servicos ADD COLUMN DuracaoFormatada TEXT NULL;";
                cmd.ExecuteNonQuery();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[DbInitializer] Migração Servicos: {ex.Message}");
        }

        // Migração defensiva para colunas de sincronização com iCloud / iPhone em Agendamentos
        try
        {
            using var cmd = context.Database.GetDbConnection().CreateCommand();
            context.Database.OpenConnection();
            cmd.CommandText = "PRAGMA table_info(Agendamentos);";
            using var reader = cmd.ExecuteReader();
            var colunasExistentes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            bool clienteIdNotNull = false;
            while (reader.Read())
            {
                var col = reader.GetString(1);
                colunasExistentes.Add(col);
                if (string.Equals(col, "ClienteId", StringComparison.OrdinalIgnoreCase))
                {
                    clienteIdNotNull = reader.GetInt32(3) == 1;
                }
            }
            reader.Close();

            if (!colunasExistentes.Contains("ICloudEventId"))
            {
                cmd.CommandText = "ALTER TABLE Agendamentos ADD COLUMN ICloudEventId TEXT NULL;";
                cmd.ExecuteNonQuery();
            }
            if (!colunasExistentes.Contains("ICloudETag"))
            {
                cmd.CommandText = "ALTER TABLE Agendamentos ADD COLUMN ICloudETag TEXT NULL;";
                cmd.ExecuteNonQuery();
            }
            if (!colunasExistentes.Contains("ICloudUrl"))
            {
                cmd.CommandText = "ALTER TABLE Agendamentos ADD COLUMN ICloudUrl TEXT NULL;";
                cmd.ExecuteNonQuery();
            }
            if (!colunasExistentes.Contains("VersaoLocal"))
            {
                cmd.CommandText = "ALTER TABLE Agendamentos ADD COLUMN VersaoLocal INTEGER NOT NULL DEFAULT 1;";
                cmd.ExecuteNonQuery();
            }
            if (!colunasExistentes.Contains("UltimaSincronizacaoICloud"))
            {
                cmd.CommandText = "ALTER TABLE Agendamentos ADD COLUMN UltimaSincronizacaoICloud TEXT NULL;";
                cmd.ExecuteNonQuery();
            }
            if (!colunasExistentes.Contains("SincronizadoComICloud"))
            {
                cmd.CommandText = "ALTER TABLE Agendamentos ADD COLUMN SincronizadoComICloud INTEGER NOT NULL DEFAULT 0;";
                cmd.ExecuteNonQuery();
            }
            if (!colunasExistentes.Contains("ClienteNaoEncontradoNome"))
            {
                cmd.CommandText = "ALTER TABLE Agendamentos ADD COLUMN ClienteNaoEncontradoNome TEXT NULL;";
                cmd.ExecuteNonQuery();
            }
            if (!colunasExistentes.Contains("ServicoNaoEncontradoNome"))
            {
                cmd.CommandText = "ALTER TABLE Agendamentos ADD COLUMN ServicoNaoEncontradoNome TEXT NULL;";
                cmd.ExecuteNonQuery();
            }
            if (clienteIdNotNull)
            {
                cmd.CommandText = @"
                    PRAGMA foreign_keys = OFF;
                    CREATE TABLE IF NOT EXISTS Agendamentos_rebuild (
                        Id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
                        ClienteId INTEGER NULL,
                        DataHora TEXT NOT NULL,
                        TempoTotalPrevistoMinutos INTEGER NOT NULL,
                        Status INTEGER NOT NULL,
                        Observacao TEXT NULL,
                        GoogleEventId TEXT NULL,
                        ComandaId INTEGER NULL,
                        ICloudEventId TEXT NULL,
                        ICloudETag TEXT NULL,
                        ICloudUrl TEXT NULL,
                        VersaoLocal INTEGER NOT NULL DEFAULT 1,
                        UltimaSincronizacaoICloud TEXT NULL,
                        SincronizadoComICloud INTEGER NOT NULL DEFAULT 0,
                        ClienteNaoEncontradoNome TEXT NULL,
                        ServicoNaoEncontradoNome TEXT NULL,
                        CONSTRAINT FK_Agendamentos_Clientes_ClienteId FOREIGN KEY (ClienteId) REFERENCES Clientes (Id) ON DELETE RESTRICT
                    );
                    INSERT INTO Agendamentos_rebuild (Id, ClienteId, DataHora, TempoTotalPrevistoMinutos, Status, Observacao, GoogleEventId, ComandaId, ICloudEventId, ICloudETag, ICloudUrl, VersaoLocal, UltimaSincronizacaoICloud, SincronizadoComICloud, ClienteNaoEncontradoNome, ServicoNaoEncontradoNome)
                    SELECT Id, ClienteId, DataHora, TempoTotalPrevistoMinutos, Status, Observacao, GoogleEventId, ComandaId, ICloudEventId, ICloudETag, ICloudUrl, VersaoLocal, UltimaSincronizacaoICloud, SincronizadoComICloud, ClienteNaoEncontradoNome, ServicoNaoEncontradoNome FROM Agendamentos;
                    DROP TABLE Agendamentos;
                    ALTER TABLE Agendamentos_rebuild RENAME TO Agendamentos;
                    CREATE INDEX IF NOT EXISTS IX_Agendamentos_ClienteId ON Agendamentos (ClienteId);
                    PRAGMA foreign_keys = ON;
                ";
                cmd.ExecuteNonQuery();
                cmd.ExecuteNonQuery();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[DbInitializer] Migração Agendamentos iCloud: {ex.Message}");
        }

        // Criação defensiva das tabelas de sincronização com iCloud / iPhone (caso o banco já existisse)
        try
        {
            using var cmd = context.Database.GetDbConnection().CreateCommand();
            context.Database.OpenConnection();
            cmd.CommandText = @"
                CREATE TABLE IF NOT EXISTS OperacoesSincronizacao (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    AgendamentoId INTEGER NULL,
                    ICloudEventId TEXT NULL,
                    TipoOperacao INTEGER NOT NULL,
                    VersaoLocal INTEGER NOT NULL DEFAULT 1,
                    CriadoEm TEXT NOT NULL,
                    AtualizadoEm TEXT NULL,
                    Tentativas INTEGER NOT NULL DEFAULT 0,
                    UltimoErro TEXT NULL,
                    Estado INTEGER NOT NULL DEFAULT 0,
                    PayloadJson TEXT NULL
                );

                CREATE INDEX IF NOT EXISTS IX_OperacoesSincronizacao_Estado ON OperacoesSincronizacao (Estado);
                CREATE INDEX IF NOT EXISTS IX_OperacoesSincronizacao_AgendamentoId ON OperacoesSincronizacao (AgendamentoId);

                CREATE TABLE IF NOT EXISTS ConflitosSincronizacao (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    AgendamentoId INTEGER NULL,
                    ICloudEventId TEXT NULL,
                    Titulo TEXT NOT NULL,
                    Descricao TEXT NULL,
                    DadosLocaisJson TEXT NULL,
                    DadosRemotosJson TEXT NULL,
                    CriadoEm TEXT NOT NULL,
                    Resolvido INTEGER NOT NULL DEFAULT 0,
                    ResolvidoEm TEXT NULL,
                    ResolucaoEscolhida TEXT NULL
                );

                CREATE INDEX IF NOT EXISTS IX_ConflitosSincronizacao_Resolvido ON ConflitosSincronizacao (Resolvido);
            ";
            cmd.ExecuteNonQuery();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[DbInitializer] Criação tabelas iCloud: {ex.Message}");
        }

        // Atualização de registros existentes que ainda não possuam DuracaoFormatada ou tenham a descrição unificada anterior
        try
        {
            var servicosExistentes = context.Servicos.Where(s => s.DeletedAt == null).ToList();
            bool alterouExistentes = false;
            foreach (var s in servicosExistentes)
            {
                if (s.Descricao == "Coloração / Tintura")
                {
                    s.Descricao = "Coloração";
                    s.Especificacao = "Tintura";
                    alterouExistentes = true;
                }

                if (string.IsNullOrWhiteSpace(s.DuracaoFormatada))
                {
                    s.DuracaoFormatada = GerenciadorSalao.Helpers.ValidationHelper.FormatarMinutosParaHhMm(s.DuracaoMinutos);
                    alterouExistentes = true;
                }
            }
            if (alterouExistentes)
            {
                context.SaveChanges();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[DbInitializer] Ajuste inicial Servicos: {ex.Message}");
        }

    }
}
