using System.IO;
using HLX.App.ViewModels.Detail;
using HLX.Core.IO;

namespace HLX.App.ViewModels;

public partial class MainWindowViewModel : ObservableObject, INavigationService
{
    private readonly Func<Task<string?>>? _pickFile;

    public MainWindowViewModel(Func<Task<string?>>? pickFile = null)
    {
        _pickFile = pickFile;
    }

    [ObservableProperty] private string _title = "HLX — HashLink Explorer";
    [ObservableProperty] private string _statusText = "Open a .dat file to begin  (File → Open…)";
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(IsStatusBarStringVisible))]
    private string _statusBarString = "";

    public bool IsStatusBarStringVisible => !string.IsNullOrEmpty(StatusBarString);
    [ObservableProperty] private IReadOnlyList<TreeNodeViewModel>? _rootNodes;
    [ObservableProperty] private TreeNodeViewModel? _selectedNode;
    [ObservableProperty] private object? _currentDetail;
    [ObservableProperty] private string _searchQuery = "";
    [ObservableProperty] private IReadOnlyList<SearchResultViewModel>? _searchResults;
    [ObservableProperty] private bool _isSearchActive;
    [ObservableProperty] private IReadOnlyList<FindUsageResultViewModel>? _findUsagesResults;
    [ObservableProperty] private string _findUsagesHeader = "";
    [ObservableProperty] private bool _isFindUsagesVisible;

    private HlModule? _module;
    private AnalysisResult? _analysis;
    private TypeNameResolver? _resolver;

    private readonly Dictionary<int, TypeNodeViewModel>     _typeNodes     = new();
    private readonly Dictionary<int, FunctionNodeViewModel> _functionNodes = new();
    private readonly Dictionary<int, string>                _funcNames     = new();
    private readonly Dictionary<int, HlFunction>            _functionByFIndex = new();
    private readonly Dictionary<int, GroupNodeViewModel>    _typeIndexToGroup = new();
    private readonly Dictionary<int, int>                   _protoFIndexToTypeIndex = new();

    private GroupNodeViewModel? _functionsRoot;
    private GroupNodeViewModel? _nativesRoot;

    private readonly List<TreeNodeViewModel> _history = new();
    private int _historyIndex = -1;
    private bool _navigating;

    [RelayCommand]
    private async Task OpenFileAsync()
    {
        if (_pickFile == null) return;
        string? path = await _pickFile();
        if (path == null) return;
        StatusText = "Loading…";
        try
        {
            var (module, analysis) = await Task.Run(() =>
            {
                using var fs = File.OpenRead(path);
                var m = HlReader.Read(fs);
                return (m, AnalysisResult.Build(m));
            });
            LoadModule(module, analysis);
            Title = $"HLX — {Path.GetFileName(path)}";
        }
        catch (Exception ex)
        {
            StatusText = $"Error: {ex.Message}";
        }
    }

    public void LoadModule(HlModule module, AnalysisResult? analysis = null)
    {
        _module = module;
        _analysis = analysis ?? AnalysisResult.Build(module);
        _resolver = _analysis.TypeNames;

        _typeNodes.Clear();
        _functionNodes.Clear();
        _funcNames.Clear();
        _functionByFIndex.Clear();
        _typeIndexToGroup.Clear();
        _protoFIndexToTypeIndex.Clear();
        _history.Clear();
        _historyIndex = -1;
        SelectedNode = null;
        CurrentDetail = null;
        FindUsagesResults = null;
        IsFindUsagesVisible = false;

        BuildFunctionNames(module);
        BuildTree(module, _analysis);
        StatusText = $"Types: {module.Types.Length}   Functions: {module.Functions.Length}" +
                     $"   Natives: {module.Natives.Length}   Globals: {module.Globals.Length}";
    }

    private void BuildFunctionNames(HlModule module)
    {
        foreach (var fn in module.Functions)
            _functionByFIndex[fn.FunctionIndex] = fn;
        for (int i = 0; i < module.Types.Length; i++)
        {
            if (module.Types[i] is not ObjectType type) continue;
            foreach (var proto in type.Protos)
            {
                _funcNames.TryAdd(proto.FunctionIndex, $"{type.Name}::{proto.Name}");
                _protoFIndexToTypeIndex.TryAdd(proto.FunctionIndex, i);
            }
        }
    }

    private void BuildTree(HlModule module, AnalysisResult analysis)
    {
        var resolver = _resolver!;

        var byNamespace = new SortedDictionary<string, List<int>>(StringComparer.Ordinal);
        for (int i = 0; i < module.Types.Length; i++)
        {
            if (module.Types[i] is not (ObjectType or EnumType or AbstractType)) continue;
            string fullName = resolver.Resolve(i);
            int lastDot = fullName.LastIndexOf('.');
            string ns = lastDot >= 0 ? fullName[..lastDot] : "";
            if (!byNamespace.TryGetValue(ns, out var lst)) byNamespace[ns] = lst = [];
            lst.Add(i);
        }

        var nsRoots = new List<TreeNodeViewModel>(byNamespace.Count);
        foreach (var (ns, typeIndices) in byNamespace)
        {
            var captured = typeIndices;
            var nsGroup = new GroupNodeViewModel(
                string.IsNullOrEmpty(ns) ? "-" : ns,
                captured.Count,
                i => GetOrCreateTypeNode(captured[i]));
            foreach (int idx in captured) _typeIndexToGroup[idx] = nsGroup;
            nsRoots.Add(nsGroup);
        }

        var freeFns = module.Functions
            .Where(fn => !_protoFIndexToTypeIndex.ContainsKey(fn.FunctionIndex))
            .ToArray();
        _functionsRoot = new GroupNodeViewModel("Functions", freeFns.Length,
            i => GetOrCreateFunctionNode(freeFns[i]));

        var nativesByLib = module.Natives
            .GroupBy(n => n.Lib, StringComparer.Ordinal)
            .OrderBy(g => g.Key, StringComparer.Ordinal)
            .ToArray();
        var libNodes = nativesByLib.Select(g =>
        {
            var nats = g.ToArray();
            return (TreeNodeViewModel)new GroupNodeViewModel(g.Key, nats.Length, j =>
            {
                var n = nats[j];
                if (!_functionNodes.TryGetValue(n.FunctionIndex, out var node))
                {
                    node = new FunctionNodeViewModel(n.FunctionIndex, n.Name, isNative: true, OnFindUsagesForFunction);
                    _functionNodes[n.FunctionIndex] = node;
                }
                return (TreeNodeViewModel)node;
            });
        }).ToList();
        _nativesRoot = new GroupNodeViewModel("Natives", libNodes);

        var globs = module.Globals;
        var globalsGroup = new GroupNodeViewModel("Globals", globs.Length, i =>
        {
            int ti = globs[i];
            string tn = (uint)ti < (uint)module.Types.Length ? resolver.Resolve(ti) : "?";
            return new GlobalNodeViewModel(i, ti, tn);
        });

        RootNodes = [..nsRoots, _functionsRoot, _nativesRoot, globalsGroup];
        RefreshCanExecute();
    }

    private TypeNodeViewModel GetOrCreateTypeNode(int typeIndex)
    {
        if (!_typeNodes.TryGetValue(typeIndex, out var node))
        {
            var type = _module!.Types[typeIndex];
            string fullName = _resolver!.Resolve(typeIndex);
            int lastDot = fullName.LastIndexOf('.');
            string simpleName = lastDot >= 0 ? fullName[(lastDot + 1)..] : fullName;

            IReadOnlyList<TreeNodeViewModel>? children = null;
            if (type is ObjectType obj && obj.Protos.Length > 0)
            {
                var protoNodes = new List<TreeNodeViewModel>(obj.Protos.Length);
                foreach (var proto in obj.Protos)
                {
                    if (!_functionNodes.TryGetValue(proto.FunctionIndex, out var fn))
                    {
                        bool isNativeProto = !_functionByFIndex.ContainsKey(proto.FunctionIndex);
                        fn = new FunctionNodeViewModel(proto.FunctionIndex, proto.Name, isNativeProto, OnFindUsagesForFunction);
                        _functionNodes[proto.FunctionIndex] = fn;
                    }
                    protoNodes.Add(fn);
                }
                children = protoNodes;
            }

            node = new TypeNodeViewModel(typeIndex, type, simpleName, OnFindUsagesForType, children);
            _typeNodes[typeIndex] = node;
        }
        return node;
    }

    private FunctionNodeViewModel GetOrCreateFunctionNode(HlFunction fn)
    {
        if (!_functionNodes.TryGetValue(fn.FunctionIndex, out var node))
        {
            string name;
            if (_protoFIndexToTypeIndex.ContainsKey(fn.FunctionIndex) &&
                _funcNames.TryGetValue(fn.FunctionIndex, out var full))
            {
                int sep = full.IndexOf("::", StringComparison.Ordinal);
                name = sep >= 0 ? full[(sep + 2)..] : full;
            }
            else
            {
                name = _funcNames.TryGetValue(fn.FunctionIndex, out var n) ? n : $"fn#{fn.FunctionIndex}";
            }
            node = new FunctionNodeViewModel(fn.FunctionIndex, name, isNative: false, OnFindUsagesForFunction);
            _functionNodes[fn.FunctionIndex] = node;
        }
        return node;
    }

    partial void OnSelectedNodeChanged(TreeNodeViewModel? value)
    {
        if (_navigating || value == null || _module == null) return;
        NavigateTo(value, pushHistory: true);
    }

    private object? CreateDetail(TreeNodeViewModel node)
    {
        if (_module == null || _analysis == null) return null;
        return node switch
        {
            TypeNodeViewModel t =>
                new TypeDetailViewModel(t.TypeIndex, t.Type, _module, _analysis, this),
            FunctionNodeViewModel f when !f.IsNative =>
                FunctionDetailViewModel.Create(f.FunctionFIndex, _module, _analysis, _funcNames, this),
            _ => null
        };
    }

    public void NavigateToType(int typeIndex)
    {
        if (_typeIndexToGroup.TryGetValue(typeIndex, out var nsGroup))
            nsGroup.IsExpanded = true;
        var node = GetOrCreateTypeNode(typeIndex);
        NavigateTo(node, pushHistory: true);
    }

    public void NavigateToFunction(int functionFIndex)
    {
        if (_protoFIndexToTypeIndex.TryGetValue(functionFIndex, out int typeIdx))
        {
            if (_typeIndexToGroup.TryGetValue(typeIdx, out var nsGroup))
                nsGroup.IsExpanded = true;
            GetOrCreateTypeNode(typeIdx).IsExpanded = true;
            if (_functionNodes.TryGetValue(functionFIndex, out var protoNode))
                NavigateTo(protoNode, pushHistory: true);
            return;
        }
        if (_functionNodes.TryGetValue(functionFIndex, out var existingNode) && existingNode.IsNative)
        {
            _nativesRoot!.IsExpanded = true;
            NavigateTo(existingNode, pushHistory: true);
            return;
        }
        if (!_functionByFIndex.TryGetValue(functionFIndex, out var fn)) return;
        var node = GetOrCreateFunctionNode(fn);
        _functionsRoot!.IsExpanded = true;
        NavigateTo(node, pushHistory: true);
    }

    public void ShowString(string value) => StatusBarString = value;

    private void NavigateTo(TreeNodeViewModel node, bool pushHistory)
    {
        _navigating = true;
        try
        {
            if (pushHistory)
            {
                while (_history.Count > _historyIndex + 1)
                    _history.RemoveAt(_history.Count - 1);
                if (_historyIndex < 0 || !ReferenceEquals(_history[_historyIndex], node))
                {
                    _history.Add(node);
                    _historyIndex = _history.Count - 1;
                }
            }
            SelectedNode = node;
            CurrentDetail = CreateDetail(node);
            RefreshCanExecute();
        }
        finally
        {
            _navigating = false;
        }
    }

    private void RefreshCanExecute()
    {
        GoBackCommand.NotifyCanExecuteChanged();
        GoForwardCommand.NotifyCanExecuteChanged();
    }

    private bool CanGoBack()    => _historyIndex > 0;
    private bool CanGoForward() => _historyIndex < _history.Count - 1;

    [RelayCommand(CanExecute = nameof(CanGoBack))]
    private void GoBack()
    {
        _historyIndex--;
        NavigateTo(_history[_historyIndex], pushHistory: false);
    }

    [RelayCommand(CanExecute = nameof(CanGoForward))]
    private void GoForward()
    {
        _historyIndex++;
        NavigateTo(_history[_historyIndex], pushHistory: false);
    }

    partial void OnSearchQueryChanged(string value)
    {
        IsSearchActive = !string.IsNullOrWhiteSpace(value);
        SearchResults = IsSearchActive ? PerformSearch(value.Trim()) : null;
    }

    private IReadOnlyList<SearchResultViewModel> PerformSearch(string query)
    {
        if (_module == null) return [];
        var results = new List<SearchResultViewModel>();

        for (int i = 0; i < _module.Types.Length; i++)
        {
            string name = _resolver!.Resolve(i);
            if (name.Contains(query, StringComparison.OrdinalIgnoreCase))
                results.Add(new SearchResultViewModel($"type    {name}", GetOrCreateTypeNode(i), NavigateFromSearch));
        }
        foreach (var native in _module.Natives)
        {
            string name = $"{native.Lib}.{native.Name}";
            if (name.Contains(query, StringComparison.OrdinalIgnoreCase) &&
                _functionNodes.TryGetValue(native.FunctionIndex, out var nn))
                results.Add(new SearchResultViewModel($"native  {name}", nn, NavigateFromSearch));
        }
        foreach (var fn in _module.Functions)
        {
            string name = _funcNames.TryGetValue(fn.FunctionIndex, out var n) ? n : $"fn#{fn.FunctionIndex}";
            if (name.Contains(query, StringComparison.OrdinalIgnoreCase))
                results.Add(new SearchResultViewModel($"func    {name}", GetOrCreateFunctionNode(fn), NavigateFromSearch));
        }
        for (int ti = 0; ti < _module.Types.Length; ti++)
        {
            if (_module.Types[ti] is not ObjectType obj) continue;
            foreach (var field in obj.Fields)
            {
                if (field.Name.Contains(query, StringComparison.OrdinalIgnoreCase))
                    results.Add(new SearchResultViewModel(
                        $"field   {obj.Name}.{field.Name}", GetOrCreateTypeNode(ti), NavigateFromSearch));
            }
        }

        return results.Take(200).ToList();
    }

    private void NavigateFromSearch(TreeNodeViewModel node)
    {
        if (node is TypeNodeViewModel tn && _typeIndexToGroup.TryGetValue(tn.TypeIndex, out var nsGroup))
        {
            nsGroup.IsExpanded = true;
        }
        else if (node is FunctionNodeViewModel fn)
        {
            if (fn.IsNative)
                _nativesRoot!.IsExpanded = true;
            else if (_protoFIndexToTypeIndex.TryGetValue(fn.FunctionFIndex, out int typeIdx))
            {
                if (_typeIndexToGroup.TryGetValue(typeIdx, out var typeNsGroup))
                    typeNsGroup.IsExpanded = true;
                GetOrCreateTypeNode(typeIdx).IsExpanded = true;
            }
            else
                _functionsRoot!.IsExpanded = true;
        }
        _navigating = true;
        SearchQuery = "";
        _navigating = false;
        NavigateTo(node, pushHistory: true);
    }

    private void OnFindUsagesForType(TypeNodeViewModel node)
    {
        if (_module == null || _analysis == null) return;
        var refs = _analysis.References.FunctionsReferencingType(node.TypeIndex);
        var results = new List<FindUsageResultViewModel>();

        foreach (int findex in refs)
        {
            if (!_functionByFIndex.TryGetValue(findex, out var fn)) continue;
            string funcName = _funcNames.TryGetValue(findex, out var n) ? n : $"fn#{findex}";
            foreach (var instr in fn.Instructions)
            {
                var kinds = HlOpcodeInfo.Operands(instr.Opcode);
                for (int i = 0; i < kinds.Length && i < instr.Operands.Length; i++)
                {
                    if (kinds[i] == HlOperandKind.TypeIndex && instr.Operands[i] == node.TypeIndex)
                    {
                        int capturedFindex = findex;
                        results.Add(new FindUsageResultViewModel(funcName, findex, instr.Offset,
                            () => NavigateToFunction(capturedFindex)));
                        break;
                    }
                }
            }
        }

        FindUsagesResults = results;
        FindUsagesHeader = $"Usages of  '{node.Header}'  ({results.Count})";
        IsFindUsagesVisible = true;
    }

    private void OnFindUsagesForFunction(FunctionNodeViewModel node)
    {
        if (_module == null || _analysis == null) return;
        var callerFindices = _analysis.CallGraph.Callers(node.FunctionFIndex);
        var results = new List<FindUsageResultViewModel>();

        foreach (int callerFi in callerFindices)
        {
            string callerName = _funcNames.TryGetValue(callerFi, out var n) ? n : $"fn#{callerFi}";
            int offset = 0;
            if (_functionByFIndex.TryGetValue(callerFi, out var callerFn))
            {
                offset = callerFn.Instructions
                    .FirstOrDefault(instr =>
                    {
                        var kinds = HlOpcodeInfo.Operands(instr.Opcode);
                        for (int i = 0; i < kinds.Length && i < instr.Operands.Length; i++)
                            if (kinds[i] == HlOperandKind.FunctionRef && instr.Operands[i] == node.FunctionFIndex)
                                return true;
                        return false;
                    })?.Offset ?? 0;
            }
            int capturedFi = callerFi;
            results.Add(new FindUsageResultViewModel(callerName, callerFi, offset,
                () => NavigateToFunction(capturedFi)));
        }

        FindUsagesResults = results;
        FindUsagesHeader = $"Callers of  '{node.Header}'  ({results.Count})";
        IsFindUsagesVisible = true;
    }

    [RelayCommand]
    private void CloseFindUsages() => IsFindUsagesVisible = false;
}
