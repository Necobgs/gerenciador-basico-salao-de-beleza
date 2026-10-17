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
    Decimal
}

public static class MaskHelper
{
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
        textBox.TextChanged -= OnTextChanged;
        DataObject.RemovePastingHandler(textBox, OnPaste);

        if ((MaskType)e.NewValue != MaskType.None)
        {
            textBox.PreviewTextInput += OnPreviewTextInput;
            textBox.TextChanged += OnTextChanged;
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
        else if (mask == MaskType.Decimal)
        {
            // Permite dígitos e separadores decimais (vírgula ou ponto)
            if (e.Text is not ("," or ".") && !Regex.IsMatch(e.Text, @"^\d+$"))
            {
                e.Handled = true;
                return;
            }

            var currentText = textBox.Text ?? "";
            if (e.Text is "," or ".")
            {
                if (currentText.Contains(',') || currentText.Contains('.'))
                {
                    e.Handled = true;
                    return;
                }
            }
            else if (currentText.Contains(','))
            {
                var partes = currentText.Split(',');
                if (partes.Length > 1 && partes[1].Length >= 2 && textBox.SelectionLength == 0 && textBox.CaretIndex > currentText.IndexOf(','))
                {
                    e.Handled = true;
                    return;
                }
            }
        }
    }

    private static void OnPaste(object sender, DataObjectPastingEventArgs e)
    {
        if (sender is not TextBox textBox) return;
        var mask = GetMask(textBox);

        if (mask is MaskType.Phone or MaskType.Cpf or MaskType.NumericOnly)
        {
            if (e.DataObject.GetDataPresent(DataFormats.Text))
            {
                var text = (string)e.DataObject.GetData(DataFormats.Text);
                var digits = ValidationHelper.LimparApenasDigitos(text);
                if (string.IsNullOrEmpty(digits))
                {
                    e.CancelCommand();
                }
            }
            else
            {
                e.CancelCommand();
            }
        }
        else if (mask == MaskType.Decimal)
        {
            if (e.DataObject.GetDataPresent(DataFormats.Text))
            {
                var text = (string)e.DataObject.GetData(DataFormats.Text);
                if (!Regex.IsMatch(text, @"^[\d,\.]+$"))
                {
                    e.CancelCommand();
                }
            }
            else
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

        if (mask == MaskType.Decimal)
        {
            if (textBox.Text.Contains('.'))
            {
                _isFormatting = true;
                try
                {
                    var caret = textBox.CaretIndex;
                    textBox.Text = textBox.Text.Replace('.', ',');
                    textBox.CaretIndex = Math.Min(caret, textBox.Text.Length);
                }
                finally
                {
                    _isFormatting = false;
                }
            }
            return;
        }

        _isFormatting = true;
        try
        {
            var raw = textBox.Text;
            var formatted = mask switch
            {
                MaskType.Phone => ValidationHelper.FormatarTelefone(raw),
                MaskType.Cpf => ValidationHelper.FormatarCpf(raw),
                MaskType.NumericOnly => ValidationHelper.LimparApenasDigitos(raw),
                _ => raw
            };

            if (textBox.Text != formatted)
            {
                textBox.Text = formatted;
                textBox.CaretIndex = formatted.Length;
            }
        }
        finally
        {
            _isFormatting = false;
        }
    }
}
