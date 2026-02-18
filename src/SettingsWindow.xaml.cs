using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;

namespace gPad;

public partial class SettingsWindow : Window
{
    private MainWindow? _mainWindow;
    private bool _updating;

    private static readonly double[] FontSizes = { 8, 9, 10, 11, 12, 14, 16, 18, 20, 22, 24, 26, 28, 36, 48, 72 };

    public SettingsWindow()
    {
        InitializeComponent();
        Loaded += SettingsWindow_Loaded;
    }

    public void SetOwner(MainWindow mainWindow)
    {
        Owner = mainWindow;
        _mainWindow = mainWindow;
    }

    private void SettingsWindow_Loaded(object sender, RoutedEventArgs e)
    {
        _updating = true;
        // Populate system fonts (sorted)
        var families = Fonts.SystemFontFamilies
            .Select(f => f.Source)
            .Where(s => !string.IsNullOrEmpty(s))
            .OrderBy(s => s, StringComparer.OrdinalIgnoreCase)
            .ToList();
        FontFamilyCombo.ItemsSource = families;
        FontSizeCombo.ItemsSource = FontSizes.Cast<object>().ToList();

        var s = SettingsPersistence.Load();
        OpacitySlider.Value = Math.Clamp(s.Opacity, 0.3, 1.0) * 100.0;
        FontFamilyCombo.Text = s.FontFamily;
        FontSizeCombo.Text = s.FontSize.ToString("0");
        CornerTL.Text = GetCornerValue(s.CornerRadiusTopLeft, s.CornerRadius).ToString("0");
        CornerTR.Text = GetCornerValue(s.CornerRadiusTopRight, s.CornerRadius).ToString("0");
        CornerBR.Text = GetCornerValue(s.CornerRadiusBottomRight, s.CornerRadius).ToString("0");
        CornerBL.Text = GetCornerValue(s.CornerRadiusBottomLeft, s.CornerRadius).ToString("0");
        UpdateCornerPreview();
        _updating = false;

        if (_mainWindow != null)
        {
            _mainWindow.Opacity = OpacitySlider.Value / 100.0;
            _mainWindow.ApplyEditorFont(s.FontFamily, s.FontSize);
            _mainWindow.ApplyCornerRadius(GetCurrentCornerRadii());
        }
    }

    private static double GetCornerValue(double value, double fallback) => value > 0 ? value : fallback;

    private void OpacitySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_updating || _mainWindow == null) return;
        var v = e.NewValue / 100.0;
        _mainWindow.Opacity = v;
        SaveCurrent();
    }

    private void CornerRadiusBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        if (_updating || _mainWindow == null) return;
        UpdateCornerPreview();
        _mainWindow.ApplyCornerRadius(GetCurrentCornerRadii());
        SaveCurrent();
    }

    private System.Windows.CornerRadius GetCurrentCornerRadii()
    {
        return new System.Windows.CornerRadius(
            ParseCorner(CornerTL.Text),
            ParseCorner(CornerTR.Text),
            ParseCorner(CornerBR.Text),
            ParseCorner(CornerBL.Text));
    }

    private static double ParseCorner(string? text) => double.TryParse(text, out var v) && v >= 0 ? Math.Clamp(v, 0, 24) : 0;

    private void UpdateCornerPreview()
    {
        var r = GetCurrentCornerRadii();
        CornerPreviewRect.CornerRadius = new System.Windows.CornerRadius(r.TopLeft, r.TopRight, r.BottomRight, r.BottomLeft);
    }

    private void FontFamilyCombo_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (_updating || _mainWindow == null || FontFamilyCombo.SelectedItem == null) return;
        ApplyFont();
    }

    private void FontSizeCombo_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (_updating || _mainWindow == null || FontSizeCombo.SelectedItem == null) return;
        ApplyFont();
    }

    protected override void OnClosed(EventArgs e)
    {
        SaveCurrent();
        base.OnClosed(e);
    }

    private void ApplyFontFromText()
    {
        if (_mainWindow == null) return;
        var family = string.IsNullOrWhiteSpace(FontFamilyCombo.Text) ? "Consolas" : FontFamilyCombo.Text.Trim();
        var size = double.TryParse(FontSizeCombo.Text, out var sz) && sz > 0 ? sz : 14.0;
        _mainWindow.ApplyEditorFont(family, size);
        SaveCurrent();
    }

    private void FontCombo_LostFocus(object sender, RoutedEventArgs e)
    {
        if (_updating || _mainWindow == null) return;
        ApplyFontFromText();
    }

    private void ApplyFont()
    {
        if (_mainWindow == null) return;
        var family = FontFamilyCombo.SelectedItem as string ?? FontFamilyCombo.Text?.Trim() ?? "Consolas";
        var size = FontSizeCombo.SelectedItem is double d ? d : (double.TryParse(FontSizeCombo.Text?.ToString(), out var sz) && sz > 0 ? sz : 14.0);
        _mainWindow.ApplyEditorFont(family, size);
        SaveCurrent();
    }

    private void SaveCurrent()
    {
        var family = FontFamilyCombo.Text?.Trim() ?? "Consolas";
        var size = double.TryParse(FontSizeCombo.Text, out var sz) ? sz : 14;
        SettingsPersistence.Save(new AppSettings
        {
            Opacity = OpacitySlider.Value / 100.0,
            FontFamily = family,
            FontSize = size,
            CornerRadiusTopLeft = ParseCorner(CornerTL.Text),
            CornerRadiusTopRight = ParseCorner(CornerTR.Text),
            CornerRadiusBottomRight = ParseCorner(CornerBR.Text),
            CornerRadiusBottomLeft = ParseCorner(CornerBL.Text)
        });
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
