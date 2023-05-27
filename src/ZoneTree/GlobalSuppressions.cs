// This file is used by Code Analysis to maintain SuppressMessage
// attributes that are applied to this project.
// Project-level suppressions either have no target or are given
// a specific target and scoped to a namespace, type, member, etc.

using System.Diagnostics.CodeAnalysis;
using System.Runtime.Intrinsics.X86;

[assembly: SuppressMessage(
    "Design",
    "CA1032:Implement standard exception constructors",
    Justification = "Prefer smaller code size.")]

[assembly: SuppressMessage(
    "Design",
    "CA1031: Do not catch general exception types",
    Justification = "Exceptions are logged.")]


[assembly: SuppressMessage(
    "Design",
    "CA1309: Use ordinal StringComparison",
    Justification = "Several string comparison types are supported by comparers.",
    Scope = "namespaceanddescendants",
    Target = "Tenray.ZoneTree.Comparers")]