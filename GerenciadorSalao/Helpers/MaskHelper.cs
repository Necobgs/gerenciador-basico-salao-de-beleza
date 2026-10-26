using System.Globalization;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace GerenciadorSalao.Helpers;

public enum MaskType
{
    None,
    Phone,
    Cpf,
    NumericOnly,
    Decimal,
    Currency,
    Time,
    Duration
}

public static class MaskHelper
{
    private static readonly CultureInfo PtBrCulture = new("pt-BR");

    public static readonly DependencyProperty MaskProperty =
        DependencyProperty.RegisterAttached(
            "Mask",
            typeof(MaskType),
            typeof(MaskHelper),
            new PropertyMetadata(MaskType.None, OnMaskChanged));

    public static MaskType GetMask(DependencyObject obj) => (MaskType)obj.GetValue(MaskProperty);
    public static void SetMask(DependencyObject obj, MaskType value) => obj.SetValue(MaskProperty, value);

    private static void OnMaskChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not TextBox textBox) return;

        textBox.PreviewTextInput -= OnPreviewTextInput;
        textBox.PreviewKeyDown -= OnPreviewKeyDown;
        textBox.TextChanged -= OnTextChanged;
        textBox.LostFocus -= OnLostFocus;
        DataObject.RemovePastingHandler(textBox, OnPaste);

        if ((MaskType)e.NewValue != MaskType.None)
        {
            textBox.PreviewTextInput += OnPreviewTextInput;
            textBox.PreviewKeyDown += OnPreviewKeyDown;
            textBox.TextChanged += OnTextChanged;
            textBox.LostFocus += OnLostFocus;
            DataObject.AddPastingHandler(textBox, OnPaste);
        }
    }

    private static void OnPreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        if (sender is not TextBox textBox) return;
        var mask = GetMask(textBox);

        if (mask is MaskType.Phone or MaskType.Cpf or MaskType.NumericOnly)
        {
            // Bloqueia qualquer caractere que não seja dígito 0-9
            if (!Regex.IsMatch(e.Text, @"^\d+$"))
            {
                e.Handled = true;
                return;
            }

            var currentDigits = ValidationHelper.LimparApenasDigitos(textBox.Text);
            if (mask == MaskType.Phone && currentDigits.Length >= 11 && textBox.SelectionLength == 0)
            {
                e.Handled = true;
            }
            else if (mask == MaskType.Cpf && currentDigits.Length >= 11 && textBox.SelectionLength == 0)
            {
                e.Handled = true;
            }
        }
        else if (mask is MaskType.Currency or MaskType.Decimal)
        {
            // Se o usuário digitou separador decimal (vírgula ou ponto no teclado físico ou numérico)
            if (e.Text is "," or ".")
            {
                e.Handled = true;
                var rawDigits = ValidationHelper.LimparApenasDigitos(textBox.Text);

                // Se já houver dígitos digitados (ex: "80" que formatou como "0,80"):
                // Ao pressionar ponto ou vírgula, o usuário confirma que os dígitos são reais inteiros
                if (!string.IsNullOrEmpty(rawDigits) && rawDigits != "00" && rawDigits != "0")
                {
                    _isFormatting = true;
                    try
                    {
                        var formatado = ValidationHelper.FormatarMoeda(rawDigits + "00");
                        textBox.Text = formatado;
                        textBox.CaretIndex = textBox.Text.Length;
                    }
                    finally
                    {
                        _isFormatting = false;
                    }
                }
                return;
            }

            // Permite apenas dígitos 0-9
            if (!Regex.IsMatch(e.Text, @"^\d+$"))
            {
                e.Handled = true;
                return;
            }

            var digitosAtuais = ValidationHelper.LimparApenasDigitos(textBox.Text);
            if (digitosAtuais.Length >= 10 && textBox.SelectionLength == 0)
            {
                e.Handled = true;
            }
        }
        else if (mask == MaskType.Time)
        {
            // Permite apenas dígitos 0-9 e dois pontos ':'
            if (!Regex.IsMatch(e.Text, @"^[\d:]+$"))
            {
                e.Handled = true;
                return;
            }

            if (e.Text == ":")
            {
                // Se o campo estiver vazio ou já contiver ':', bloqueia
                if (string.IsNullOrWhiteSpace(textBox.Text) || (textBox.Text.Contains(':') && textBox.SelectionLength == 0))
                {
                    e.Handled = true;
                    return;
                }
            }

            var currentDigits = ValidationHelper.LimparApenasDigitos(textBox.Text);
            // Limita a 4 dígitos para HHmm (caso não haja seleção de texto sendo substituída)
            if (Regex.IsMatch(e.Text, @"^\d+$") && currentDigits.Length >= 4 && textBox.SelectionLength == 0)
            {
                e.Handled = true;
            }
        }
        else if (mask == MaskType.Duration)
        {
            // Permite dígitos 0-9, dois pontos ':' e letra 'h'/'H'
            if (!Regex.IsMatch(e.Text, @"^[\d:hH]+$"))
            {
                e.Handled = true;
                return;
            }

            if ((textBox.Text ?? "").Length >= 8 && textBox.SelectionLength == 0)
            {
                e.Handled = true;
            }
        }
    }

    private static void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (sender is not TextBox textBox) return;
        var mask = GetMask(textBox);

        if (mask == MaskType.Time)
        {
            if (e.Key == Key.Back && textBox.SelectionLength == 0 && textBox.CaretIndex > 0)
            {
                // Se o cursor estiver imediatamente após os dois pontos (ex: "14:|" ou "09:|")
                if (textBox.CaretIndex == 3 && textBox.Text.Length >= 3 && textBox.Text[2] == ':')
                {
                    e.Handled = true;
                    _isFormatting = true;
                    try
                    {
                        var digits = ValidationHelper.LimparApenasDigitos(textBox.Text);
                        var novoTexto = digits.Length > 1 ? digits[..1] : string.Empty;
                        if (novoTexto == "0") novoTexto = string.Empty;
                        textBox.Text = novoTexto;
                        textBox.CaretIndex = novoTexto.Length;
                    }
                    finally
                    {
                        _isFormatting = false;
                    }
                }
            }
            else if (e.Key == Key.Delete && textBox.SelectionLength == 0)
            {
                // Se o cursor estiver antes dos dois pontos (ex: "14|:30")
                if (textBox.CaretIndex == 2 && textBox.Text.Length > 2 && textBox.Text[2] == ':')
                {
                    e.Handled = true;
                    _isFormatting = true;
                    try
                    {
                        var digits = ValidationHelper.LimparApenasDigitos(textBox.Text);
                        var hora = digits.Length >= 2 ? digits[..2] : digits;
                        var minutoRestante = digits.Length >= 4 ? digits[3..] : string.Empty;
                        var novoTexto = string.IsNullOrEmpty(minutoRestante) ? $"{hora}:" : $"{hora}:{minutoRestante}";
                        textBox.Text = novoTexto;
                        textBox.CaretIndex = 3;
                    }
                    finally
                    {
                        _isFormatting = false;
                    }
                }
            }
        }
    }

    private static void OnPaste(object sender, DataObjectPastingEventArgs e)
    {
        if (sender is not TextBox textBox) return;
        var mask = GetMask(textBox);

        if (!e.DataObject.GetDataPresent(DataFormats.Text))
        {
            e.CancelCommand();
            return;
        }

        var text = (string)e.DataObject.GetData(DataFormats.Text);

        if (mask is MaskType.Phone or MaskType.Cpf or MaskType.NumericOnly)
        {
            var digits = ValidationHelper.LimparApenasDigitos(text);
            if (string.IsNullOrEmpty(digits))
            {
                e.CancelCommand();
            }
        }
        else if (mask is MaskType.Currency or MaskType.Decimal)
        {
            if (ValidationHelper.ValidarValorMonetario(text, out var decimalVal, out _, "valor"))
            {
                e.CancelCommand();
                _isFormatting = true;
                try
                {
                    textBox.Text = decimalVal.ToString("N2", PtBrCulture);
                    textBox.CaretIndex = textBox.Text.Length;
                }
                finally
                {
                    _isFormatting = false;
                }
            }
            else
            {
                e.CancelCommand();
            }
        }
        else if (mask == MaskType.Time)
        {
            var digits = ValidationHelper.LimparApenasDigitos(text);
            if (string.IsNullOrEmpty(digits))
            {
                e.CancelCommand();
                return;
            }

            var formatado = ValidationHelper.FormatarHorario(text);
            if (!string.IsNullOrEmpty(formatado))
            {
                e.CancelCommand();
                _isFormatting = true;
                try
                {
                    textBox.Text = formatado;
                    textBox.CaretIndex = formatado.Length;
                }
                finally
                {
                    _isFormatting = false;
                }
            }
            else
            {
                e.CancelCommand();
            }
        }
        else if (mask == MaskType.Duration)
        {
            if (!Regex.IsMatch(text, @"^[\d:hH\s]+$"))
            {
                e.CancelCommand();
            }
        }
    }

    private static bool _isFormatting;

    private static void OnTextChanged(object sender, TextChangedEventArgs e)
    {
        if (_isFormatting) return;
        if (sender is not TextBox textBox) return;
        var mask = GetMask(textBox);

        if (mask == MaskType.None) return;

        _isFormatting = true;
        try
        {
            var raw = textBox.Text ?? string.Empty;

            if (mask is MaskType.Currency or MaskType.Decimal)
            {
                var formatted = ValidationHelper.FormatarMoeda(raw);
                if (textBox.Text != formatted)
                {
                    textBox.Text = formatted;
                    textBox.CaretIndex = formatted.Length;
                }
                return;
            }

            if (mask == MaskType.Duration)
            {
                var lower = raw.ToLower();
                if (raw != lower)
                {
                    var caret = textBox.CaretIndex;
                    textBox.Text = lower;
                    textBox.CaretIndex = Math.Min(caret, lower.Length);
                }
                return;
            }

            var formatadoGeral = mask switch
            {
                MaskType.Phone => ValidationHelper.FormatarTelefone(raw),
                MaskType.Cpf => ValidationHelper.FormatarCpf(raw),
                MaskType.Time => ValidationHelper.FormatarHorario(raw),
                MaskType.NumericOnly => ValidationHelper.LimparApenasDigitos(raw),
                _ => raw
            };

            if (textBox.Text != formatadoGeral)
            {
                textBox.Text = formatadoGeral;
                textBox.CaretIndex = formatadoGeral.Length;
            }
        }
        finally
        {
            _isFormatting = false;
        }
    }

    private static void OnLostFocus(object sender, RoutedEventArgs e)
    {
        if (sender is not TextBox textBox) return;
        var mask = GetMask(textBox);

        if (mask is MaskType.Currency or MaskType.Decimal)
        {
            if (string.IsNullOrWhiteSpace(textBox.Text)) return;

            if (ValidationHelper.ValidarValorMonetario(textBox.Text, out var val, out _))
            {
                var formatado = val.ToString("N2", PtBrCulture);
                if (textBox.Text != formatado)
                {
                    _isFormatting = true;
                    try
                    {
                        textBox.Text = formatado;
                    }
                    finally
                    {
                        _isFormatting = false;
                    }
                }
            }
        }
        else if (mask == MaskType.Time)
        {
            if (string.IsNullOrWhiteSpace(textBox.Text)) return;

            var normalizado = ValidationHelper.NormalizarHorario(textBox.Text);
            if (textBox.Text != normalizado)
            {
                _isFormatting = true;
                try
                {
                    textBox.Text = normalizado;
                }
                finally
                {
                    _isFormatting = false;
                }
            }
        }
        else if (mask == MaskType.Duration)
        {
            if (string.IsNullOrWhiteSpace(textBox.Text)) return;

            if (ValidationHelper.ValidarDuracaoServico(textBox.Text, out var mins, out _))
            {
                var formatado = ValidationHelper.FormatarMinutosParaHhMm(mins);
                if (textBox.Text != formatado)
                {
                    _isFormatting = true;
                    try
                    {
                        textBox.Text = formatado;
                    }
                    finally
                    {
                        _isFormatting = false;
                    }
                }
            }
        }
    }
}
