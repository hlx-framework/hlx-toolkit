namespace HLX.App.ViewModels.Detail;

public abstract class InstructionPartViewModel
{
    public abstract string Text { get; }
    public virtual bool IsLink => false;
}
