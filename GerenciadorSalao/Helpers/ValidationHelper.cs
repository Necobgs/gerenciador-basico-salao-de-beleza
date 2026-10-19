using System.Text.RegularExpressions;

namespace GerenciadorSalao.Helpers;

public static class ValidationHelper
{
    private static readonly Regex EmailRegex = new(
        @"^[^@\s]+@[^@\s]+\.[^@\s]+$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public static string LimparApenasDigitos(string? valor)
    {
        if (string.IsNullOrEmpty(valor)) return string.Empty;
        return Regex.Replace(valor, @"\D", "");
    }

    public static bool ValidarNome(string? nome, out string? erro)
    {
        if (string.IsNullOrWhiteSpace(nome))
        {
            erro = "O nome do cliente é obrigatório.";
            return false;
        }

        var trimmed = nome.Trim();
        if (trimmed.Length < 2)
        {
            erro = "O nome deve conter pelo menos 2 caracteres.";
            return false;
        }

        erro = null;
        return true;
    }

    public static bool ValidarTelefone(string? telefone, out string? erro, bool obrigatorio = false)
    {
        if (string.IsNullOrWhiteSpace(telefone))
        {
            if (obrigatorio)
            {
                erro = "O telefone é obrigatório.";
                return false;
            }
            erro = null;
            return true;
        }

        var digitos = LimparApenasDigitos(telefone);
        if (digitos.Length is not 10 and not 11)
        {
            erro = "O telefone deve conter DDD e 8 ou 9 dígitos (ex: (11) 99999-9999).";
            return false;
        }

        var ddd = int.Parse(digitos.Substring(0, 2));
        if (ddd < 11 || ddd > 99)
        {
            erro = "O DDD informado é inválido.";
            return false;
        }

        if (digitos.Length == 11 && digitos[2] != '9')
        {
            erro = "Telefones celulares de 11 dígitos devem iniciar com o dígito 9 após o DDD.";
            return false;
        }

        erro = null;
        return true;
    }

    public static bool ValidarEmail(string? email, out string? erro, bool obrigatorio = false)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            if (obrigatorio)
            {
                erro = "O e-mail é obrigatório.";
                return false;
            }
            erro = null;
            return true;
        }

        var trimmed = email.Trim();
        if (!EmailRegex.IsMatch(trimmed) || trimmed.Length > 150)
        {
            erro = "O e-mail informado possui um formato inválido (ex: cliente@exemplo.com).";
            return false;
        }

        erro = null;
        return true;
    }

    public static bool ValidarCpf(string? cpf, out string? erro, bool obrigatorio = false)
    {
        if (string.IsNullOrWhiteSpace(cpf))
        {
            if (obrigatorio)
            {
                erro = "O CPF é obrigatório.";
                return false;
            }
            erro = null;
            return true;
        }

        var digitos = LimparApenasDigitos(cpf);
        if (digitos.Length != 11)
        {
            erro = "O CPF deve conter exatamente 11 dígitos.";
            return false;
        }

        if (new string(digitos[0], 11) == digitos)
        {
            erro = "O CPF informado é inválido.";
            return false;
        }

        var pesos1 = new[] { 10, 9, 8, 7, 6, 5, 4, 3, 2 };
        var soma1 = 0;
        for (var i = 0; i < 9; i++) soma1 += (digitos[i] - '0') * pesos1[i];
        var resto1 = soma1 % 11;
        var digito1 = resto1 < 2 ? 0 : 11 - resto1;

        if (digitos[9] - '0' != digito1)
        {
            erro = "O CPF informado é inválido.";
            return false;
        }

        var pesos2 = new[] { 11, 10, 9, 8, 7, 6, 5, 4, 3, 2 };
        var soma2 = 0;
        for (var i = 0; i < 10; i++) soma2 += (digitos[i] - '0') * pesos2[i];
        var resto2 = soma2 % 11;
        var digito2 = resto2 < 2 ? 0 : 11 - resto2;

        if (digitos[10] - '0' != digito2)
        {
            erro = "O CPF informado é inválido.";
            return false;
        }

        erro = null;
        return true;
    }

    public static string FormatarTelefone(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return string.Empty;
        var digitos = LimparApenasDigitos(raw);

        if (digitos.Length == 0) return string.Empty;
        if (digitos.Length <= 2) return $"({digitos}";
        if (digitos.Length <= 6) return $"({digitos[..2]}) {digitos[2..]}";
        if (digitos.Length <= 10) return $"({digitos[..2]}) {digitos.Substring(2, digitos.Length - 6)}-{digitos[^4..]}";

        var limited = digitos.Length > 11 ? digitos[..11] : digitos;
        return $"({limited[..2]}) {limited.Substring(2, 5)}-{limited[^4..]}";
    }

    public static string FormatarCpf(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return string.Empty;
        var digitos = LimparApenasDigitos(raw);

        if (digitos.Length == 0) return string.Empty;
        if (digitos.Length <= 3) return digitos;
        if (digitos.Length <= 6) return $"{digitos[..3]}.{digitos[3..]}";
        if (digitos.Length <= 9) return $"{digitos[..3]}.{digitos.Substring(3, 3)}.{digitos[6..]}";

        var limited = digitos.Length > 11 ? digitos[..11] : digitos;
        return $"{limited[..3]}.{limited.Substring(3, 3)}.{limited.Substring(6, 3)}-{limited[9..]}";
    }

    public static bool ValidarDescricaoProduto(string? descricao, out string? erro)
    {
        if (string.IsNullOrWhiteSpace(descricao))
        {
            erro = "O nome / descrição do produto é obrigatório.";
            return false;
        }

        var trimmed = descricao.Trim();
        if (trimmed.Length < 2)
        {
            erro = "A descrição deve conter pelo menos 2 caracteres.";
            return false;
        }

        erro = null;
        return true;
    }

    public static bool ValidarValorProduto(string? valorTexto, out decimal valorDecimal, out string? erro)
    {
        valorDecimal = 0;
        if (string.IsNullOrWhiteSpace(valorTexto))
        {
            erro = "O valor do produto é obrigatório.";
            return false;
        }

        var limpo = valorTexto.Replace("R$", "").Trim();
        if (limpo.Contains(',') && limpo.Contains('.'))
        {
            limpo = limpo.Replace(".", ""); // remove separador de milhar
        }

        if (!decimal.TryParse(limpo, System.Globalization.NumberStyles.Currency, new System.Globalization.CultureInfo("pt-BR"), out valorDecimal) &&
            !decimal.TryParse(limpo, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out valorDecimal))
        {
            erro = "Informe um valor monetário válido (ex: 89,00).";
            return false;
        }

        if (valorDecimal < 0)
        {
            erro = "O valor do produto não pode ser negativo.";
            return false;
        }

        erro = null;
        return true;
    }
}
