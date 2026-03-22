using McfEditor.Models;
using McfEditor.Settings;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Input;

namespace McfEditor;

public partial class MainWindow
{
    private void ShowProgress(string status, double percent)
    {
        StatusTextBlock.Text = status;
        ProgressHost.Visibility = Visibility.Visible;

        ProgressHost.UpdateLayout();

        double hostWidth = ProgressHost.ActualWidth;
        if (hostWidth <= 0)
            hostWidth = 220;

        double clamped = Math.Max(0, Math.Min(100, percent));
        ProgressFill.Width = hostWidth * (clamped / 100.0);

        Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.Background);
    }

    private void HideProgress(string status = "Ready")
    {
        StatusTextBlock.Text = status;
        ProgressFill.Width = 0;
        ProgressHost.Visibility = Visibility.Collapsed;
    }

    private void RebuildExplorerTree(IEnumerable<McfImageEntry> entries)
    {
        _explorerNodes.Clear();

        var entryList = entries.ToList();
        bool hasStructuredPaths = entryList.Any(e => !string.IsNullOrWhiteSpace(e.MappedPath));

        var orderedEntries = hasStructuredPaths
            ? entryList
                .OrderBy(e => string.IsNullOrWhiteSpace(e.RelativePath) ? e.FileName : e.RelativePath,
                         StringComparer.OrdinalIgnoreCase)
            : entryList
                .OrderBy(e => e.Index);

        foreach (var entry in orderedEntries)
        {
            var relativePath = string.IsNullOrWhiteSpace(entry.RelativePath)
                ? entry.FileName
                : entry.RelativePath;

            var parts = relativePath
                .Split(new[] { '\\', '/' }, StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length == 0)
                continue;

            ObservableCollection<ExplorerNode> currentChildren = _explorerNodes;

            for (int i = 0; i < parts.Length; i++)
            {
                var isLast = i == parts.Length - 1;
                var name = parts[i];

                var existing = currentChildren.FirstOrDefault(n =>
                    n.Name.Equals(name, StringComparison.OrdinalIgnoreCase) &&
                    n.IsFolder == !isLast);

                if (existing == null)
                {
                    existing = new ExplorerNode
                    {
                        Name = name,
                        FullPath = string.Join("/", parts.Take(i + 1)),
                        IsFolder = !isLast,
                        Entry = isLast ? entry : null
                    };

                    currentChildren.Add(existing);
                    SortExplorerNodes(currentChildren);
                }

                currentChildren = existing.Children;
            }
        }

        SortExplorerNodes(_explorerNodes);
    }

    private static void SortExplorerNodes(ObservableCollection<ExplorerNode> nodes)
    {
        bool allImageLeaves = nodes.Count > 0 && nodes.All(n => !n.IsFolder && n.Entry is not null);

        List<ExplorerNode> ordered;

        if (allImageLeaves)
        {
            ordered = nodes
                .OrderBy(n => n.Entry!.Index)
                .ToList();
        }
        else
        {
            ordered = nodes
                .OrderByDescending(n => n.IsFolder)
                .ThenBy(n => n.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        nodes.Clear();

        foreach (var node in ordered)
        {
            if (node.Children.Count > 0)
                SortExplorerNodes(node.Children);

            nodes.Add(node);
        }
    }

    private void SetBusy(bool isBusy, string? message = null)
    {
        _isBusy = isBusy;
        Cursor = isBusy ? System.Windows.Input.Cursors.Wait : null;

        MainMenu.IsEnabled = !isBusy;
        ContentRoot.IsEnabled = !isBusy;

        CommandManager.InvalidateRequerySuggested();

        if (isBusy)
        {
            if (!string.IsNullOrWhiteSpace(message))
                ShowProgress(message, 0);
            else
                ShowProgress("Working...", 0);
        }
        else
        {
            HideProgress(StatusTextBlock.Text);
        }
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
        bool hasStructuredPaths = _project.Entries.Any(x =>
            !string.IsNullOrWhiteSpace(x.MappedPath));

        var filtered = hasStructuredPaths
            ? _project.Entries
                .OrderBy(x => string.IsNullOrWhiteSpace(x.RelativePath) ? x.FileName : x.RelativePath,
                         StringComparer.OrdinalIgnoreCase)
                .ToList()
            : _project.Entries
                .OrderBy(x => x.Index)
                .ToList();

        RebuildExplorerTree(filtered);
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
