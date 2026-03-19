using McfEditor.IO;
using McfEditor.Models;
using McfEditor.Settings;
using McfEditor.UI.Dialogs;
using McfEditor.Views;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text.Json;

namespace McfEditor;

public partial class MainWindow : Window
{
    public static readonly RoutedUICommand OpenMcfCommand =
        new("Open MCF", nameof(OpenMcfCommand), typeof(MainWindow));

    private readonly McfExtractionService _extractionService = new();
    private readonly McfCompressionService _compressionService = new();

    private readonly ObservableCollection<McfImageEntry> _visibleEntries = new();
    private readonly McfProject _project = new();

    private string? _pythonFolder;
    private bool _isBusy;

    public MainWindow()
    {
        InitializeComponent();

        ImageList.ItemsSource = _visibleEntries;

        ApplyWindowPlacementFromSettings();
        UpdateWindowTitle();
        ResetSelectionUi();

        CommandBindings.Add(new CommandBinding(OpenMcfCommand, async (_, __) => await OpenMcfAsync()));
        InputBindings.Add(new KeyBinding(OpenMcfCommand, new KeyGesture(Key.O, ModifierKeys.Control)));

        Loaded += async (_, __) =>
        {
            _pythonFolder = ResolvePythonFolder();
            SetStatus("Ready");
            
            if (AppSettingsStore.Current.AutoCheckUpdatesOnStartup)
                await CheckForUpdatesAsync(silentIfUpToDate: true, silentOnError: true);
        };

        Closing += (_, __) => PersistWindowPlacementToSettings();
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
