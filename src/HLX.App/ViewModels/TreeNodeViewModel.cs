using System.ComponentModel;

namespace HLX.App.ViewModels;

public abstract class TreeNodeViewModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    protected void Notify(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    private bool _isExpanded;
    private bool _isSelected;

    public bool IsExpanded
    {
        get => _isExpanded;
        set { if (_isExpanded == value) return; _isExpanded = value; Notify(nameof(IsExpanded)); }
    }

    public bool IsSelected
    {
        get => _isSelected;
        set { if (_isSelected == value) return; _isSelected = value; Notify(nameof(IsSelected)); }
    }

    public abstract string Header { get; }
    public virtual IReadOnlyList<TreeNodeViewModel>? Children => null;
    public virtual IRelayCommand? FindUsagesCommand => null;
    public virtual bool CanFindUsages => false;
}
