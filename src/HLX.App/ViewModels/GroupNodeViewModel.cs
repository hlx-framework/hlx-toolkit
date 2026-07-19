using System.Collections;
using System.Collections.Specialized;

namespace HLX.App.ViewModels;

public sealed class GroupNodeViewModel : TreeNodeViewModel
{
    private readonly string _header;
    private readonly IReadOnlyList<TreeNodeViewModel> _children;

    public GroupNodeViewModel(string baseName, IReadOnlyList<TreeNodeViewModel> children)
    {
        _header = $"{baseName} ({children.Count})";
        _children = children;
    }

    public GroupNodeViewModel(string baseName, int count, Func<int, TreeNodeViewModel> factory)
    {
        _header = $"{baseName} ({count})";
        _children = new LazyList(count, factory);
    }

    public override string Header => _header;
    public override IReadOnlyList<TreeNodeViewModel> Children => _children;
}

internal sealed class LazyList :
    IReadOnlyList<TreeNodeViewModel>,
    IList<TreeNodeViewModel>,
    IList,
    INotifyCollectionChanged
{
    private readonly Func<int, TreeNodeViewModel> _create;
    private readonly TreeNodeViewModel?[] _cache;

    public LazyList(int count, Func<int, TreeNodeViewModel> create)
    {
        _create = create;
        _cache  = new TreeNodeViewModel?[count];
    }

    public int Count => _cache.Length;

    public TreeNodeViewModel this[int index]
    {
        get => _cache[index] ??= _create(index);
        set => throw new NotSupportedException();
    }

    object? IList.this[int index]
    {
        get => this[index];
        set => throw new NotSupportedException();
    }

    bool IList.IsReadOnly  => true;
    bool IList.IsFixedSize => true;
    bool ICollection<TreeNodeViewModel>.IsReadOnly => true;
    bool ICollection.IsSynchronized => false;
    object ICollection.SyncRoot     => this;

    public IEnumerator<TreeNodeViewModel> GetEnumerator()
    {
        for (int i = 0; i < _cache.Length; i++) yield return this[i];
    }
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public int  IndexOf(TreeNodeViewModel item) { for (int i = 0; i < _cache.Length; i++) if (_cache[i] == item) return i; return -1; }
    public bool Contains(TreeNodeViewModel item) => IndexOf(item) >= 0;
    public void CopyTo(TreeNodeViewModel[] array, int index) { for (int i = 0; i < Count; i++) array[index + i] = this[i]; }

    int  IList.IndexOf(object? v)  => v is TreeNodeViewModel t ? IndexOf(t) : -1;
    bool IList.Contains(object? v) => v is TreeNodeViewModel t && Contains(t);
    void ICollection.CopyTo(Array array, int index) { for (int i = 0; i < Count; i++) array.SetValue(this[i], index + i); }

    int  IList.Add(object? v)                              => throw new NotSupportedException();
    void IList.Clear()                                     => throw new NotSupportedException();
    void IList.Insert(int i, object? v)                    => throw new NotSupportedException();
    void IList.Remove(object? v)                           => throw new NotSupportedException();
    void IList.RemoveAt(int i)                             => throw new NotSupportedException();
    void IList<TreeNodeViewModel>.Insert(int i, TreeNodeViewModel v)  => throw new NotSupportedException();
    void IList<TreeNodeViewModel>.RemoveAt(int i)                     => throw new NotSupportedException();
    void ICollection<TreeNodeViewModel>.Add(TreeNodeViewModel item)   => throw new NotSupportedException();
    void ICollection<TreeNodeViewModel>.Clear()                       => throw new NotSupportedException();
    bool ICollection<TreeNodeViewModel>.Remove(TreeNodeViewModel item) => throw new NotSupportedException();

    // Avalonia's ItemsSourceView requires this interface to treat the list as live, even though it never fires.
    public event NotifyCollectionChangedEventHandler? CollectionChanged { add { } remove { } }
}
