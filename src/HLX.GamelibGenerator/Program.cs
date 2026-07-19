using HLX.Core.IO;

namespace HLX.GamelibGenerator;

class Program
{
    static int Main(string[] args)
    {
        if (args.Length > 0 && args[0] == "--scan-std")
        {
            // Dev-time only: prints C# dictionary-initializer syntax to paste into
            // KnownModuleQualifiedPaths after a Haxe version bump.
            var scanned = StdLibScanner.Scan(args.Length > 1 ? args[1] : "/usr/share/haxe/std");
            foreach (var (k, v) in scanned.OrderBy(kv => kv.Key, StringComparer.Ordinal))
                Console.WriteLine($"        [\"{k}\"] = \"{v}\",");
            return 0;
        }

        if (args.Length < 2)
        {
            Console.WriteLine("Usage: HLX.GamelibGenerator <path/to/hlboot.dat> <output/directory>");
            return 1;
        }

        string hlbootPath = args[0];
        string outDir = args[1];

        Console.WriteLine($"[HLX.GamelibGenerator] Parsing {hlbootPath}...");

        HLX.Core.HlModule module;
        using (var stream = File.OpenRead(hlbootPath))
            module = HlReader.Read(stream);

        Console.WriteLine($"[HLX.GamelibGenerator] Parsed OK - {module.Types.Length} types, {module.Functions.Length} functions, {module.Natives.Length} natives.");

        // Candidate names must be collected for both classes and enums before the
        // shared HaxeTypeMapper is built, since it needs both full sets to resolve an
        // in-scope reference; each collector's CollectAll then runs against it.
        // Constructor recovery is an independent, standalone bytecode scan.
        var ctorCollector = new ConstructorCollector(module);
        Console.WriteLine($"[HLX.GamelibGenerator] Constructor recovery: {ctorCollector.TotalCandidateSitesFound} New+Call site(s) found, " +
            $"{ctorCollector.ClassesResolved} class(es) with an unambiguous constructor, " +
            $"{ctorCollector.ClassesAmbiguous} with ambiguous/conflicting candidates (skipped).");

        var collector = new ClassCollector(module, ctorCollector.ConstructorFindexByTypeIndex);
        var enumCollector = new EnumCollector(module);
        var mapper = new HaxeTypeMapper(module, collector.CandidateNames, enumCollector.CandidateNames);
        collector.CollectAll(mapper);
        enumCollector.CollectAll(mapper);

        Console.WriteLine($"[HLX.GamelibGenerator] {collector.TotalObjectTypes} object types total " +
            $"({collector.CompanionTypes} companions, {collector.ExcludedByNamespace} excluded by namespace, " +
            $"{collector.ExcludedUnreferenceable} private/nested/unreferenceable, " +
            $"{collector.ExcludedInvalidIdentifier} not valid Haxe type paths, " +
            $"{collector.ExcludedTooLong} too long to be a safe output path) - " +
            $"{collector.CandidateNames.Count} classes in scope for generation.");

        Console.WriteLine($"[HLX.GamelibGenerator] {enumCollector.TotalEnumTypes} enum types total " +
            $"({enumCollector.ExcludedByNamespace} excluded by namespace, " +
            $"{enumCollector.ExcludedUnreferenceable} private/nested/unreferenceable, " +
            $"{enumCollector.ExcludedInvalidIdentifier} not valid Haxe type paths, " +
            $"{enumCollector.ExcludedTooLong} too long to be a safe output path) - " +
            $"{enumCollector.CandidateNames.Count} enums in scope for generation.");

        var grouping = GenericGrouping.Run(collector.Classes);

        Console.WriteLine($"[HLX.GamelibGenerator] {grouping.Singles.Count} plain wrappers, " +
            $"{grouping.Groups.Count} collapsed @:generic wrapper(s) covering {grouping.Aliases.Count} concrete instantiation(s), " +
            $"{collector.SkippedMembers} member(s) skipped across all classes (see per-file notes), " +
            $"{collector.ClassesWithConstructor} class(es) got a constructor.");

        Directory.CreateDirectory(outDir);
        int filesWritten = 0, filesFailed = 0;

        foreach (var c in grouping.Singles)
            if (WriteFile(outDir, c.FullName, HxEmitter.EmitClass(c))) filesWritten++; else filesFailed++;

        foreach (var g in grouping.Groups)
            if (WriteFile(outDir, g.FullName, HxEmitter.EmitGenericGroup(g))) filesWritten++; else filesFailed++;

        foreach (var (concreteName, (group, typeArg)) in grouping.Aliases)
            if (WriteFile(outDir, concreteName, HxEmitter.EmitAlias(concreteName, group, typeArg))) filesWritten++; else filesFailed++;

        foreach (var e in enumCollector.Enums)
            if (WriteFile(outDir, e.FullName, HxEmitter.EmitEnum(e))) filesWritten++; else filesFailed++;

        Console.WriteLine($"[HLX.GamelibGenerator] Generation complete. {filesWritten} .hx files written to {outDir}" +
            (filesFailed > 0 ? $" ({filesFailed} failed - see warnings above)." : "."));
        return 0;
    }

    // One bad class's output path is a defensive log-and-skip, not a fatal error for the whole run.
    private static bool WriteFile(string outDir, string fullName, string content)
    {
        var package = Naming.PackageOf(fullName);
        var shortName = Naming.ShortName(fullName);
        var dir = package.Length == 0 ? outDir : Path.Combine(outDir, package.Replace('.', Path.DirectorySeparatorChar));
        try
        {
            Directory.CreateDirectory(dir);
            var path = Path.Combine(dir, shortName + ".hx");
            File.WriteAllText(path, content);
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[HLX.GamelibGenerator] WARNING: failed to write '{fullName}': {ex.Message}");
            return false;
        }
    }
}
