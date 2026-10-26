using GerenciadorSalao.Helpers;

namespace GerenciadorSalao.Services.Tests;

public static class ValidationHelperTests
{
    public static void ExecutarTestes(Action<string>? log = null)
    {
        void Log(string msg) => log?.Invoke(msg);

        Log("=== INICIANDO TESTES DE MÁSCARA E VALIDAÇÃO DE HORÁRIO ===");

        // 1. FormatarHorario
        AssertEqual(ValidationHelper.FormatarHorario(null), string.Empty, "Null");
        AssertEqual(ValidationHelper.FormatarHorario(""), string.Empty, "Vazio");
        AssertEqual(ValidationHelper.FormatarHorario("1"), "1", "1 dígito (1)");
        AssertEqual(ValidationHelper.FormatarHorario("2"), "2", "1 dígito (2)");
        AssertEqual(ValidationHelper.FormatarHorario("0"), "0", "1 dígito (0)");
        AssertEqual(ValidationHelper.FormatarHorario("3"), "03:", "1 dígito (3)");
        AssertEqual(ValidationHelper.FormatarHorario("8"), "08:", "1 dígito (8)");
        AssertEqual(ValidationHelper.FormatarHorario("9"), "09:", "1 dígito (9)");

        AssertEqual(ValidationHelper.FormatarHorario("09"), "09:", "2 dígitos (09)");
        AssertEqual(ValidationHelper.FormatarHorario("14"), "14:", "2 dígitos (14)");
        AssertEqual(ValidationHelper.FormatarHorario("20"), "20:", "2 dígitos (20)");
        AssertEqual(ValidationHelper.FormatarHorario("23"), "23:", "2 dígitos (23)");
        AssertEqual(ValidationHelper.FormatarHorario("25"), "23:", "2 dígitos com hora > 23 (25 -> 23:)");

        AssertEqual(ValidationHelper.FormatarHorario("143"), "14:3", "3 dígitos (143)");
        AssertEqual(ValidationHelper.FormatarHorario("14:3"), "14:3", "3 dígitos com ':' (14:3)");
        AssertEqual(ValidationHelper.FormatarHorario("146"), "14:06", "3 dígitos com minuto > 5 (146 -> 14:06)");
        AssertEqual(ValidationHelper.FormatarHorario("930"), "09:30", "3 dígitos hora única (930 -> 09:30)");
        AssertEqual(ValidationHelper.FormatarHorario("815"), "08:15", "3 dígitos hora única (815 -> 08:15)");

        AssertEqual(ValidationHelper.FormatarHorario("1430"), "14:30", "4 dígitos (1430)");
        AssertEqual(ValidationHelper.FormatarHorario("14:30"), "14:30", "4 dígitos com ':' (14:30)");
        AssertEqual(ValidationHelper.FormatarHorario("0900"), "09:00", "4 dígitos (0900)");
        AssertEqual(ValidationHelper.FormatarHorario("9:30"), "09:30", "Hora única com ':' (9:30 -> 09:30)");
        AssertEqual(ValidationHelper.FormatarHorario("8:00"), "08:00", "Hora única com ':' (8:00 -> 08:00)");

        // 2. NormalizarHorario
        AssertEqual(ValidationHelper.NormalizarHorario("9"), "09:00", "Normalizar 9");
        AssertEqual(ValidationHelper.NormalizarHorario("09:"), "09:00", "Normalizar 09:");
        AssertEqual(ValidationHelper.NormalizarHorario("14"), "14:00", "Normalizar 14");
        AssertEqual(ValidationHelper.NormalizarHorario("14:"), "14:00", "Normalizar 14:");
        AssertEqual(ValidationHelper.NormalizarHorario("14:3"), "14:30", "Normalizar 14:3");
        AssertEqual(ValidationHelper.NormalizarHorario("14:30"), "14:30", "Normalizar 14:30");
        AssertEqual(ValidationHelper.NormalizarHorario("0"), "00:00", "Normalizar 0");

        // 3. ValidarHorario
        bool ok1 = ValidationHelper.ValidarHorario("14:30", out var ts1, out _);
        if (!ok1 || ts1 != new TimeSpan(14, 30, 0))
            throw new Exception("Falha em ValidarHorario 14:30");

        bool ok2 = ValidationHelper.ValidarHorario("9", out var ts2, out _);
        if (!ok2 || ts2 != new TimeSpan(9, 0, 0))
            throw new Exception("Falha em ValidarHorario 9");

        bool ok3 = ValidationHelper.ValidarHorario("14:3", out var ts3, out _);
        if (!ok3 || ts3 != new TimeSpan(14, 30, 0))
            throw new Exception("Falha em ValidarHorario 14:3");

        bool ok4 = ValidationHelper.ValidarHorario("abc", out _, out var erro4);
        if (ok4 || string.IsNullOrEmpty(erro4))
            throw new Exception("Falha em ValidarHorario abc (deveria falhar)");

        Log("TODOS OS TESTES DE MÁSCARA E VALIDAÇÃO DE HORÁRIO PASSARAM COM SUCESSO!");
    }

    private static void AssertEqual(string actual, string expected, string caso)
    {
        if (actual != expected)
        {
            throw new Exception($"FALHA no teste [{caso}]: Esperado='{expected}', Obtido='{actual}'");
        }
    }
}
