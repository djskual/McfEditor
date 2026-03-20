using McfEditor.Settings;
using System.IO;
using System.Linq;
using System.Windows.Input;

namespace McfEditor;

public partial class MainWindow
{
    private void SetBusy(bool isBusy, string? message = null)
    {
        _isBusy = isBusy;
        Cursor = isBusy ? System.Windows.Input.Cursors.Wait : null;

        if (!string.IsNullOrWhiteSpace(message))
            SetStatus(message);
    }

    private void SetStatus(string message)
    {
        StatusTextBlock.Text = message;
    }

    private void ClearProject()
    {
        _project.SourceFilePath = string.Empty;
        _project.WorkingDirectory = string.Empty;
        _project.OutputFilePath = null;
        _project.IsDirty = false;
        _project.Entries.Clear();
        _visibleEntries.Clear();
        _undoRedoManager.Clear();
        RefreshUndoRedoUi();

        PreviewImage.Source = null;
        CurrentFileLabel.Text = "File: -";
        CurrentWorkLabel.Text = "Workdir: -";
        PreviewTitleText.Text = "Preview";
        PreviewHintText.Text = "Open an MCF file to start.";

        ResetSelectionUi();
        UpdateWindowTitle();
    }

    private void RefreshVisibleEntries()
    {
        var query = SearchBox.Text?.Trim() ?? string.Empty;
        var filtered = _project.Entries
            .Where(x =>
                string.IsNullOrWhiteSpace(query)
                || x.Index.ToString().Contains(query, StringComparison.OrdinalIgnoreCase)
                || x.DisplayName.Contains(query, StringComparison.OrdinalIgnoreCase))
            .OrderBy(x => x.Index)
            .ToList();

        _visibleEntries.Clear();
        foreach (var entry in filtered)
            _visibleEntries.Add(entry);
    }

    private static string CreateWorkingDirectory(string sourceFile)
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            "McfEditor",
            Path.GetFileNameWithoutExtension(sourceFile) + "_" + DateTime.Now.ToString("yyyyMMdd_HHmmss"));

        Directory.CreateDirectory(root);
        return root;
    }

    private static AppSettings UpdateSettings(Action<AppSettings> update)
    {
        var copy = AppSettingsStore.Current.Clone();
        update(copy);
        copy.Normalize();
        return copy;
    }

    private string? ResolvePythonFolder()
    {
        var baseDir = AppContext.BaseDirectory;
        var candidates = new[]
        {
            Path.Combine(baseDir, "Python"),
            Path.Combine(baseDir, "..", "..", "..", "Python"),
            Path.Combine(baseDir, "..", "..", "..", "..", "Python")
        };

        return candidates
            .Select(Path.GetFullPath)
            .FirstOrDefault(Directory.Exists);
    }

    private void ApplyWindowPlacementFromSettings()
    {
        var settings = AppSettingsStore.Current;
        if (!settings.RememberWindowSizeAndPosition)
            return;

        if (settings.WindowWidth is > 0)
            Width = settings.WindowWidth.Value;

        if (settings.WindowHeight is > 0)
            Height = settings.WindowHeight.Value;

        if (settings.WindowLeft.HasValue)
            Left = settings.WindowLeft.Value;

        if (settings.WindowTop.HasValue)
            Top = settings.WindowTop.Value;
    }

    private void PersistWindowPlacementToSettings()
    {
        var copy = AppSettingsStore.Current.Clone();

        if (copy.RememberWindowSizeAndPosition)
        {
            copy.WindowWidth = Width;
            copy.WindowHeight = Height;
            copy.WindowLeft = Left;
            copy.WindowTop = Top;
        }

        AppSettingsStore.Save(copy);
    }

    private static string GetTempRootDirectory()
    {
        return Path.Combine(Path.GetTempPath(), "McfEditor");
    }

    private static void PurgeTempRootDirectory()
    {
        var root = GetTempRootDirectory();

        if (!Directory.Exists(root))
            return;

        try
        {
            // Retire les attributs readonly éventuels avant suppression
            foreach (var file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
            {
                try
                {
                    File.SetAttributes(file, FileAttributes.Normal);
                }
                catch
                {
                }
            }

            Directory.Delete(root, recursive: true);
        }
        catch
        {
            // Best effort: on ne bloque pas la fermeture de l'appli
        }
    }
}
