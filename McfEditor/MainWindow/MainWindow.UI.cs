using McfEditor.Models;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;

namespace McfEditor;

public partial class MainWindow
{
    private McfImageEntry? _selectedEntry; 

    private void ImagesTreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (e.NewValue is ExplorerNode node && !node.IsFolder && node.Entry is not null)
        {
            _selectedEntry = node.Entry;
            UpdateSelectionUi(_selectedEntry);
            RefreshPreview(_selectedEntry);
            return;
        }

        _selectedEntry = null;
        ResetSelectionUi();
        PreviewImage.Source = null;
        PreviewTitleText.Text = "Preview";
        PreviewHintText.Text = "No image selected.";
    }

    private void UpdateSelectionUi(McfImageEntry entry)
    {
        SelectedIndexText.Text = $"Index: {entry.Index}";
        SelectedStatusText.Text = $"Status: {entry.StatusText}";
        SelectedDimensionsText.Text = $"Dimensions: {entry.Width} x {entry.Height}";
        SelectedModeText.Text = $"Mode: {entry.ImageMode}";
        SelectedOffsetText.Text = $"Offset: 0x{entry.Offset:X}";
        SelectedSizeText.Text = $"Compressed size: {entry.CompressedSize} bytes";
        SelectedPathText.Text = $"Path: {entry.PreviewPath}";
        PreviewTitleText.Text = entry.DisplayName;
        PreviewHintText.Text = entry.IsModified
            ? "Showing replacement image."
            : "Showing extracted image.";
    }

    private void ResetSelectionUi()
    {
        SelectedIndexText.Text = "Index: -";
        SelectedStatusText.Text = "Status: -";
        SelectedDimensionsText.Text = "Dimensions: -";
        SelectedModeText.Text = "Mode: -";
        SelectedOffsetText.Text = "Offset: -";
        SelectedSizeText.Text = "Compressed size: -";
        SelectedPathText.Text = "Path: -";
        PreviewTitleText.Text = "Preview";
        PreviewHintText.Text = "No image selected.";
    }

    private void RefreshPreview(McfImageEntry entry)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(entry.PreviewPath) || !File.Exists(entry.PreviewPath))
            {
                PreviewImage.Source = null;
                PreviewHintText.Text = "Preview file not found.";
                return;
            }

            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.UriSource = new Uri(entry.PreviewPath);
            bitmap.EndInit();
            bitmap.Freeze();

            PreviewImage.Source = bitmap;
        }
        catch (Exception ex)
        {
            PreviewImage.Source = null;
            PreviewHintText.Text = $"Preview error: {ex.Message}";
        }
    }

    private void RefreshSelectedEntryUi()
    {
        if (_selectedEntry is null)
        {
            ResetSelectionUi();
            PreviewImage.Source = null;
            return;
        }

        UpdateSelectionUi(_selectedEntry);
        RefreshPreview(_selectedEntry);

        RefreshVisibleEntries();
    }

    private void SelectFirstImageNode()
    {
        var firstLeaf = FindFirstLeaf(_explorerNodes);
        if (firstLeaf != null)
        {
            firstLeaf.IsSelected = true;
            _selectedEntry = firstLeaf.Entry;
            UpdateSelectionUi(_selectedEntry!);
            RefreshPreview(_selectedEntry!);
        }
    }

    private ExplorerNode? FindNodeForEntry(IEnumerable<ExplorerNode> nodes, McfImageEntry entry)
    {
        foreach (var node in nodes)
        {
            if (node.Entry == entry)
                return node;

            var child = FindNodeForEntry(node.Children, entry);
            if (child is not null)
                return child;
        }

        return null;
    }

    private void SelectNodeForEntry(McfImageEntry entry)
    {
        var node = FindNodeForEntry(_explorerNodes, entry);
        if (node is null)
            return;

        ExpandParentsForNode(_explorerNodes, entry);
        node.IsSelected = true;
    }

    private bool ExpandParentsForNode(IEnumerable<ExplorerNode> nodes, McfImageEntry entry)
    {
        foreach (var node in nodes)
        {
            if (node.Entry == entry)
                return true;

            if (ExpandParentsForNode(node.Children, entry))
            {
                node.IsExpanded = true;
                return true;
            }
        }

        return false;
    }
}
