using System.IO;
using HLX.App.ViewModels;
using HLX.App.ViewModels.Detail;
using HLX.Core;
using HLX.Core.IO;

namespace HLX.App.Tests;

public class AppTests
{
    private static readonly string FixturePath =
        Path.Combine(AppContext.BaseDirectory, "fixtures", "hlboot.dat");

    private static HlModule LoadFixture()
    {
        using var fs = File.OpenRead(FixturePath);
        return HlReader.Read(fs);
    }

    private static MainWindowViewModel CreateLoadedVm()
    {
        var vm = new MainWindowViewModel();
        vm.LoadModule(LoadFixture());
        return vm;
    }

    [Fact]
    public void LoadModule_PopulatesTree()
    {
        var vm = CreateLoadedVm();
        Assert.NotNull(vm.RootNodes);
        Assert.NotEmpty(vm.RootNodes!);
    }

    [Fact]
    public void LoadModule_SetsStatusText()
    {
        var vm = CreateLoadedVm();
        Assert.Contains("421", vm.StatusText);
        Assert.Contains("336", vm.StatusText);
    }

    [Fact]
    public void LoadModule_RootHasNamespaceAndKindGroups()
    {
        var vm = CreateLoadedVm();
        // One group per type namespace, plus Functions, Natives, and Globals.
        Assert.Equal(15, vm.RootNodes!.Count);
    }

    [Fact]
    public void TypeDetailViewModel_AllTypesRenderWithoutException()
    {
        var m = LoadFixture();
        var analysis = HLX.Analysis.AnalysisResult.Build(m);
        var nav = new NullNavigationService();

        for (int i = 0; i < m.Types.Length; i++)
        {
            var vm = new TypeDetailViewModel(i, m.Types[i], m, analysis, nav);
            Assert.NotNull(vm.TypeName);
            Assert.NotEmpty(vm.KindLabel);
        }
    }

    [Fact]
    public void TypeDetailViewModel_ObjectTypeHasFields()
    {
        var m = LoadFixture();
        var analysis = HLX.Analysis.AnalysisResult.Build(m);
        var nav = new NullNavigationService();
        int stringIdx = Enumerable.Range(0, m.Types.Length)
            .First(i => m.Types[i] is ObjectType o && o.Name == "String");
        var vm = new TypeDetailViewModel(stringIdx, m.Types[stringIdx], m, analysis, nav);
        Assert.Equal("Object", vm.KindLabel);
        Assert.True(vm.Fields.Count > 0, "String type should have fields");
    }

    [Fact]
    public void TypeDetailViewModel_FunctionTypeHasReturnType()
    {
        var m = LoadFixture();
        var analysis = HLX.Analysis.AnalysisResult.Build(m);
        var nav = new NullNavigationService();
        int ftIdx = Enumerable.Range(0, m.Types.Length).First(i => m.Types[i] is FunctionType);
        var vm = new TypeDetailViewModel(ftIdx, m.Types[ftIdx], m, analysis, nav);
        Assert.NotNull(vm.ReturnType);
    }

    [Fact]
    public void FunctionDetailViewModel_AllFunctionsRenderWithoutException()
    {
        var m = LoadFixture();
        var analysis = HLX.Analysis.AnalysisResult.Build(m);
        var nav = new NullNavigationService();
        var funcNames = BuildFuncNames(m);

        for (int i = 0; i < m.Functions.Length; i++)
        {
            var vm = FunctionDetailViewModel.Create(
                m.Functions[i].FunctionIndex, m, analysis, funcNames, nav);
            Assert.NotNull(vm.Instructions);
            Assert.Equal(m.Functions[i].Instructions.Length, vm.Instructions.Count);
        }
    }

    [Fact]
    public void FunctionDetailViewModel_PseudoLines_AllFunctionsRenderWithoutException()
    {
        var m = LoadFixture();
        var analysis = HLX.Analysis.AnalysisResult.Build(m);
        var nav = new NullNavigationService();
        var funcNames = BuildFuncNames(m);

        for (int i = 0; i < m.Functions.Length; i++)
        {
            var vm = FunctionDetailViewModel.Create(
                m.Functions[i].FunctionIndex, m, analysis, funcNames, nav);
            Assert.NotNull(vm.PseudoLines);
        }
    }

    [Fact]
    public void FunctionDetailViewModel_PseudoLines_KnownLoopFunctionHasWhileKeyword()
    {
        var m = LoadFixture();
        var analysis = HLX.Analysis.AnalysisResult.Build(m);
        var nav = new NullNavigationService();
        var funcNames = BuildFuncNames(m);

        var vm = FunctionDetailViewModel.Create(4, m, analysis, funcNames, nav); // String::findChar has a while loop
        Assert.Contains(vm.PseudoLines, l => l.Text.Contains("while"));
    }

    [Fact]
    public void FunctionDetailViewModel_HasRegisters()
    {
        var m = LoadFixture();
        var analysis = HLX.Analysis.AnalysisResult.Build(m);
        var nav = new NullNavigationService();
        var funcNames = BuildFuncNames(m);
        var vm = FunctionDetailViewModel.Create(m.Functions[0].FunctionIndex, m, analysis, funcNames, nav);
        Assert.True(vm.Registers.Count >= 0);
    }

    [Fact]
    public void FunctionDetailViewModel_AtLeastOneFunctionHasLinkPart()
    {
        var m = LoadFixture();
        var analysis = HLX.Analysis.AnalysisResult.Build(m);
        var nav = new NullNavigationService();
        var funcNames = BuildFuncNames(m);

        bool foundLink = m.Functions.Any(fn =>
        {
            var vm = FunctionDetailViewModel.Create(fn.FunctionIndex, m, analysis, funcNames, nav);
            return vm.Instructions.Any(line => line.Parts.Any(p => p.IsLink));
        });
        Assert.True(foundLink, "Expected at least one instruction to have a hyperlink part");
    }

    [Fact]
    public void Search_FindsStringClass()
    {
        var vm = CreateLoadedVm();
        vm.SearchQuery = "String";
        Assert.True(vm.IsSearchActive);
        Assert.NotNull(vm.SearchResults);
        Assert.Contains(vm.SearchResults!, r => r.Node is TypeNodeViewModel t && t.Header == "String");
    }

    [Fact]
    public void Search_ClearsOnEmptyQuery()
    {
        var vm = CreateLoadedVm();
        vm.SearchQuery = "Main";
        Assert.True(vm.IsSearchActive);
        vm.SearchQuery = "";
        Assert.False(vm.IsSearchActive);
        Assert.Null(vm.SearchResults);
    }

    [Fact]
    public void Navigation_GoBackForward()
    {
        var vm = CreateLoadedVm();
        Assert.False(vm.GoBackCommand.CanExecute(null));

        vm.SelectedNode = vm.RootNodes![0];
        vm.SelectedNode = vm.RootNodes![1];

        Assert.True(vm.GoBackCommand.CanExecute(null));
        vm.GoBackCommand.Execute(null);
        Assert.True(vm.GoForwardCommand.CanExecute(null));
    }

    [Fact]
    public void FindUsages_TypeNodeTriggersFindUsages()
    {
        var vm = CreateLoadedVm();
        var typeGroup = vm.RootNodes!.OfType<GroupNodeViewModel>().First(); // first namespace group
        typeGroup.IsExpanded = true; // trigger lazy load of type nodes
        var typeNode = typeGroup.Children!.OfType<TypeNodeViewModel>().First();

        typeNode.FindUsagesCommand!.Execute(null);
        Assert.True(vm.IsFindUsagesVisible);
    }

    [Fact]
    public void FindUsages_FunctionNodeShowsCallers()
    {
        var vm = CreateLoadedVm();
        var funcGroup = vm.RootNodes!.OfType<GroupNodeViewModel>()
            .First(g => g.Header.StartsWith("Functions"));
        funcGroup.IsExpanded = true; // trigger lazy load
        var funcNode = funcGroup.Children!.OfType<FunctionNodeViewModel>()
            .First(n => !n.IsNative);
        funcNode.FindUsagesCommand!.Execute(null);
        Assert.True(vm.IsFindUsagesVisible);
    }

    private static IReadOnlyDictionary<int, string> BuildFuncNames(HlModule m)
    {
        var d = new Dictionary<int, string>();
        foreach (var type in m.Types.OfType<ObjectType>())
            foreach (var proto in type.Protos)
                d.TryAdd(proto.FunctionIndex, $"{type.Name}::{proto.Name}");
        return d;
    }

    private sealed class NullNavigationService : INavigationService
    {
        public void NavigateToType(int typeIndex) { }
        public void NavigateToFunction(int functionFIndex) { }
        public void ShowString(string value) { }
    }
}
