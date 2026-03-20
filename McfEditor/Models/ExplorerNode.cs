using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace McfEditor.Models;

public sealed class ExplorerNode : INotifyPropertyChanged
{
    private string _name = string.Empty;
    private string _fullPath = string.Empty;
    private bool _isFolder;
    private bool _isExpanded;
    private bool _isSelected;
    private McfImageEntry? _entry;

    public string Name
    {
        get => _name;
        set
        {
            if (_name == value)
                return;
            _name = value;
            OnPropertyChanged();
        }
    }

    public string FullPath
    {
        get => _fullPath;
        set
        {
            if (_fullPath == value)
                return;
            _fullPath = value;
            OnPropertyChanged();
        }
    }

    public bool IsFolder
    {
        get => _isFolder;
        set
        {
            if (_isFolder == value)
                return;
            _isFolder = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsLeaf));
        }
    }

    public bool IsLeaf => !IsFolder && Entry is not null;

    public McfImageEntry? Entry
    {
        get => _entry;
        set
        {
            if (_entry == value)
                return;
            _entry = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsLeaf));
        }
    }

    public ObservableCollection<ExplorerNode> Children { get; } = new();

    public bool IsExpanded
    {
        get => _isExpanded;
        set
        {
            if (_isExpanded == value)
                return;
            _isExpanded = value;
            OnPropertyChanged();
        }
    }

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected == value)
                return;
            _isSelected = value;
            OnPropertyChanged();
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
