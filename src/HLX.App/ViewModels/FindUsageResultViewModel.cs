namespace HLX.App.ViewModels;

public sealed class FindUsageResultViewModel
{
    public string FunctionName { get; }
    public int FunctionFIndex { get; }
    public int InstructionOffset { get; }
    public string Description { get; }
    public IRelayCommand NavigateCommand { get; }

    public FindUsageResultViewModel(string funcName, int funcFIndex, int instrOffset, Action navigate)
    {
        FunctionName = funcName;
        FunctionFIndex = funcFIndex;
        InstructionOffset = instrOffset;
        Description = $"{funcName}  @{instrOffset}";
        NavigateCommand = new RelayCommand(navigate);
    }
}
