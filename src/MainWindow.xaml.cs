using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using System.Windows.Shell;

namespace gPad;

public partial class MainWindow : Window
{
    private const string TabIndexFormat = "gPad.TabIndex";
    private readonly List<NoteTab> _tabs = new();
    private int _currentIndex = -1;
    private bool _ignoreTextChange;
    private DispatcherTimer? _toastHideTimer;
    private System.Windows.Point _dragStartPos;
    private int _dragSourceIndex = -1;
    private System.Windows.CornerRadius _cornerRadii;

    public MainWindow()
    {
        InitializeComponent();
        var iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "icon.png");
        if (File.Exists(iconPath))
        {
            try { Icon = System.Windows.Media.Imaging.BitmapFrame.Create(new Uri(iconPath, UriKind.Absolute)); } catch { /* ignore */ }
        }
        PreviewKeyDown += MainWindow_PreviewKeyDown;
        WindowStatePersistence.Restore(this);
        var settings = SettingsPersistence.Load();
        Opacity = Math.Clamp(settings.Opacity, 0.3, 1.0);
        ApplyEditorFont(settings.FontFamily, settings.FontSize);
        ApplyCornerRadius(GetCornerRadiiFromSettings(settings));
        RootBorder.Loaded += (_, _) => UpdateRootBorderClip();
        RootBorder.SizeChanged += (_, _) => UpdateRootBorderClip();
        LoadAllTabs();
        if (_tabs.Count > 0)
            SelectTab(0);
    }

    public void ApplyEditorFont(string fontFamily, double fontSize)
    {
        try { Editor.FontFamily = new System.Windows.Media.FontFamily(fontFamily); } catch { Editor.FontFamily = new System.Windows.Media.FontFamily("Consolas"); }
        Editor.FontSize = fontSize > 0 ? fontSize : 14;
    }

    private static System.Windows.CornerRadius GetCornerRadiiFromSettings(AppSettings s)
    {
        var fallback = Math.Clamp(s.CornerRadius, 0, 24);
        return new System.Windows.CornerRadius(
            ClampCorner(s.CornerRadiusTopLeft, fallback),
            ClampCorner(s.CornerRadiusTopRight, fallback),
            ClampCorner(s.CornerRadiusBottomRight, fallback),
            ClampCorner(s.CornerRadiusBottomLeft, fallback));
    }

    private static double ClampCorner(double value, double fallback)
        => Math.Clamp(value > 0 ? value : fallback, 0, 24);

    public void ApplyCornerRadius(System.Windows.CornerRadius radii)
    {
        _cornerRadii = new System.Windows.CornerRadius(
            Math.Clamp(radii.TopLeft, 0, 24),
            Math.Clamp(radii.TopRight, 0, 24),
            Math.Clamp(radii.BottomRight, 0, 24),
            Math.Clamp(radii.BottomLeft, 0, 24));
        RootBorder.CornerRadius = _cornerRadii;
        if (_cornerRadii.TopLeft > 0 || _cornerRadii.TopRight > 0 || _cornerRadii.BottomRight > 0 || _cornerRadii.BottomLeft > 0)
            UpdateRootBorderClip();
        else
            RootBorder.Clip = null;
    }

    private void UpdateRootBorderClip()
    {
        var w = RootBorder.ActualWidth;
        var h = RootBorder.ActualHeight;
        if (w <= 0 || h <= 0) return;
        var tl = _cornerRadii.TopLeft;
        var tr = _cornerRadii.TopRight;
        var br = _cornerRadii.BottomRight;
        var bl = _cornerRadii.BottomLeft;
        if (tl <= 0 && tr <= 0 && br <= 0 && bl <= 0) { RootBorder.Clip = null; return; }
        RootBorder.Clip = MakeRoundedRectGeometry(w, h, tl, tr, br, bl);
    }

    private static System.Windows.Media.Geometry MakeRoundedRectGeometry(double w, double h, double tl, double tr, double br, double bl)
    {
        var path = new System.Windows.Media.PathFigure { StartPoint = new System.Windows.Point(tl, 0), IsClosed = true };
        path.Segments.Add(new System.Windows.Media.LineSegment(new System.Windows.Point(w - tr, 0), true));
        if (tr > 0) path.Segments.Add(new System.Windows.Media.ArcSegment(new System.Windows.Point(w, tr), new System.Windows.Size(tr, tr), 0, false, System.Windows.Media.SweepDirection.Clockwise, true));
        else path.Segments.Add(new System.Windows.Media.LineSegment(new System.Windows.Point(w, 0), true));
        path.Segments.Add(new System.Windows.Media.LineSegment(new System.Windows.Point(w, h - br), true));
        if (br > 0) path.Segments.Add(new System.Windows.Media.ArcSegment(new System.Windows.Point(w - br, h), new System.Windows.Size(br, br), 0, false, System.Windows.Media.SweepDirection.Clockwise, true));
        else path.Segments.Add(new System.Windows.Media.LineSegment(new System.Windows.Point(w, h), true));
        path.Segments.Add(new System.Windows.Media.LineSegment(new System.Windows.Point(bl, h), true));
        if (bl > 0) path.Segments.Add(new System.Windows.Media.ArcSegment(new System.Windows.Point(0, h - bl), new System.Windows.Size(bl, bl), 0, false, System.Windows.Media.SweepDirection.Clockwise, true));
        else path.Segments.Add(new System.Windows.Media.LineSegment(new System.Windows.Point(0, h), true));
        path.Segments.Add(new System.Windows.Media.LineSegment(new System.Windows.Point(0, tl), true));
        if (tl > 0) path.Segments.Add(new System.Windows.Media.ArcSegment(new System.Windows.Point(tl, 0), new System.Windows.Size(tl, tl), 0, false, System.Windows.Media.SweepDirection.Clockwise, true));
        else path.Segments.Add(new System.Windows.Media.LineSegment(new System.Windows.Point(0, 0), true));
        return new System.Windows.Media.PathGeometry(new[] { path });
    }

    private void MainWindow_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.S && Keyboard.Modifiers == ModifierKeys.Control)
        {
            SaveCurrentNoteAndShowToast();
            e.Handled = true;
        }
    }

    private void LoadAllTabs()
    {
        var dir = PathHelper.NotesDirectory;
        var savedOrder = TabOrderPersistence.Load();
        var tabMeta = TabMetaPersistence.Load();
        var allFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var path in Directory.GetFiles(dir, "*.txt"))
            allFiles.Add(path);

        // Load tabs in saved order
        foreach (var path in savedOrder)
        {
            if (!allFiles.Contains(path) || !File.Exists(path)) continue;
            allFiles.Remove(path);
            var name = Path.GetFileNameWithoutExtension(path);
            var content = File.ReadAllText(path);
            var meta = tabMeta.TryGetValue(path, out var m) ? m : null;
            _tabs.Add(new NoteTab(path, name, content, meta?.IconPath, meta?.Color));
        }
        // Append any untracked notes (new files) at the end, sorted by name
        var untracked = allFiles.OrderBy(p => Path.GetFileNameWithoutExtension(p), StringComparer.OrdinalIgnoreCase).ToList();
        foreach (var path in untracked)
        {
            var name = Path.GetFileNameWithoutExtension(path);
            var content = File.ReadAllText(path);
            var meta = tabMeta.TryGetValue(path, out var m) ? m : null;
            _tabs.Add(new NoteTab(path, name, content, meta?.IconPath, meta?.Color));
        }
        RebuildTabStrip();
    }

    private void RebuildTabStrip()
    {
        TabStrip.Children.Clear();
        for (var i = 0; i < _tabs.Count; i++)
        {
            var index = i;
            var tab = _tabs[i];
            var content = new System.Windows.Controls.StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal };
            if (!string.IsNullOrEmpty(tab.IconPath) && File.Exists(tab.IconPath))
            {
                try
                {
                    var img = new System.Windows.Controls.Image
                    {
                        Source = new System.Windows.Media.Imaging.BitmapImage(new Uri(tab.IconPath, UriKind.Absolute)),
                        Width = 14,
                        Height = 14,
                        Margin = new Thickness(0, 0, 4, 0),
                        VerticalAlignment = VerticalAlignment.Center
                    };
                    content.Children.Add(img);
                }
                catch { /* ignore */ }
            }
            content.Children.Add(new System.Windows.Controls.TextBlock
            {
                Text = tab.DisplayName,
                VerticalAlignment = VerticalAlignment.Center
            });
            var btn = new System.Windows.Controls.Button
            {
                Content = content,
                Style = (Style)FindResource("TabButtonStyle"),
                Tag = index
            };
            btn.Click += (_, _) => SelectTab(index);
            btn.PreviewMouseLeftButtonDown += (s, e) => { _dragStartPos = e.GetPosition(null); _dragSourceIndex = index; };
            btn.PreviewMouseMove += TabButton_PreviewMouseMove;
            btn.PreviewMouseLeftButtonUp += (s, e) => { _dragSourceIndex = -1; };
            btn.ContextMenu = CreateTabContextMenu(index);
            var borderBrush = System.Windows.Media.Brushes.Transparent;
            if (!string.IsNullOrEmpty(tab.HighlightColor))
            {
                try
                {
                    borderBrush = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(tab.HighlightColor));
                }
                catch { /* ignore */ }
            }
            var border = new System.Windows.Controls.Border
            {
                BorderThickness = new Thickness(0, 0, 0, 2),
                BorderBrush = borderBrush,
                Background = System.Windows.Media.Brushes.Transparent,
                Child = btn,
                Tag = index
            };
            TabStrip.Children.Add(border);
        }
    }

    private System.Windows.Controls.ContextMenu CreateTabContextMenu(int tabIndex)
    {
        var menu = new System.Windows.Controls.ContextMenu();
        var rename = new System.Windows.Controls.MenuItem { Header = "Rename..." };
        rename.Click += (_, _) => RenameTab(tabIndex);
        menu.Items.Add(rename);
        var setIcon = new System.Windows.Controls.MenuItem { Header = "Set icon..." };
        setIcon.Click += (_, _) => SetTabIcon(tabIndex);
        menu.Items.Add(setIcon);
        var setColor = new System.Windows.Controls.MenuItem { Header = "Set highlight color..." };
        setColor.Click += (_, _) => SetTabHighlightColor(tabIndex);
        menu.Items.Add(setColor);
        var removeIcon = new System.Windows.Controls.MenuItem { Header = "Remove icon" };
        removeIcon.Click += (_, _) => RemoveTabIcon(tabIndex);
        menu.Items.Add(removeIcon);
        var removeColor = new System.Windows.Controls.MenuItem { Header = "Remove highlight color" };
        removeColor.Click += (_, _) => RemoveTabHighlightColor(tabIndex);
        menu.Items.Add(removeColor);
        return menu;
    }

    private void RenameTab(int tabIndex)
    {
        if (tabIndex < 0 || tabIndex >= _tabs.Count) return;
        var tab = _tabs[tabIndex];
        var dialog = new NewTabDialog { Owner = this };
        dialog.SetInitialName(tab.DisplayName);
        dialog.Title = "Rename note";
        if (dialog.ShowDialog() != true || string.IsNullOrWhiteSpace(dialog.NoteName))
            return;
        var name = dialog.NoteName.Trim();
        var invalid = Path.GetInvalidFileNameChars();
        foreach (var c in invalid)
            name = name.Replace(c, '_');
        if (string.IsNullOrEmpty(name)) return;
        if (string.Equals(name, tab.DisplayName, StringComparison.OrdinalIgnoreCase)) return;
        var newPath = Path.Combine(PathHelper.NotesDirectory, name + ".txt");
        if (File.Exists(newPath)) return;
        try
        {
            File.Move(tab.FilePath, newPath);
        }
        catch { return; }
        var newTab = new NoteTab(newPath, name, tab.Content, tab.IconPath, tab.HighlightColor);
        _tabs.RemoveAt(tabIndex);
        _tabs.Insert(tabIndex, newTab);
        SaveTabOrder();
        SaveTabMeta();
        RebuildTabStrip();
        if (_currentIndex >= 0) UpdateTabStripSelection();
    }

    private void SetTabIcon(int tabIndex)
    {
        if (tabIndex < 0 || tabIndex >= _tabs.Count) return;
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "Image files|*.png;*.jpg;*.jpeg;*.ico;*.bmp|All files|*.*",
            Title = "Choose tab icon"
        };
        if (dlg.ShowDialog() != true) return;
        var stored = TabMetaPersistence.StoreIcon(dlg.FileName);
        if (stored == null) return;
        _tabs[tabIndex].IconPath = stored;
        SaveTabMeta();
        RebuildTabStrip();
        if (_currentIndex >= 0) UpdateTabStripSelection();
    }

    private void RemoveTabIcon(int tabIndex)
    {
        if (tabIndex < 0 || tabIndex >= _tabs.Count) return;
        _tabs[tabIndex].IconPath = null;
        SaveTabMeta();
        RebuildTabStrip();
        if (_currentIndex >= 0) UpdateTabStripSelection();
    }

    private void SetTabHighlightColor(int tabIndex)
    {
        if (tabIndex < 0 || tabIndex >= _tabs.Count) return;
        using var dlg = new System.Windows.Forms.ColorDialog();
        if (dlg.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;
        var c = dlg.Color;
        _tabs[tabIndex].HighlightColor = $"#{c.R:X2}{c.G:X2}{c.B:X2}";
        SaveTabMeta();
        RebuildTabStrip();
        if (_currentIndex >= 0) UpdateTabStripSelection();
    }

    private void RemoveTabHighlightColor(int tabIndex)
    {
        if (tabIndex < 0 || tabIndex >= _tabs.Count) return;
        _tabs[tabIndex].HighlightColor = null;
        SaveTabMeta();
        RebuildTabStrip();
        if (_currentIndex >= 0) UpdateTabStripSelection();
    }

    private void SaveTabMeta()
    {
        var meta = new Dictionary<string, TabMeta>(StringComparer.OrdinalIgnoreCase);
        foreach (var t in _tabs)
            meta[t.FilePath] = new TabMeta(t.IconPath, t.HighlightColor);
        TabMetaPersistence.Save(meta);
    }

    private void TabButton_PreviewMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed || _dragSourceIndex < 0) return;
        var pos = e.GetPosition(null);
        if (Math.Abs(pos.X - _dragStartPos.X) + Math.Abs(pos.Y - _dragStartPos.Y) < 6) return;
        var btn = (System.Windows.Controls.Button)sender;
        DragDrop.DoDragDrop(btn, new System.Windows.DataObject(TabIndexFormat, _dragSourceIndex), System.Windows.DragDropEffects.Move);
        _dragSourceIndex = -1;
    }

    private void TabStrip_DragOver(object sender, System.Windows.DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(TabIndexFormat) ? System.Windows.DragDropEffects.Move : System.Windows.DragDropEffects.None;
        e.Handled = true;
    }

    private void TabStrip_Drop(object sender, System.Windows.DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(TabIndexFormat)) return;
        var sourceIndex = (int)e.Data.GetData(TabIndexFormat);
        var dropPos = e.GetPosition(TabStrip);
        int dropIndex = GetDropIndex(dropPos.X);
        if (dropIndex < 0) dropIndex = _tabs.Count;
        MoveTab(sourceIndex, dropIndex);
        SaveTabOrder();
        e.Handled = true;
    }

    private int GetDropIndex(double x)
    {
        for (var i = 0; i < TabStrip.Children.Count; i++)
        {
            var el = TabStrip.Children[i] as System.Windows.FrameworkElement;
            if (el == null) continue;
            var left = el.TranslatePoint(new System.Windows.Point(0, 0), TabStrip);
            if (x < left.X + el.ActualWidth / 2)
                return i;
        }
        return TabStrip.Children.Count;
    }

    private void MoveTab(int fromIndex, int toIndex)
    {
        if (fromIndex < 0 || fromIndex >= _tabs.Count || fromIndex == toIndex) return;
        if (toIndex > fromIndex) toIndex--;
        if (toIndex < 0 || toIndex > _tabs.Count) return;
        var selectedTab = _currentIndex >= 0 && _currentIndex < _tabs.Count ? _tabs[_currentIndex] : null;
        var item = _tabs[fromIndex];
        _tabs.RemoveAt(fromIndex);
        _tabs.Insert(toIndex, item);
        _currentIndex = selectedTab != null ? _tabs.IndexOf(selectedTab) : -1;
        RebuildTabStrip();
        if (_currentIndex >= 0)
            UpdateTabStripSelection();
    }

    private void UpdateTabStripSelection()
    {
        foreach (var child in TabStrip.Children)
        {
            if (child is System.Windows.Controls.Border border && border.Child is System.Windows.Controls.Button b && b.Tag is int i)
            {
                b.Foreground = i == _currentIndex
                    ? (System.Windows.Media.Brush)FindResource("TextBrush")
                    : new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x80, 0x80, 0x80));
            }
        }
    }

    private void SaveTabOrder()
    {
        TabOrderPersistence.Save(_tabs.Select(t => t.FilePath));
    }

    private void SelectTab(int index)
    {
        if (index < 0 || index >= _tabs.Count) return;

        // Save current tab if dirty
        if (_currentIndex >= 0 && _currentIndex < _tabs.Count)
        {
            var current = _tabs[_currentIndex];
            current.Content = Editor.Text;
            current.IsDirty = false;
            SaveTabToFile(current);
        }

        _currentIndex = index;
        var selected = _tabs[index];
        _ignoreTextChange = true;
        Editor.Text = selected.Content;
        _ignoreTextChange = false;
        UpdateTabStripSelection();
    }

    private void SaveTabToFile(NoteTab tab)
    {
        try
        {
            File.WriteAllText(tab.FilePath, tab.Content);
        }
        catch
        {
            // Ignore
        }
    }

    private void Editor_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        if (_ignoreTextChange) return;
        if (_currentIndex >= 0 && _currentIndex < _tabs.Count)
            _tabs[_currentIndex].IsDirty = true;
    }

    private void AddTabButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new NewTabDialog { Owner = this };
        if (dialog.ShowDialog() != true || string.IsNullOrWhiteSpace(dialog.NoteName))
            return;

        var name = dialog.NoteName.Trim();
        var invalid = Path.GetInvalidFileNameChars();
        foreach (var c in invalid)
            name = name.Replace(c, '_');
        if (string.IsNullOrEmpty(name)) return;

        var path = Path.Combine(PathHelper.NotesDirectory, name + ".txt");
        if (!File.Exists(path))
            File.WriteAllText(path, "");

        var tab = new NoteTab(path, name, "", null, null);
        _tabs.Add(tab);
        RebuildTabStrip();
        SelectTab(_tabs.Count - 1);
        SaveTabOrder();
    }

    private void DragButton_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left)
            DragMove();
    }

    private void SaveCurrentNoteAndShowToast()
    {
        if (_currentIndex < 0 || _currentIndex >= _tabs.Count) return;

        var tab = _tabs[_currentIndex];
        tab.Content = Editor.Text;
        tab.IsDirty = false;
        SaveTabToFile(tab);

        Toast.Visibility = Visibility.Visible;
        _toastHideTimer?.Stop();
        _toastHideTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        _toastHideTimer.Tick += (_, _) =>
        {
            _toastHideTimer.Stop();
            Toast.Visibility = Visibility.Collapsed;
        };
        _toastHideTimer.Start();
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        if (_currentIndex >= 0 && _currentIndex < _tabs.Count)
        {
            _tabs[_currentIndex].Content = Editor.Text;
            SaveTabToFile(_tabs[_currentIndex]);
        }
        WindowStatePersistence.Save(this);
        SaveTabOrder();
        base.OnClosing(e);
    }

    private sealed class NoteTab
    {
        public string FilePath { get; }
        public string DisplayName { get; }
        public string Content { get; set; }
        public bool IsDirty { get; set; }
        public string? IconPath { get; set; }
        public string? HighlightColor { get; set; }

        public NoteTab(string filePath, string displayName, string content, string? iconPath, string? highlightColor)
        {
            FilePath = filePath;
            DisplayName = displayName;
            Content = content;
            IconPath = iconPath;
            HighlightColor = highlightColor;
        }
    }
}
