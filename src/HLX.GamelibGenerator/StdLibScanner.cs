using System.Text.RegularExpressions;

namespace HLX.GamelibGenerator;

/// <summary>
/// Scans the installed Haxe std/ source to build an accurate secondary-type ->
/// module-qualified-path table (see KnownModuleQualifiedPaths).
/// </summary>
internal static partial class StdLibScanner
{
    // Top-level (column 0) declarations only. "enum abstract Name" must be checked
    // before plain "enum", or "abstract" gets captured as the type name instead
    // (Xml.hx's `enum abstract XmlType(Int)`).
    [GeneratedRegex(@"^(?:@:[\w.]+(?:\([^)]*\))?\s*\n?)*(?:private\s+|extern\s+|final\s+)*(?:enum\s+abstract|class|interface|enum|abstract|typedef)\s+(\w+)", RegexOptions.Multiline)]
    private static partial Regex TopLevelTypeRegex();

    public static Dictionary<string, string> Scan(string stdRoot)
    {
        var result = new Dictionary<string, string>();
        if (!Directory.Exists(stdRoot)) return result;

        foreach (var file in Directory.EnumerateFiles(stdRoot, "*.hx", SearchOption.AllDirectories))
        {
            var rel = Path.GetRelativePath(stdRoot, file);
            var withoutExt = rel[..^".hx".Length];
            var modulePath = withoutExt.Replace(Path.DirectorySeparatorChar, '.').Replace(Path.AltDirectorySeparatorChar, '.');
            var primaryShortName = Path.GetFileNameWithoutExtension(file);
            var package = Naming.PackageOf(modulePath);

            string text;
            try { text = File.ReadAllText(file); }
            catch { continue; }

            foreach (Match m in TopLevelTypeRegex().Matches(text))
            {
                var typeName = m.Groups[1].Value;
                if (typeName == primaryShortName) continue; // primary type, already referenceable as the module path itself
                if (!Naming.LooksLikeValidHaxeTypePath(typeName)) continue;
                // Key is package-qualified (e.g. "hl.Class"), matching what HL bytecode records.
                var key = package.Length == 0 ? typeName : $"{package}.{typeName}";
                // Only relevant when package is empty (the key IS the bare name then):
                // never override a root-package magic name like "Class" with a qualified path.
                if (package.Length == 0 && Naming.IsRootStdlibMagicName(typeName)) continue;
                result.TryAdd(key, $"{modulePath}.{typeName}"); // first declaration wins on a rare cross-file collision
            }
        }
        return result;
    }
}
