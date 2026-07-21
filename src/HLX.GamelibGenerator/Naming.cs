using System.Text.RegularExpressions;

namespace HLX.GamelibGenerator;

/// <summary>
/// Name-shape checks deciding whether a real HL type name can be honestly referenced
/// from generated Haxe source, and whether an identifier is valid Haxe.
/// </summary>
internal static partial class Naming
{
    // haxe.*, hl.*, sys.* stdlib - needs special handling, but no longer an automatic
    // drop: a bytecode-based classifier (see StdWrapperClassifier) decides per-name
    // whether it needs a generated wrapper or is safe to reference directly. The only
    // remaining ALWAYS-direct category is root-magic names (below) - genuinely shared
    // across every module. (h2d./h3d./hxd./hxsl./FMOD_ used to be excluded too, but
    // every wrapper here now resolves by name against the game's own running process,
    // so there's no separate-version drift risk for those.)
    private static readonly Regex RootStdlibMagicNamePattern = new(
        @"^(?:Std|Type|Sys|String|Math|Array|Date|EReg|Xml|Reflect|IntIterator|UInt|" +
        @"ValueType|Enum|EnumValue|Class|Dynamic|Void|Bool|Int|Float|Map|IMap|StringBuf|" +
        @"StringTools|SysError|Any|DateTools|Lambda|List|UnicodeString|ArrayAccess|" +
        @"Iterable|Iterator|KeyValueIterable|KeyValueIterator|Null|Single)$");

    private static readonly Regex[] StdNamespacePatterns =
    [
        RootStdlibMagicNamePattern,
        new Regex(@"^haxe\..*$"),
        new Regex(@"^hl\..*$"),
        new Regex(@"^sys\..*$"),
    ];

    // Std (haxe./hl./sys., plus bare root-magic names) - a name matching this needs
    // routing through StdWrapperClassifier's wrap-vs-direct-reference decision instead
    // of being generated like an ordinary game class. Renamed from the old
    // "IsExcludedNamespace": these names are no longer categorically excluded, some now
    // get a generated wrapper of their own.
    public static bool IsStdNamespace(string dottedName) =>
        StdNamespacePatterns.Any(p => p.IsMatch(dottedName));

    // Package prefix a std wrapper's own generated Haxe module lives under - e.g.
    // "haxe.ds.StringMap" wraps as "hlx.std.haxe.ds.StringMap" (see GameClass.RuntimeTypeName).
    public const string StdWrapperPackagePrefix = "hlx.std.";

    // FMOD's native C-binding marker names (FMOD_STUDIO_EVENTDESCRIPTION, ...) - opaque
    // native handles with no real Haxe declaration a mod's compile could ever resolve.
    private static readonly Regex FmodNativeAbiPattern = new(@"^FMOD_[A-Za-z0-9_]*$");

    public static bool IsThirdPartyNativeAbiName(string name) => FmodNativeAbiPattern.IsMatch(name);

    // Bare-name check (vs. IsStdNamespace's qualified-prefix check) - needed by
    // StdLibScanner since a secondary type's bare name can coincide with a root name
    // (e.g. hl.Class) without being that root type.
    public static bool IsRootStdlibMagicName(string bareName) => RootStdlibMagicNamePattern.IsMatch(bareName);

    // Compiler-private/nested types ("_ModuleName.NestedType") aren't importable from
    // outside their module. Also catches "$Companion" naming.
    public static bool HasUnreferenceableSegment(string dottedName)
    {
        foreach (var seg in dottedName.Split('.'))
        {
            if (seg.Length == 0) return true;
            if (seg[0] == '_' || seg[0] == '$') return true;
        }
        return false;
    }

    [GeneratedRegex(@"^[A-Za-z_][A-Za-z0-9_]*$")]
    private static partial Regex IdentifierRegex();

    // Every segment must be a plain identifier, and the final segment must start
    // uppercase - rejects raw native-ABI names like "hl_int64_map".
    public static bool LooksLikeValidHaxeTypePath(string dottedName)
    {
        if (string.IsNullOrEmpty(dottedName)) return false;
        var segs = dottedName.Split('.');
        foreach (var seg in segs)
            if (!IdentifierRegex().IsMatch(seg)) return false;
        var last = segs[^1];
        return char.IsAsciiLetterUpper(last[0]);
    }

    // A colliding member is skipped (logged), never mangled - Haxe has no escape hatch.
    private static readonly HashSet<string> ReservedWords = new()
    {
        "abstract", "break", "case", "cast", "catch", "class", "continue", "default",
        "do", "dynamic", "else", "enum", "extends", "extern", "false", "final", "for",
        "function", "if", "implements", "import", "in", "inline", "interface", "macro",
        "new", "null", "operator", "overload", "override", "package", "private",
        "public", "return", "static", "super", "switch", "this", "throw", "true",
        "try", "typedef", "untyped", "using", "var", "while"
    };

    public static bool IsReservedWord(string name) => ReservedWords.Contains(name);

    // Some @:generic instantiations over an anonymous structure mangle the entire
    // field list into the type name (580+ chars seen), blowing past filesystem path limits.
    private const int MaxReasonableNameLength = 180;
    public static bool IsReasonableLength(string dottedName) => dottedName.Length <= MaxReasonableNameLength;

    // Real HL field/method names are otherwise free-form UTF-8.
    public static bool IsValidPlainIdentifier(string name) =>
        name.Length > 0 && IdentifierRegex().IsMatch(name) && !IsReservedWord(name);

    public static string ShortName(string dottedName)
    {
        int i = dottedName.LastIndexOf('.');
        return i < 0 ? dottedName : dottedName[(i + 1)..];
    }

    public static string PackageOf(string dottedName)
    {
        int i = dottedName.LastIndexOf('.');
        return i < 0 ? "" : dottedName[..i];
    }
}
