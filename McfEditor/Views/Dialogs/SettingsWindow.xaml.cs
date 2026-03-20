using McfEditor.Settings;
using McfEditor.UI.Interop;
using Microsoft.Win32;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;

namespace McfEditor.Views;

public partial class SettingsWindow : Window
{
    private readonly AppSettings _workingCopy;

    public AppSettings ResultSettings { get; private set; }

    public SettingsWindow(AppSettings settings)
    {
        InitializeComponent();

        _workingCopy = settings.Clone();
        ResultSettings = _workingCopy.Clone();

        RememberWindowPlacementCheck.IsChecked = _workingCopy.RememberWindowSizeAndPosition;
        PythonPathTextBox.Text = _workingCopy.PythonExecutablePath;
        UseImageIdMapWhenAvailableCheck.IsChecked = _workingCopy.UseImageIdMapWhenAvailable;
        AskBeforeUsingImageIdMapCheck.IsChecked = _workingCopy.AskBeforeUsingImageIdMap;
        OpenWorkingFolderCheck.IsChecked = _workingCopy.OpenWorkingFolderAfterExtraction;
        DefaultOutputFolderTextBox.Text = _workingCopy.DefaultOutputFolder ?? string.Empty;

        AutoCheckUpdatesCheck.IsChecked = _workingCopy.AutoCheckUpdatesOnStartup;
        IncludePrereleaseCheck.IsChecked = _workingCopy.IncludePrereleaseVersionsInUpdateCheck;

        Loaded += (_, __) => ShowSection(0);
    }

    private void SectionList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded)
            return;

        ShowSection(SectionList.SelectedIndex);
    }

    private void ShowSection(int index)
    {
        GeneralPanel.Visibility = Visibility.Collapsed;
        PythonPanel.Visibility = Visibility.Collapsed;
        OutputPanel.Visibility = Visibility.Collapsed;
        UpdatesPanel.Visibility = Visibility.Collapsed;

        switch (index)
        {
            case 1:
                PythonPanel.Visibility = Visibility.Visible;
                break;
            case 2:
                OutputPanel.Visibility = Visibility.Visible;
                break;
            case 3:
                UpdatesPanel.Visibility = Visibility.Visible;
                break;
            default:
                GeneralPanel.Visibility = Visibility.Visible;
                break;
        }

        SectionHost.Opacity = 0.35;
        var fade = new DoubleAnimation
        {
            From = 0.0,
            To = 1.0,
            Duration = TimeSpan.FromMilliseconds(220)
        };
        SectionHost.BeginAnimation(OpacityProperty, fade);
    }

    private void BrowsePython_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Choose python executable",
            Filter = "Python executable|python.exe;py.exe|All files|*.*",
            CheckFileExists = true
        };

        if (dialog.ShowDialog(this) == true)
            PythonPathTextBox.Text = dialog.FileName;
    }

    private void BrowseOutputFolder_Click(object sender, RoutedEventArgs e)
    {
        var folder = FolderPicker.PickFolder("Choose default output folder");
        if (!string.IsNullOrWhiteSpace(folder))
            DefaultOutputFolderTextBox.Text = folder;
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        _workingCopy.RememberWindowSizeAndPosition = RememberWindowPlacementCheck.IsChecked == true;
        _workingCopy.PythonExecutablePath = string.IsNullOrWhiteSpace(PythonPathTextBox.Text)
            ? "python"
            : PythonPathTextBox.Text.Trim();
        _workingCopy.UseImageIdMapWhenAvailable = UseImageIdMapWhenAvailableCheck.IsChecked == true;
        _workingCopy.AskBeforeUsingImageIdMap = AskBeforeUsingImageIdMapCheck.IsChecked == true;
        _workingCopy.OpenWorkingFolderAfterExtraction = OpenWorkingFolderCheck.IsChecked == true;
        _workingCopy.DefaultOutputFolder = string.IsNullOrWhiteSpace(DefaultOutputFolderTextBox.Text)
            ? null
            : DefaultOutputFolderTextBox.Text.Trim();
        _workingCopy.AutoCheckUpdatesOnStartup = AutoCheckUpdatesCheck.IsChecked == true;
        _workingCopy.IncludePrereleaseVersionsInUpdateCheck = IncludePrereleaseCheck.IsChecked == true;

        _workingCopy.Normalize();
        ResultSettings = _workingCopy.Clone();
        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
