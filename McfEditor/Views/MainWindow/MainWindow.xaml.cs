using McfEditor.IO;
using McfEditor.Models;
using McfEditor.Settings;
using McfEditor.UI.Dialogs;
using McfEditor.UndoRedo;
using McfEditor.Views;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Linq;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace McfEditor;

public partial class MainWindow : Window, INotifyPropertyChanged
{
    public static readonly RoutedUICommand OpenMcfCommand =
        new("Open MCF", nameof(OpenMcfCommand), typeof(MainWindow));

    public static readonly RoutedUICommand UndoProjectCommand =
        new("Undo", nameof(UndoProjectCommand), typeof(MainWindow));

    public static readonly RoutedUICommand RedoProjectCommand =
        new("Redo", nameof(RedoProjectCommand), typeof(MainWindow)); 

    private readonly McfExtractionService _extractionService = new();
    private readonly McfCompressionService _compressionService = new();

    private readonly ObservableCollection<McfImageEntry> _visibleEntries = new();
    private readonly ObservableCollection<ExplorerNode> _explorerNodes = new();
    private readonly McfProject _project = new();
    private bool _isBusy;

    private ExplorerNode? _contextMenuNode;

    private readonly UndoRedoManager _undoRedoManager = new();

    public event PropertyChangedEventHandler? PropertyChanged;

    public string UndoMenuHeader =>
    _undoRedoManager.CanUndo && !string.IsNullOrWhiteSpace(_undoRedoManager.UndoDescription)
        ? $"_Undo {_undoRedoManager.UndoDescription}"
        : "_Undo";

    public string RedoMenuHeader =>
        _undoRedoManager.CanRedo && !string.IsNullOrWhiteSpace(_undoRedoManager.RedoDescription)
            ? $"_Redo {_undoRedoManager.RedoDescription}"
            : "_Redo"; 

    public ObservableCollection<ExplorerNode> ExplorerNodes => _explorerNodes;

    public MainWindow()
    {
        InitializeComponent();
        DataContext = this;

        ImagesTreeView.ItemsSource = _explorerNodes;

        ApplyWindowPlacementFromSettings();
        UpdateWindowTitle();
        ResetSelectionUi();

        CommandBindings.Add(new CommandBinding(OpenMcfCommand, async (_, __) => await OpenMcfAsync()));
        CommandBindings.Add(new CommandBinding(UndoProjectCommand, Undo_CommandExecuted, Undo_CommandCanExecute));
        CommandBindings.Add(new CommandBinding(RedoProjectCommand, Redo_CommandExecuted, Redo_CommandCanExecute));

        InputBindings.Add(new KeyBinding(OpenMcfCommand, new KeyGesture(Key.O, ModifierKeys.Control)));
        InputBindings.Add(new KeyBinding(UndoProjectCommand, new KeyGesture(Key.Z, ModifierKeys.Control)));
        InputBindings.Add(new KeyBinding(RedoProjectCommand, new KeyGesture(Key.Y, ModifierKeys.Control)));

        Loaded += async (_, __) =>
        {
            SetStatus("Ready");
            UpdateWindowTitle();
            RefreshUndoRedoUi();

            if (AppSettingsStore.Current.AutoCheckUpdatesOnStartup)
                await CheckForUpdatesAsync(silentIfUpToDate: true, silentOnError: true);
        };

        Closing += (_, __) =>
        {
            PersistWindowPlacementToSettings();
            PurgeTempRootDirectory();
        };
    }

    private static ExplorerNode? FindFirstLeaf(IEnumerable<ExplorerNode> nodes)
    {
        foreach (var node in nodes)
        {
            if (!node.IsFolder && node.Entry is not null)
                return node;

            var leaf = FindFirstLeaf(node.Children);
            if (leaf is not null)
                return leaf;
        }

        return null;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private void RefreshDirtyState()
    {
        if (_project == null)
            return;

        _project.IsDirty = _project.Entries.Any(e => e.IsModified);
        UpdateWindowTitle();
    }

    private void Undo_Click(object sender, RoutedEventArgs e)
    {
        PerformUndo();
    }

    private void Redo_Click(object sender, RoutedEventArgs e)
    {
        PerformRedo();
    }

    private void RefreshUndoRedoUi()
    {
        OnPropertyChanged(nameof(UndoMenuHeader));
        OnPropertyChanged(nameof(RedoMenuHeader));
        CommandManager.InvalidateRequerySuggested();
    }

    private void Undo_CommandCanExecute(object sender, CanExecuteRoutedEventArgs e)
    {
        e.CanExecute = !_isBusy && _undoRedoManager.CanUndo;
    }

    private void Redo_CommandCanExecute(object sender, CanExecuteRoutedEventArgs e)
    {
        e.CanExecute = !_isBusy && _undoRedoManager.CanRedo;
    }

    private void Undo_CommandExecuted(object sender, ExecutedRoutedEventArgs e)
    {
        PerformUndo();
    }

    private void Redo_CommandExecuted(object sender, ExecutedRoutedEventArgs e)
    {
        PerformRedo();
    }

    private void PerformUndo()
    {
        if (!_undoRedoManager.CanUndo)
            return;

        var currentEntry = _selectedEntry;

        _undoRedoManager.Undo();
        RefreshDirtyState();

        if (currentEntry is not null)
        {
            _selectedEntry = currentEntry;
            UpdateSelectionUi(currentEntry);
            RefreshPreview(currentEntry);
            SelectNodeForEntry(currentEntry);
        }
        else
        {
            RefreshSelectedEntryUi();
        }

        SetStatus("Undo applied");
        RefreshUndoRedoUi();
    }

    private void PerformRedo()
    {
        if (!_undoRedoManager.CanRedo)
            return;

        var currentEntry = _selectedEntry;

        _undoRedoManager.Redo();
        RefreshDirtyState();

        if (currentEntry is not null)
        {
            _selectedEntry = currentEntry;
            UpdateSelectionUi(currentEntry);
            RefreshPreview(currentEntry);
            SelectNodeForEntry(currentEntry);
        }
        else
        {
            RefreshSelectedEntryUi();
        }

        SetStatus("Redo applied");
        RefreshUndoRedoUi();
    }

    private void UpdateWindowTitle()
    {
        var version = GetBuildTag();

        if (string.Equals(version, "unknown", StringComparison.OrdinalIgnoreCase))
            version = "dev";

        var dirtySuffix = (_project != null && _project.IsDirty) ? " *" : string.Empty;

        Title = $"McfEditor {version}{dirtySuffix}";
    }

    private readonly struct TagVersion : IComparable<TagVersion>
    {
        public int Major { get; }
        public int Minor { get; }
        public int Patch { get; }
        public string? PreLabel { get; }
        public int PreNumber { get; }
        public bool IsPrerelease => !string.IsNullOrWhiteSpace(PreLabel);

        public TagVersion(int major, int minor, int patch, string? preLabel, int preNumber)
        {
            Major = major;
            Minor = minor;
            Patch = patch;
            PreLabel = preLabel;
            PreNumber = preNumber;
        }

        public int CompareTo(TagVersion other)
        {
            var c = Major.CompareTo(other.Major);
            if (c != 0) return c;

            c = Minor.CompareTo(other.Minor);
            if (c != 0) return c;

            c = Patch.CompareTo(other.Patch);
            if (c != 0) return c;

            var thisIsStable = string.IsNullOrWhiteSpace(PreLabel);
            var otherIsStable = string.IsNullOrWhiteSpace(other.PreLabel);

            if (thisIsStable && otherIsStable) return 0;
            if (thisIsStable) return 1;
            if (otherIsStable) return -1;

            c = string.Compare(PreLabel, other.PreLabel, StringComparison.OrdinalIgnoreCase);
            if (c != 0) return c;

            return PreNumber.CompareTo(other.PreNumber);
        }
    }

    private async void CheckUpdate_Click(object sender, RoutedEventArgs e)
    {
        await CheckForUpdatesAsync(silentIfUpToDate: false, silentOnError: false);
    }

    private static bool TryParseTagVersion(string? tag, out TagVersion version)
    {
        version = default;

        if (string.IsNullOrWhiteSpace(tag))
            return false;

        var s = tag.Trim();

        if (s.StartsWith("v", StringComparison.OrdinalIgnoreCase))
            s = s[1..];

        string corePart = s;
        string? prePart = null;

        var dashIndex = s.IndexOf('-');
        if (dashIndex >= 0)
        {
            corePart = s[..dashIndex];
            prePart = s[(dashIndex + 1)..];
        }

        var core = corePart.Split('.');
        if (core.Length != 3)
            return false;

        if (!int.TryParse(core[0], out var major)) return false;
        if (!int.TryParse(core[1], out var minor)) return false;
        if (!int.TryParse(core[2], out var patch)) return false;

        string? preLabel = null;
        int preNumber = 0;

        if (!string.IsNullOrWhiteSpace(prePart))
        {
            var pre = prePart.Split('.', 2, StringSplitOptions.RemoveEmptyEntries);

            preLabel = pre[0].Trim();

            if (pre.Length > 1 && !int.TryParse(pre[1], out preNumber))
                preNumber = 0;
        }

        version = new TagVersion(major, minor, patch, preLabel, preNumber);
        return true;
    }

    private static string GetBuildTag()
    {
        try
        {
            var path = Path.Combine(AppContext.BaseDirectory, "git-tag.txt");
            if (!File.Exists(path))
                return "unknown";

            var tag = File.ReadAllText(path).Trim();
            if (string.IsNullOrWhiteSpace(tag))
                return "unknown";

            return tag;
        }
        catch
        {
            return "unknown";
        }
    }

    private async Task CheckForUpdatesAsync(bool silentIfUpToDate, bool silentOnError)
    {
        try
        {
            using var client = new HttpClient();
            client.DefaultRequestHeaders.Add("User-Agent", "McfEditor");
            client.Timeout = TimeSpan.FromSeconds(5);

            var json = await client.GetStringAsync(
                "https://api.github.com/repos/djskual/McfEditor/tags");

            using var doc = JsonDocument.Parse(json);

            if (doc.RootElement.ValueKind != JsonValueKind.Array || doc.RootElement.GetArrayLength() == 0)
            {
                if (!silentIfUpToDate)
                {
                    AppMessageBox.Show(
                        "No tag found on GitHub.",
                        "Update check",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
                return;
            }

            bool includePrerelease = AppSettingsStore.Current.IncludePrereleaseVersionsInUpdateCheck;

            string? latestTag = null;
            TagVersion? latestVersion = null;

            foreach (var element in doc.RootElement.EnumerateArray())
            {
                if (!element.TryGetProperty("name", out var nameProp))
                    continue;

                var tagName = nameProp.GetString();
                if (string.IsNullOrWhiteSpace(tagName))
                    continue;

                if (!TryParseTagVersion(tagName, out var parsed))
                    continue;

                if (!includePrerelease && parsed.IsPrerelease)
                    continue;

                if (latestVersion == null || parsed.CompareTo(latestVersion.Value) > 0)
                {
                    latestVersion = parsed;
                    latestTag = tagName.Trim();
                }
            }

            if (latestVersion == null || string.IsNullOrWhiteSpace(latestTag))
            {
                if (!silentIfUpToDate)
                {
                    AppMessageBox.Show(
                        "No matching version tag found on GitHub.",
                        "Update check",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
                return;
            }

            var currentTag = GetBuildTag();

            if (!TryParseTagVersion(currentTag, out var currentVersion))
            {
                if (!silentOnError)
                {
                    AppMessageBox.Show(
                        $"Current version tag is invalid: {currentTag}",
                        "Update check",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                }
                return;
            }

            if (latestVersion.Value.CompareTo(currentVersion) > 0)
            {
                var result = AppMessageBox.Show(
                    $"New version available: {latestTag}\n\nOpen download page?",
                    "Update available",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Information);

                if (result == MessageBoxResult.Yes)
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "https://github.com/djskual/McfEditor/releases",
                        UseShellExecute = true
                    });
                }
            }
            else if (!silentIfUpToDate)
            {
                AppMessageBox.Show(
                    "You already have the latest version.",
                    "No update",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
        }
        catch (Exception ex)
        {
            if (!silentOnError)
            {
                AppMessageBox.Show(
                    $"Unable to check updates.\n\n{ex.Message}",
                    "Update error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
        }
    }
}
