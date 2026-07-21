using HLX.Core;

namespace HLX.GamelibGenerator;

/// <summary>
/// Decides whether a std (haxe./hl./sys.) <c>ObjectType</c>/<c>EnumType</c> needs a
/// generated reflection-based wrapper, purely from what's already present in the
/// bytecode (<see cref="HlModule.Types"/>) - no separate <c>haxe</c> compiler
/// invocation or JSON intermediate needed, unlike the classifier this replaces
/// (the old JSON-based <c>StdClassClassifier</c>). A type with a real compiled body
/// (own fields, own methods, or static members on its <c>$Companion</c>) recompiles
/// fresh per module and needs the reflective indirection to dodge the cross-module
/// SafeCast bug a direct reference would hit; a type with nothing of its own is a
/// native-ABI shell, already safe to reference directly (same bucket as a root-magic
/// name like <c>Array</c>/<c>String</c>).
/// </summary>
internal static class StdWrapperClassifier
{
    // A compiled EnumType only exists at all if it has real constructors - there's no
    // "shell" case to special-case here, unlike ObjectType. Mirrors the old JSON
    // classifier's own "kind == enum -> always wrap" rule.
    public static bool NeedsWrapper(EnumType e) => true;

    public static bool NeedsWrapper(ObjectType o, ObjectType? companion) =>
        o.Fields.Length > 0
        || o.Protos.Length > 0
        || (companion != null && (companion.Fields.Length > 0 || companion.Bindings.Length > 0));
}
