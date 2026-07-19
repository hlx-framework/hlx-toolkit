namespace HLX.App.ViewModels;

public interface INavigationService
{
    void NavigateToType(int typeIndex);
    void NavigateToFunction(int functionFIndex);
    void ShowString(string value);
}
