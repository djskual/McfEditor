using McfEditor.IO;
using McfEditor.Models;
using McfEditor.Settings;
using McfEditor.UI.Dialogs;
using McfEditor.UI.Interop;
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

        var validationError = ValidateReplacementImage(entry, dialog.FileName);
        if (!string.IsNullOrWhiteSpace(validationError))
        {
            AppMessageBox.Show(
                this,
                validationError,
                "Invalid replacement image",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

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

    private string? ValidateReplacementImage(McfImageEntry entry, string replacementPath)
    {
        if (string.IsNullOrWhiteSpace(replacementPath))
            return "No replacement file was selected.";

        if (!File.Exists(replacementPath))
            return $"Replacement file not found:\n{replacementPath}";

        PngCodec.PngInfo originalInfo;
        PngCodec.PngInfo replacementInfo;

        try
        {
            originalInfo = PngCodec.ReadPngInfo(entry.ExtractedPath);
        }
        catch (Exception ex)
        {
            return $"Failed to read original image metadata.\n\n{ex.Message}";
        }

        try
        {
            replacementInfo = PngCodec.ReadPngInfo(replacementPath);
        }
        catch (Exception ex)
        {
            return $"Failed to read replacement image metadata.\n\n{ex.Message}";
        }

        if (replacementInfo.Width != originalInfo.Width || replacementInfo.Height != originalInfo.Height)
        {
            return
                $"Invalid image size.\n\n" +
                $"Expected: {originalInfo.Width}x{originalInfo.Height}\n" +
                $"Found: {replacementInfo.Width}x{replacementInfo.Height}";
        }

        var expectedBitsPerPixel = entry.ImageMode switch
        {
            "L" => 8,
            "RGBA" => 32,
            _ => -1
        };

        if (expectedBitsPerPixel > 0 && replacementInfo.BitsPerPixel != expectedBitsPerPixel)
        {
            return
                $"Invalid color depth.\n\n" +
                $"Expected: {expectedBitsPerPixel} bpp ({entry.ImageMode})\n" +
                $"Found: {replacementInfo.BitsPerPixel} bpp ({replacementInfo.PixelFormatName})";
        }

        var expectedDpiX = Math.Round(originalInfo.DpiX);
        var expectedDpiY = Math.Round(originalInfo.DpiY);
        var foundDpiX = Math.Round(replacementInfo.DpiX);
        var foundDpiY = Math.Round(replacementInfo.DpiY);

        if (expectedDpiX != foundDpiX || expectedDpiY != foundDpiY)
        {
            return
                $"Invalid image resolution.\n\n" +
                $"Expected: {expectedDpiX:0} x {expectedDpiY:0} DPI\n" +
                $"Found: {foundDpiX:0} x {foundDpiY:0} DPI";
        }

        return null;
    }

    private void PrepareReplacementFiles()
    {
        var unsortedDir = Path.Combine(_project.WorkingDirectory, "Unsorted");
        Directory.CreateDirectory(unsortedDir);

        foreach (var entry in _project.Entries)
        {
            if (string.IsNullOrWhiteSpace(entry.ReplacementPath))
                continue;

            var validationError = ValidateReplacementImage(entry, entry.ReplacementPath!);
            if (!string.IsNullOrWhiteSpace(validationError))
            {
                throw new InvalidOperationException(
                    $"Replacement image for entry #{entry.Index} is invalid.\n\n{validationError}");
            }

            var destination = Path.Combine(unsortedDir, $"img_{entry.Index}.png");
            File.Copy(entry.ReplacementPath!, destination, overwrite: true);
        }
    }

    private async void ExtractImage_Click(object sender, RoutedEventArgs e)
    {
        if (_isBusy)
            return;

        ExplorerNode? node;

        if (sender == ExtractImageMenuItem)
            node = _contextMenuNode ?? ImagesTreeView.SelectedItem as ExplorerNode;
        else
            node = ImagesTreeView.SelectedItem as ExplorerNode;

        if (node is null || node.Entry is null || node.IsFolder)
            return;

        var targetFolder = ChooseExportFolder("Select export folder for image");
        if (string.IsNullOrWhiteSpace(targetFolder))
            return;

        var relativePath = node.Entry.LeafName;
        await ExportEntriesAsync(
            new List<(McfImageEntry, string)> { (node.Entry, relativePath) },
            targetFolder,
            "Exporting image...");

        _contextMenuNode = null;
    }

    private async void ExtractFolder_Click(object sender, RoutedEventArgs e)
    {
        if (_isBusy)
            return;

        var node = _contextMenuNode ?? ImagesTreeView.SelectedItem as ExplorerNode;
        if (node is null || !node.IsFolder)
            return;

        var targetParentFolder = ChooseExportFolder($"Select export destination for '{node.Name}'");
        if (string.IsNullOrWhiteSpace(targetParentFolder))
            return;

        // Force creation of the clicked folder itself inside the selected destination
        var exportRoot = Path.Combine(targetParentFolder, node.Name);

        var rootPath = node.FullPath.Replace('/', Path.DirectorySeparatorChar).Trim(Path.DirectorySeparatorChar);

        var items = CollectLeafNodesFromNode(node)
            .Select(leaf =>
            {
                var leafPath = leaf.FullPath.Replace('/', Path.DirectorySeparatorChar).Trim(Path.DirectorySeparatorChar);

                string relativeInsideFolder;

                if (!string.IsNullOrWhiteSpace(rootPath) &&
                    leafPath.StartsWith(rootPath + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                {
                    relativeInsideFolder = leafPath.Substring(rootPath.Length + 1);
                }
                else
                {
                    relativeInsideFolder = leaf.Name;
                }

                return (leaf.Entry!, relativeInsideFolder);
            })
            .ToList();

        await ExportEntriesAsync(items, exportRoot, $"Exporting folder '{node.Name}'...");

        _contextMenuNode = null;
    }

    private void ExtractNode_Click(object sender, RoutedEventArgs e)
    {
        if (_isBusy)
            return;

        var node = _contextMenuNode ?? ImagesTreeView.SelectedItem as ExplorerNode;
        if (node is null)
            return;

        if (node.IsFolder)
        {
            ExtractFolder_Click(sender, e);
            return;
        }

        ExtractImage_Click(sender, e);
    }

    private async void ExtractAll_Click(object sender, RoutedEventArgs e)
    {
        if (_isBusy)
            return;

        if (_project.Entries.Count == 0)
        {
            AppMessageBox.Show(
                this,
                "Open an MCF file first.",
                "McfEditor",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        var targetFolder = ChooseExportFolder("Select export folder");
        if (string.IsNullOrWhiteSpace(targetFolder))
            return;

        var mode = AskExportModeForAll();
        if (mode is null)
            return;

        var items = _project.Entries
            .OrderBy(e => e.Index)
            .Select(entry => (entry, GetEntryExportRelativePath(entry, mode.Value)))
            .ToList();

        await ExportEntriesAsync(items, targetFolder, "Exporting all images...");
    }
    private enum ExportMode
    {
        Raw,
        Structured
    }

    private string? ChooseExportFolder(string title)
    {
        var initialPath = AppSettingsStore.Current.DefaultExportFolder;

        var folder = FolderPicker.PickFolder(title);

        if (string.IsNullOrWhiteSpace(folder))
            return null;

        return folder;
    }

    private ExportMode? AskExportModeForAll()
    {
        bool hasMappedPaths = _project.Entries.Any(e => !string.IsNullOrWhiteSpace(e.MappedPath));
        if (!hasMappedPaths)
            return ExportMode.Raw;

        var result = AppMessageBox.Show(
            this,
            "Structured image paths are available from imageidmap.res.\n\n" +
            "Yes = export structured folders\n" +
            "No = export raw img_<index>.png files\n" +
            "Cancel = abort",
            "Extract all",
            MessageBoxButton.YesNoCancel,
            MessageBoxImage.Question);

        return result switch
        {
            MessageBoxResult.Yes => ExportMode.Structured,
            MessageBoxResult.No => ExportMode.Raw,
            _ => null
        };
    }

    private static string GetEntryExportRelativePath(McfImageEntry entry, ExportMode mode)
    {
        if (mode == ExportMode.Structured && !string.IsNullOrWhiteSpace(entry.MappedPath))
            return entry.MappedPath!.Replace('/', Path.DirectorySeparatorChar);

        return $"img_{entry.Index}.png";
    }

    private static IEnumerable<McfImageEntry> CollectEntriesFromNode(ExplorerNode node)
    {
        if (!node.IsFolder && node.Entry is not null)
        {
            yield return node.Entry;
            yield break;
        }

        foreach (var child in node.Children)
        {
            foreach (var entry in CollectEntriesFromNode(child))
                yield return entry;
        }
    }

    private static IEnumerable<ExplorerNode> CollectLeafNodesFromNode(ExplorerNode node)
    {
        if (!node.IsFolder && node.Entry is not null)
        {
            yield return node;
            yield break;
        }

        foreach (var child in node.Children)
        {
            foreach (var leaf in CollectLeafNodesFromNode(child))
                yield return leaf;
        }
    }

    private async Task ExportEntriesAsync(
    IReadOnlyList<(McfImageEntry Entry, string RelativePath)> items,
    string targetFolder,
    string progressLabel)
    {
        if (items.Count == 0)
            return;

        SetBusy(true, progressLabel);
        ShowProgress(progressLabel, 0);

        try
        {
            int copiedCount = 0;

            await Task.Run(() =>
            {
                Directory.CreateDirectory(targetFolder);

                for (int i = 0; i < items.Count; i++)
                {
                    var item = items[i];
                    var sourcePath = item.Entry.PreviewPath;

                    if (string.IsNullOrWhiteSpace(sourcePath))
                        throw new InvalidOperationException($"Entry #{item.Entry.Index} has no source file to export.");

                    if (!File.Exists(sourcePath))
                        throw new FileNotFoundException(
                            $"Source file not found for entry #{item.Entry.Index}.",
                            sourcePath);

                    var destinationPath = Path.Combine(targetFolder, item.RelativePath);
                    var destinationDir = Path.GetDirectoryName(destinationPath);

                    if (!string.IsNullOrWhiteSpace(destinationDir))
                        Directory.CreateDirectory(destinationDir);

                    File.Copy(sourcePath, destinationPath, true);
                    copiedCount++;

                    double percent = ((i + 1) * 100.0) / items.Count;
                    Dispatcher.Invoke(() =>
                    {
                        ShowProgress($"{progressLabel} ({i + 1}/{items.Count})", percent);
                    });
                }
            });

            if (copiedCount == 0)
                throw new InvalidOperationException("No files were exported.");

            HideProgress($"Exported {copiedCount} item(s) to '{targetFolder}'.");
        }
        catch (Exception ex)
        {
            HideProgress("Export failed.");
            AppMessageBox.Show(
                this,
                ex.Message,
                "Export failed",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        finally
        {
            SetBusy(false);
        }
    }
}
