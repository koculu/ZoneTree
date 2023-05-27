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

[assembly: SuppressMessage(
    "Naming",
    "CA1711: Identifiers should not have incorrect suffix",
    Justification = "A custom queue ends with a Queue...")]

[assembly: SuppressMessage(
    "Naming",
    "CA1707: Identifiers should not contain underscores",
    Justification = "Underscores are good for SSE2, X64 like abbrevations.")]

[assembly: SuppressMessage(
    "Security",
    "CA5394: Do not use insecure randomness",
    Justification = "Randoms are not used for security.")]

[assembly: SuppressMessage(
    "Performance",
    "CA1819: Properties should not return arrays",
    Justification = "This is not a business application and returned arrays are not cloned.")]

[assembly: SuppressMessage(
    "Design",
    "CA1000: Do not declare static members on generic types",
    Justification = "Not critical.")]

