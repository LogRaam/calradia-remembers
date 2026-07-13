// Code written by Gabriel Mailhot, 12/07/2026.
// Polyfill: enables C# 'init'-only setters when targeting netstandard2.0 (which lacks the type the compiler
// emits for them). Mirrors the same shim the mod already carries.

namespace System.Runtime.CompilerServices
{
    internal static class IsExternalInit
    {
    }
}
