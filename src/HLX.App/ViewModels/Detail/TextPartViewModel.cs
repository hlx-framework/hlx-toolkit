namespace HLX.App.ViewModels.Detail;

public sealed class TextPartViewModel(string text) : InstructionPartViewModel
{
    public override string Text => text;
}
