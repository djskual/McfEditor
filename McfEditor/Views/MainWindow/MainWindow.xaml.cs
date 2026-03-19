using McfEditor.IO;
using McfEditor.Models;
using McfEditor.Settings;
using McfEditor.UI.Dialogs;
using McfEditor.Views;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;

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

        Loaded += (_, __) =>
        {
            _pythonFolder = ResolvePythonFolder();
            SetStatus("Ready");
        };

        Closing += (_, __) => PersistWindowPlacementToSettings();
    }
}
