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

    public static string FormatarMoeda(string? raw, bool incluirSimbolo = false)
    {
        if (string.IsNullOrWhiteSpace(raw)) return string.Empty;

        var digitos = LimparApenasDigitos(raw);
        if (digitos.Length == 0) return string.Empty;

        // Trata sequência de zeros à esquerda
        digitos = digitos.TrimStart('0');
        if (digitos.Length == 0) return incluirSimbolo ? "R$ 0,00" : "0,00";
        if (digitos.Length == 1) return incluirSimbolo ? $"R$ 0,0{digitos}" : $"0,0{digitos}";
        if (digitos.Length == 2) return incluirSimbolo ? $"R$ 0,{digitos}" : $"0,{digitos}";

        var centavos = digitos[^2..];
        var inteirosStr = digitos[..^2];

        if (long.TryParse(inteirosStr, out var inteirosVal))
        {
            var inteirosFormatado = inteirosVal.ToString("N0", new System.Globalization.CultureInfo("pt-BR"));
            var resultado = $"{inteirosFormatado},{centavos}";
            return incluirSimbolo ? $"R$ {resultado}" : resultado;
        }

        var res = $"{inteirosStr},{centavos}";
        return incluirSimbolo ? $"R$ {res}" : res;
    }

    public static string FormatarDuracao(int minutos, bool abreviado = false)
    {
        if (minutos <= 0) return abreviado ? "0 min" : "0 minutos";

        if (minutos < 60)
        {
            return abreviado ? $"{minutos} min" : (minutos == 1 ? "1 minuto" : $"{minutos} minutos");
        }

        var horas = minutos / 60;
        var restoMinutos = minutos % 60;

        if (abreviado)
        {
            return restoMinutos == 0 ? $"{horas}h" : $"{horas}h {restoMinutos:D2}m";
        }

        if (restoMinutos == 0)
        {
            return horas == 1 ? "1:00 hora" : $"{horas}:00 horas";
        }

        return $"{horas}:{restoMinutos:D2} horas";
    }

    public static string FormatarMinutosParaHhMm(int minutos)
    {
        if (minutos <= 0) return "0:00";
        var horas = minutos / 60;
        var restoMinutos = minutos % 60;
        return $"{horas}:{restoMinutos:D2}";
    }

    public static string FormatarDuracaoParaEdicao(int minutos)
    {
        if (minutos <= 0) return "1:00";
        return FormatarMinutosParaHhMm(minutos);
    }

    public static bool ValidarValorMonetario(string? valorTexto, out decimal valorDecimal, out string? erro, string campoNome = "item")
    {
        valorDecimal = 0;
        if (string.IsNullOrWhiteSpace(valorTexto))
        {
            erro = $"O valor do {campoNome} é obrigatório.";
            return false;
        }

        var limpo = valorTexto.Replace("R$", "").Trim();

        // Se contém apenas ponto e NÃO contém vírgula (ex: "120.50" ou "80.00" digitado no teclado numérico):
        if (limpo.Contains('.') && !limpo.Contains(','))
        {
            var partes = limpo.Split('.');
            // Se houver 1 ponto e até 2 dígitos depois dele, o usuário utilizou ponto como separador decimal
            if (partes.Length == 2 && partes[1].Length <= 2)
            {
                limpo = $"{partes[0]},{partes[1]}";
            }
            else
            {
                // Múltiplos pontos ou mais de 2 dígitos: trata como separador de milhar (ex: 1.000 ou 1.000.000)
                limpo = limpo.Replace(".", "");
            }
        }
        else if (limpo.Contains(',') && limpo.Contains('.'))
        {
            // Ponto como milhar e vírgula como decimal (ex: 1.250,50)
            limpo = limpo.Replace(".", "");
        }

        if (!decimal.TryParse(limpo, System.Globalization.NumberStyles.Currency, new System.Globalization.CultureInfo("pt-BR"), out valorDecimal) &&
            !decimal.TryParse(limpo, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out valorDecimal))
        {
            erro = "Informe um valor monetário válido (ex: 80,00 ou 120,50).";
            return false;
        }

        if (valorDecimal < 0)
        {
            erro = $"O valor do {campoNome} não pode ser negativo.";
            return false;
        }

        erro = null;
        return true;
    }


    public static bool ValidarValorProduto(string? valorTexto, out decimal valorDecimal, out string? erro)
    {
        return ValidarValorMonetario(valorTexto, out valorDecimal, out erro, "produto");
    }

    public static bool ValidarDescricaoServico(string? descricao, out string? erro)
    {
        if (string.IsNullOrWhiteSpace(descricao))
        {
            erro = "O nome / descrição do serviço é obrigatório.";
            return false;
        }

        var trimmed = descricao.Trim();
        if (trimmed.Length < 2)
        {
            erro = "A descrição do serviço deve conter pelo menos 2 caracteres.";
            return false;
        }

        if (trimmed.Length > 100)
        {
            erro = "A descrição do serviço não pode exceder 100 caracteres.";
            return false;
        }

        erro = null;
        return true;
    }

    public static bool ValidarValorServico(string? valorTexto, out decimal valorDecimal, out string? erro)
    {
        return ValidarValorMonetario(valorTexto, out valorDecimal, out erro, "serviço");
    }

    public static bool ValidarDuracaoServico(string? duracaoTexto, out int duracaoMinutos, out string? erro)
    {
        duracaoMinutos = 0;
        if (string.IsNullOrWhiteSpace(duracaoTexto))
        {
            erro = "O tempo estimado é obrigatório (ex: 2:30 ou 90 min).";
            return false;
        }

        var limpo = duracaoTexto.Trim().ToLower();

        // 1. Formato com dois pontos (ex: "2:30", "02:30", "1:00", "0:45")
        if (limpo.Contains(':'))
        {
            var partes = limpo.Split(':');
            if (partes.Length == 2 &&
                int.TryParse(LimparApenasDigitos(partes[0]), out var horas) &&
                int.TryParse(LimparApenasDigitos(partes[1]), out var minutos))
            {
                if (minutos >= 60)
                {
                    erro = "Os minutos não podem ser 60 ou mais em formato hh:mm (ex: use 1:30 ao invés de 1:90).";
                    return false;
                }

                duracaoMinutos = (horas * 60) + minutos;
                if (duracaoMinutos <= 0)
                {
                    erro = "A duração do serviço deve ser maior que zero.";
                    return false;
                }

                if (duracaoMinutos > 1440)
                {
                    erro = "A duração não pode exceder 24 horas (1440 min).";
                    return false;
                }

                erro = null;
                return true;
            }

            erro = "Formato de hora inválido. Use horas:minutos (ex: 2:30) ou minutos (ex: 90).";
            return false;
        }

        // 2. Formato com letra 'h' (ex: "2h30", "2h", "1h 30m")
        if (limpo.Contains('h'))
        {
            var partes = limpo.Split('h');
            var horasStr = LimparApenasDigitos(partes[0]);
            var minsStr = partes.Length > 1 ? LimparApenasDigitos(partes[1]) : "";

            var horas = string.IsNullOrEmpty(horasStr) ? 0 : int.Parse(horasStr);
            var minutos = string.IsNullOrEmpty(minsStr) ? 0 : int.Parse(minsStr);

            if (minutos >= 60)
            {
                erro = "Os minutos não podem ser 60 ou mais.";
                return false;
            }

            duracaoMinutos = (horas * 60) + minutos;
            if (duracaoMinutos <= 0)
            {
                erro = "A duração do serviço deve ser maior que zero.";
                return false;
            }

            if (duracaoMinutos > 1440)
            {
                erro = "A duração não pode exceder 24 horas.";
                return false;
            }

            erro = null;
            return true;
        }

        // 3. Formato numérico direto (minutos totais: ex: "30", "45", "90", "120", "180")
        var digitos = LimparApenasDigitos(limpo);
        if (!int.TryParse(digitos, out var minutosTotais) || minutosTotais <= 0)
        {
            erro = "Informe um tempo de duração válido (ex: 2:30 para horas e minutos ou 90 para minutos).";
            return false;
        }

        if (minutosTotais > 1440)
        {
            erro = "A duração não pode exceder 1440 minutos (24 horas).";
            return false;
        }

        duracaoMinutos = minutosTotais;
        erro = null;
        return true;
    }
}

