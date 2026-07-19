namespace HLX.App.ViewModels.Detail;

public sealed class BindingItem
{
    public string FieldName { get; }
    public string FunctionName { get; }
    public int FunctionFIndex { get; }
    public IRelayCommand NavigateCommand { get; }

    public BindingItem(HlBinding binding, ObjectType owner, HlModule module, IReadOnlyDictionary<int, string> funcNames, Action<int> navigate)
    {
        FunctionFIndex = binding.FunctionIndex;
        FieldName = (uint)binding.FieldIndex < (uint)owner.Fields.Length
            ? owner.Fields[binding.FieldIndex].Name : $"field#{binding.FieldIndex}";
        FunctionName = funcNames.TryGetValue(binding.FunctionIndex, out var n) ? n : $"fn#{binding.FunctionIndex}";
        NavigateCommand = new RelayCommand(() => navigate(binding.FunctionIndex));
    }
}
