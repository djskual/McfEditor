using McfEditor.Models;
using System.IO;
using System.Windows.Controls;
using System.Windows.Media.Imaging;

namespace McfEditor;

public partial class MainWindow
{
    private McfImageEntry? _selectedEntry; 

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        RefreshVisibleEntries();
    }

    private void ImageList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _selectedEntry = ImageList.SelectedItem as McfImageEntry;

        if (_selectedEntry is not null)
        {
            UpdateSelectionUi(_selectedEntry);
            RefreshPreview(_selectedEntry);
        }
        else
        {
            ResetSelectionUi();
            PreviewImage.Source = null;
        }
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

        ImageList.Items.Refresh();
    }
}
