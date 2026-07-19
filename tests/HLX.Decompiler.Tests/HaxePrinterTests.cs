namespace HLX.Decompiler.Tests;

public class HaxePrinterTests
{
    private static readonly HlType I32 = new PrimitiveType(PrimitiveKind.I32);

    private static string[] Lines(AstBlock block) => HaxePrinter.Print(block).Select(l => l.Text).ToArray();

    [Fact]
    public void SimpleAssign_RendersAsStatement()
    {
        var block = new AstBlock([new AstLeaf(new AssignStatement(new RegisterExpr(0, I32), new IntLiteral(5)))]);
        Assert.Equal(["r0 = 5;"], Lines(block));
    }

    [Fact]
    public void IfElse_IndentsNestedBlocks()
    {
        var thenBody = new AstBlock([new AstLeaf(new ReturnStatement(new IntLiteral(1)))]);
        var elseBody = new AstBlock([new AstLeaf(new ReturnStatement(new IntLiteral(2)))]);
        var cond = new BinaryExpr(BinaryOp.Lt, new RegisterExpr(0, I32), new RegisterExpr(1, I32));
        var block = new AstBlock([new AstIf(cond, thenBody, elseBody)]);

        Assert.Equal(
        [
            "if (r0 < r1) {",
            "    return 1;",
            "} else {",
            "    return 2;",
            "}",
        ], Lines(block));
    }

    [Fact]
    public void IfWithoutElse_OmitsElseBranch()
    {
        var thenBody = new AstBlock([new AstLeaf(new ReturnStatement(null))]);
        var block = new AstBlock([new AstIf(new RegisterExpr(0, I32), thenBody, null)]);

        Assert.Equal(["if (r0) {", "    return;", "}"], Lines(block));
    }

    [Fact]
    public void While_WithLabel_PrefixesLabel()
    {
        var body = new AstBlock([new AstBreak("loop0")]);
        var block = new AstBlock([new AstWhile(new BoolLiteral(true), body, "loop0")]);

        Assert.Equal(["loop0: while (true) {", "    break loop0;", "}"], Lines(block));
    }

    [Fact]
    public void While_WithoutLabel_NoPrefix()
    {
        var body = new AstBlock([new AstContinue(null)]);
        var block = new AstBlock([new AstWhile(new BoolLiteral(true), body, null)]);

        Assert.Equal(["while (true) {", "    continue;", "}"], Lines(block));
    }

    [Fact]
    public void Switch_RendersCasesAndDefault()
    {
        var cases = new[]
        {
            new AstSwitchCase(0, new AstBlock([new AstLeaf(new ReturnStatement(new IntLiteral(10)))])),
            new AstSwitchCase(null, new AstBlock([new AstLeaf(new ReturnStatement(new IntLiteral(-1)))])),
        };
        var block = new AstBlock([new AstSwitch(new RegisterExpr(0, I32), [.. cases])]);

        Assert.Equal(
        [
            "switch (r0) {",
            "    case 0:",
            "        return 10;",
            "    default:",
            "        return -1;",
            "}",
        ], Lines(block));
    }

    [Fact]
    public void GotoAndLabel_RenderDistinctly()
    {
        var block = new AstBlock([new AstGoto("L0004"), new AstLabel("L0004")]);
        var lines = HaxePrinter.Print(block);

        Assert.Equal("goto L0004;", lines[0].Text);
        Assert.Equal(PrintedLineKind.Code, lines[0].Kind);
        Assert.Equal("L0004:", lines[1].Text);
        Assert.Equal(PrintedLineKind.Label, lines[1].Kind);
    }

    [Fact]
    public void Comment_UsesCommentKind()
    {
        var block = new AstBlock([new AstComment("assert")]);
        var lines = HaxePrinter.Print(block);
        Assert.Equal("// assert", lines[0].Text);
        Assert.Equal(PrintedLineKind.Comment, lines[0].Kind);
    }

    [Fact]
    public void TryCatch_RendersBothBlocks()
    {
        var tryBody = new AstBlock([new AstLeaf(new ThrowStatement(new RegisterExpr(2, I32), false))]);
        var catchBody = new AstBlock([new AstLeaf(new ReturnStatement(new RegisterExpr(2, I32)))]);
        var block = new AstBlock([new AstTry(tryBody, new AstCatch(new RegisterExpr(2, I32), catchBody))]);

        Assert.Equal(
        [
            "try {",
            "    throw r2;",
            "} catch (r2) {",
            "    return r2;",
            "}",
        ], Lines(block));
    }
}
