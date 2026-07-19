using HLX.GamelibGenerator;

namespace HLX.GamelibGenerator.Tests;

// StdLibScanner.Scan reads plain .hx text via regex; these craft snippets to hit each branch.
public class StdLibScannerTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("hlx-stdlibscanner-").FullName;

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private void WriteFile(string relativePath, string content)
    {
        var full = Path.Combine(_root, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        File.WriteAllText(full, content);
    }

    [Fact]
    public void Scan_MissingRoot_ReturnsEmpty()
    {
        var result = StdLibScanner.Scan(Path.Combine(_root, "does-not-exist"));
        Assert.Empty(result);
    }

    [Fact]
    public void Scan_PrimaryType_IsNotIncludedAsASecondaryType()
    {
        WriteFile("hl/BaseType.hx", "package hl;\nclass BaseType {}\n");
        var result = StdLibScanner.Scan(_root);
        Assert.DoesNotContain("hl.BaseType", result.Keys);
    }

    [Fact]
    public void Scan_SecondaryType_IsQualifiedByDeclaringModule()
    {
        WriteFile("hl/BaseType.hx", "package hl;\nclass BaseType {}\nclass Class extends BaseType {}\n");
        var result = StdLibScanner.Scan(_root);
        Assert.Equal("hl.BaseType.Class", result["hl.Class"]);
    }

    [Fact]
    public void Scan_EnumAbstractCompoundKeyword_CapturesRealTypeName_NotTheWordAbstract()
    {
        // Real bug: a naive "enum" match (not checking "enum abstract" first) would capture "abstract" as the name.
        WriteFile("Xml.hx", "class Xml {}\nenum abstract XmlType(Int) {\n    var Element = 0;\n}\n");
        var result = StdLibScanner.Scan(_root);
        Assert.Equal("Xml.XmlType", result["XmlType"]);
        Assert.DoesNotContain("abstract", result.Keys);
    }

    [Fact]
    public void Scan_PlainEnum_IsCapturedNormally()
    {
        WriteFile("haxe/macro/Type.hx", "package haxe.macro;\nclass Type {}\nenum ClassKind {}\n");
        var result = StdLibScanner.Scan(_root);
        Assert.Equal("haxe.macro.Type.ClassKind", result["haxe.macro.ClassKind"]);
    }

    [Fact]
    public void Scan_PrivateAndExternAndFinalModifiers_AreSkippedBeforeKeyword()
    {
        WriteFile("haxe/Foo.hx", "package haxe;\nclass Foo {}\nprivate final class Bar {}\nextern class Baz {}\n");
        var result = StdLibScanner.Scan(_root);
        Assert.Equal("haxe.Foo.Bar", result["haxe.Bar"]);
        Assert.Equal("haxe.Foo.Baz", result["haxe.Baz"]);
    }

    [Fact]
    public void Scan_MetadataBeforeDeclaration_IsSkipped()
    {
        WriteFile("haxe/Foo.hx", "package haxe;\nclass Foo {}\n@:coreApi\nclass Tagged {}\n");
        var result = StdLibScanner.Scan(_root);
        Assert.Equal("haxe.Foo.Tagged", result["haxe.Tagged"]);
    }

    [Fact]
    public void Scan_NestedOrIndentedDeclaration_IsNotTopLevel_AndIsIgnored()
    {
        WriteFile("haxe/Foo.hx", "package haxe;\nclass Foo {\n    class Inner {}\n}\n");
        var result = StdLibScanner.Scan(_root);
        Assert.DoesNotContain("haxe.Inner", result.Keys);
    }

    [Fact]
    public void Scan_RootPackageSecondaryType_HasNoPackagePrefix()
    {
        WriteFile("StdTypes.hx", "class StdTypes {}\nabstract Helper(Float) {}\n");
        var result = StdLibScanner.Scan(_root);
        Assert.Equal("StdTypes.Helper", result["Helper"]);
    }

    [Fact]
    public void Scan_RootPackageMagicName_IsNeverOverriddenByAScannedSecondaryDeclaration()
    {
        // A root-package type literally named "Class" must not shadow the magic name (packaged hl.Class still is).
        WriteFile("Weird.hx", "class Weird {}\nclass Class {}\n");
        var result = StdLibScanner.Scan(_root);
        Assert.DoesNotContain("Class", result.Keys);
    }

    [Fact]
    public void Scan_LowercaseOrInvalidCapturedName_IsRejected()
    {
        WriteFile("haxe/Foo.hx", "package haxe;\nclass Foo {}\ntypedef bogus = Int;\n");
        var result = StdLibScanner.Scan(_root);
        Assert.DoesNotContain("haxe.bogus", result.Keys);
    }

    [Fact]
    public void Scan_FirstDeclarationWins_OnCrossFileNameCollision()
    {
        WriteFile("haxe/A.hx", "package haxe;\nclass A {}\nclass Shared {}\n");
        WriteFile("haxe/B.hx", "package haxe;\nclass B {}\nclass Shared {}\n");
        var result = StdLibScanner.Scan(_root);
        Assert.True(result["haxe.Shared"] is "haxe.A.Shared" or "haxe.B.Shared");
    }

    [Fact]
    public void Scan_InterfaceAndTypedef_AreCaptured()
    {
        WriteFile("haxe/Foo.hx", "package haxe;\nclass Foo {}\ninterface Bar {}\ntypedef Baz = {};\n");
        var result = StdLibScanner.Scan(_root);
        Assert.Equal("haxe.Foo.Bar", result["haxe.Bar"]);
        Assert.Equal("haxe.Foo.Baz", result["haxe.Baz"]);
    }
}
