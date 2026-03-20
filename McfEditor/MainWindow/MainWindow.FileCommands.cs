using McfEditor.IO;
using McfEditor.Models;
using McfEditor.Settings;
using McfEditor.UI.Dialogs;
using McfEditor.UndoRedo;
using McfEditor.Views;
using Microsoft.Win32;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Input;

namespace McfEditor;

public partial class MainWindow
{
    private async Task OpenMcfAsync()
    {
        if (_isBusy)
            return;

        var dialog = new OpenFileDialog
        {
            Title = "Open MCF file",
            Filter = "MCF files|*.mcf|All files|*.*",
            CheckFileExists = true
        };

        if (!string.IsNullOrWhiteSpace(AppSettingsStore.Current.LastOpenedMcfPath))
        {
            try
            {
                dialog.InitialDirectory = Path.GetDirectoryName(AppSettingsStore.Current.LastOpenedMcfPath);
            }
            catch
            {
            }
        }

        if (dialog.ShowDialog(this) != true)
            return;

        await OpenMcfInternalAsync(dialog.FileName);
    }

    private async Task OpenMcfInternalAsync(string sourceFile)
    {
        if (_isBusy)
            return;

        var settings = AppSettingsStore.Current;

        try
        {
            SetBusy(true, "Extracting MCF...");
            ShowProgress("Preparing extraction...", 0);
            ClearProject();
            PurgeTempRootDirectory();

            var workingDir = CreateWorkingDirectory(sourceFile);

            var idMapPath = Path.Combine(Path.GetDirectoryName(sourceFile) ?? string.Empty, "imageidmap.res");
            bool useImageIdMap = false;

            if (File.Exists(idMapPath) && settings.UseImageIdMapWhenAvailable)
            {
                useImageIdMap = true;

                if (settings.AskBeforeUsingImageIdMap)
                {
                    var result = AppMessageBox.Show(
                        this,
                        "imageidmap.res was found next to the selected MCF file.\n\nDo you want to use it to organize extracted images into folders?",
                        "Use imageidmap.res",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question);

                    useImageIdMap = result == MessageBoxResult.Yes;
                }
            }

            var progress = new Progress<ProgressInfo>(p =>
            {
                ShowProgress(p.Message, p.Percent);
            });
            
            var manifest = await _extractionService.ExtractAsync(
                                 sourceFile,
                                 workingDir,
                                 useImageIdMap,
                                 progress);

            _project.SourceFilePath = sourceFile;
            _project.WorkingDirectory = manifest.WorkingDirectory;
            _project.OutputFilePath = null;
            _project.IsDirty = false;
            _project.Entries.Clear();
            _undoRedoManager.Clear();
            RefreshUndoRedoUi();

            foreach (var entry in manifest.Entries.OrderBy(x => x.Index))
                _project.Entries.Add(entry);

            RefreshVisibleEntries();

            AppSettingsStore.Save(UpdateSettings(settings =>
            {
                settings.LastOpenedMcfPath = sourceFile;
            }));

            CurrentFileLabel.Text = $"File: {Path.GetFileName(sourceFile)}";
            CurrentWorkLabel.Text = $"Workdir: {manifest.WorkingDirectory}";
            PreviewHintText.Text = $"Loaded {manifest.ImageCount} image(s).";
            UpdateWindowTitle();
            RefreshUndoRedoUi();

            if (manifest.Warnings.Count > 0)
                HideProgress($"Loaded with {manifest.Warnings.Count} warning(s).");
            else
                HideProgress($"Loaded {manifest.ImageCount} image(s).");

            SelectFirstImageNode();

            if (settings.OpenWorkingFolderAfterExtraction && Directory.Exists(_project.WorkingDirectory))
                Process.Start(new ProcessStartInfo { FileName = _project.WorkingDirectory, UseShellExecute = true });
        }
        catch (Exception ex)
        {
            AppMessageBox.Show(
                this,
                ex.Message,
                "Extraction failed",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            HideProgress("Extraction failed.");
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async void OpenMcf_Click(object sender, RoutedEventArgs e)
    {
        if (_isBusy)
            return;

        await OpenMcfAsync();
    }

    private async void ExtractAll_Click(object sender, RoutedEventArgs e)
    {
        if (_isBusy)
            return;
        
        if (string.IsNullOrWhiteSpace(_project.SourceFilePath))
        {
            await OpenMcfAsync();
            return;
        }

        await OpenMcfInternalAsync(_project.SourceFilePath);
    }

    private async void RebuildMcf_Click(object sender, RoutedEventArgs e)
    {
        if (_isBusy)
            return;

        if (string.IsNullOrWhiteSpace(_project.SourceFilePath) || string.IsNullOrWhiteSpace(_project.WorkingDirectory))
        {
            AppMessageBox.Show(
                this,
                "Open and extract an MCF file first.",
                "McfEditor",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        var dialog = new SaveFileDialog
        {
            Title = "Save rebuilt MCF",
            Filter = "MCF files|*.mcf|All files|*.*",
            FileName = Path.GetFileNameWithoutExtension(_project.SourceFilePath) + "_rebuilt.mcf"
        };

        if (!string.IsNullOrWhiteSpace(AppSettingsStore.Current.DefaultOutputFolder) &&
            Directory.Exists(AppSettingsStore.Current.DefaultOutputFolder))
        {
            dialog.InitialDirectory = AppSettingsStore.Current.DefaultOutputFolder;
        }
        else
        {
            dialog.InitialDirectory = Path.GetDirectoryName(_project.SourceFilePath);
        }

        if (dialog.ShowDialog(this) != true)
            return;

        try
        {
            SetBusy(true, "Preparing rebuild...");
            ShowProgress("Preparing rebuild...", 0);
            PrepareReplacementFiles();

            var progress = new Progress<ProgressInfo>(p =>
            {
                ShowProgress(p.Message, p.Percent);
            });
            
            var report = await _compressionService.RebuildAsync(
            _project.SourceFilePath,
            dialog.FileName,
            Path.Combine(_project.WorkingDirectory, "Unsorted"),
            progress);

            _project.OutputFilePath = report.OutputFile;
            _project.IsDirty = false;
            UpdateWindowTitle();
            SetStatus($"Rebuild finished: {Path.GetFileName(report.OutputFile)}");

            AppMessageBox.Show(
                this,
                $"Rebuild completed successfully.{Environment.NewLine}{Environment.NewLine}{report.OutputFile}",
                "McfEditor",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            AppMessageBox.Show(
                this,
                ex.Message,
                "Rebuild failed",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            SetStatus("Rebuild failed.");
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void ReplaceImage_Click(object sender, RoutedEventArgs e)
    {
        if (_isBusy)
            return;

        if (ImagesTreeView.SelectedItem is not ExplorerNode node || node.IsFolder || node.Entry is null)
            return;

        var entry = node.Entry;

        var dialog = new OpenFileDialog
        {
            Title = $"Replace image #{entry.Index}",
            Filter = "PNG files|*.png|All files|*.*",
            CheckFileExists = true
        };

        if (dialog.ShowDialog(this) != true)
            return;

        var action = new ImageReplacementAction(
            entry,
            entry.ReplacementPath,
            entry.IsModified,
            dialog.FileName,
            true);

        _undoRedoManager.Execute(action);

        _selectedEntry = entry;
        UpdateSelectionUi(entry);
        RefreshPreview(entry);
        SelectNodeForEntry(entry);

        RefreshDirtyState();
        RefreshUndoRedoUi();
        SetStatus($"Replacement selected for image #{entry.Index}");
    }

    private void RestoreImage_Click(object sender, RoutedEventArgs e)
    {
        if (_isBusy)
            return;

        if (ImagesTreeView.SelectedItem is not ExplorerNode node || node.IsFolder || node.Entry is null)
            return;

        var entry = node.Entry;

        if (string.IsNullOrWhiteSpace(entry.ReplacementPath) && !entry.IsModified)
            return;

        var action = new ImageReplacementAction(
            entry,
            entry.ReplacementPath,
            entry.IsModified,
            null,
            false);

        _undoRedoManager.Execute(action);

        _selectedEntry = entry;
        UpdateSelectionUi(entry);
        RefreshPreview(entry);
        SelectNodeForEntry(entry);

        RefreshDirtyState();
        RefreshUndoRedoUi();
        SetStatus($"Image #{entry.Index} restored to original");
    }

    private void OpenWorkingFolder_Click(object sender, RoutedEventArgs e)
    {
        if (_isBusy)
            return;

        if (string.IsNullOrWhiteSpace(_project.WorkingDirectory) || !Directory.Exists(_project.WorkingDirectory))
        {
            AppMessageBox.Show(
                this,
                "No working folder is available yet.",
                "McfEditor",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = _project.WorkingDirectory,
            UseShellExecute = true
        });
    }

    private void Exit_Click(object sender, RoutedEventArgs e)
    {
        if (_isBusy)
            return;
        
        Close();
    }

    private void Settings_Click(object sender, RoutedEventArgs e)
    {
        if (_isBusy)
            return;
        
        var window = new SettingsWindow(AppSettingsStore.Current)
        {
            Owner = this
        };

        if (window.ShowDialog() == true)
            AppSettingsStore.Save(window.ResultSettings);
    }

    private void About_Click(object sender, RoutedEventArgs e)
    {
        if (_isBusy)
            return;

        new AboutWindow
        {
            Owner = this
        }.ShowDialog();
    }

    private void PrepareReplacementFiles()
    {
        var unsortedDir = Path.Combine(_project.WorkingDirectory, "Unsorted");
        Directory.CreateDirectory(unsortedDir);

        foreach (var entry in _project.Entries)
        {
            if (string.IsNullOrWhiteSpace(entry.ReplacementPath))
                continue;

            var destination = Path.Combine(unsortedDir, $"img_{entry.Index}.png");
            File.Copy(entry.ReplacementPath!, destination, overwrite: true);
        }
    }
}
