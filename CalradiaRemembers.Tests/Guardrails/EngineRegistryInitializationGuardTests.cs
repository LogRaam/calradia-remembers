// Code written by Gabriel Mailhot, 05/08/2026.
// A WHITEBOX guardrail: it inspects the COMPILED mod assembly (CalradiaRemembers.dll) with Mono.Cecil, reading
// IL from the file WITHOUT loading or executing it (so the TaleWorlds DLLs never enter this test process), and
// FAILS the build if any constructor reintroduces the load-order landmine that crashed the game on 2026-08-05.
//
// WHY IT EXISTS: that day a save-load hard crash (native 0xE0434352 / TypeInitializationException) was traced to
// CouncilWardBehavior. It held a STATIC FIELD initializer that built a SkillObject[] out of DefaultSkills. A
// behavior's ctor writing a static field triggers the type initializer, which evaluated that array and touched
// the engine's DefaultSkills registry. On the SAVE-LOAD path OnGameStart adds the behavior BEFORE the skill
// registry is populated, so DefaultSkills threw inside the type initializer and the whole game crashed. A fresh
// campaign worked (registry already up), so it read as "new-works / load-crashes" and hid from ordinary testing.
//
// WHAT BREAKS WITHOUT THIS TEST: the fix (SelectableSpecialties made lazy) is invisible to the compiler and to
// every other test; nothing stops the same static-init-touches-a-registry pattern from being pasted back into
// this or any future behavior, and it would crash only on a player's LOAD, not on the developer's fresh
// campaign. This scan turns that silent, load-only landmine into a red build. Running it is also a standing
// regression check on the CouncilWardBehavior fix itself.
//
// THE RULE IT ENFORCES: never touch a Default* engine registry (or Campaign.Current / Game.Current /
// MBObjectManager.Instance) from a static initializer / .cctor / .ctor; defer it (lazy accessor, OnGameStart,
// RegisterEvents, or an event handler). See EngineRegistryGuard for the blacklist and the pure decision.

#region

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using FluentAssertions;
using Mono.Cecil;
using Mono.Cecil.Cil;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests.Guardrails
{
    [TestFixture]
    public sealed class EngineRegistryInitializationGuardTests
    {
        // THE STAKE: this is the whole reason the file exists. If ANY type's static constructor (where static
        // field initializers compile to) or ANY instance constructor references a blacklisted engine registry or
        // process-global singleton, the game crashes on the save-load path for real players (the 2026-08-05
        // CouncilWardBehavior / DefaultSkills TypeInitializationException). It MUST pass against the current,
        // already-fixed code; every offender is reported at once so a reviewer sees the full blast radius, not
        // just the first hit.
        [Test]
        public void GIVEN_the_compiled_mod_assembly_WHEN_scanning_every_constructor_THEN_none_touches_an_unsafe_engine_registry()
        {
            string? dllPath = TryLocateModAssembly();

            // A missing DLL is an inability to run the check, not a passed check. Under nCrunch the test runs from
            // an isolated workspace with no path back to the repo tree (neither TestDirectory nor this file's
            // CallerFilePath resolves to it), and a net10 test project cannot reference the net472 mod to get its
            // DLL copied in, so the built assembly is genuinely unreachable there: SKIP (Ignore), since the real
            // gate runs under `dotnet test` / CI on every batch. Everywhere else a missing DLL means the mod was
            // simply not built, so FAIL loudly with the one action that fixes it rather than silently no-opping
            // the guardrail in CI or a clean checkout.
            if (dllPath == null)
            {
                string message =
                    "Could not locate a compiled CalradiaRemembers.dll to scan. Build the mod project first, e.g.\n" +
                    "  dotnet build \"CalradiaRemembers\\CalradiaRemembers.csproj\"\n" +
                    "then re-run the tests. This guardrail inspects the built assembly's IL; without it the " +
                    "save-load crash check cannot run.";

                if (Environment.GetEnvironmentVariable("NCrunch") == "1")
                    Assert.Ignore("nCrunch runs this test from an isolated workspace with no path to the mod's " +
                                  "build output, so the IL scan cannot run here; it runs under dotnet test / CI. " + message);
                else
                    Assert.Fail(message);

                return;
            }

            var violations = new List<string>();
            string? assemblyName = null;
            int constructorsScanned = 0;

            using (ModuleDefinition module = ModuleDefinition.ReadModule(dllPath))
            {
                assemblyName = module.Assembly?.Name?.Name;

                // GetTypes() returns every type including nested and compiler-generated ones, so no constructor is
                // out of scope. Base-type resolution is never needed: we scan each type's own ctors directly, so
                // nothing here has to resolve against the (absent) TaleWorlds DLLs.
                foreach (TypeDefinition type in module.GetTypes())
                {
                    // IsConstructor covers BOTH the static constructor (.cctor, where static field initializers
                    // land) and every instance constructor (.ctor).
                    foreach (MethodDefinition ctor in type.Methods.Where(m => m.IsConstructor))
                    {
                        if (!ctor.HasBody) continue;

                        constructorsScanned++;
                        string ctorKind = ctor.IsStatic ? ".cctor" : ".ctor";

                        foreach (Instruction instruction in ctor.Body.Instructions)
                        {
                            TypeReference? declaring = DeclaringTypeOf(instruction.Operand);
                            if (declaring == null || !EngineRegistryGuard.IsUnsafeRegistry(declaring.Name)) continue;

                            string member = MemberNameOf(instruction.Operand) ?? "<member>";
                            violations.Add(
                                $"{type.FullName}.{ctorKind} references {declaring.Name}.{member} " +
                                $"(namespace '{declaring.Namespace}'). A constructor / static initializer must not " +
                                "touch an engine registry or process-global singleton: it runs before the registry " +
                                "is populated on the save-load path and crashes the game. Fix: make it lazy, or " +
                                "move it to OnGameStart / RegisterEvents / an event handler.");
                        }
                    }
                }
            }

            // Anti-vacuity: prove we actually scanned the real mod assembly and iterated real constructors, so a
            // future path/build mishap that resolves to an empty or wrong DLL turns into a red test instead of a
            // false green (a guardrail that scans nothing and reports "clean" is the very thing this task warns
            // against). The mod defines hundreds of types; any positive count proves the module truly loaded.
            assemblyName.Should().Be("CalradiaRemembers",
                "the scan must run against the mod assembly, not some unrelated CalradiaRemembers.dll on disk");
            constructorsScanned.Should().BeGreaterThan(0,
                "finding zero constructors means the DLL failed to load its types and the scan was vacuous");

            violations.Should().BeEmpty(
                "a constructor or static initializer that touches a Default* engine registry or a process-global " +
                "singleton (Campaign/Game/MBObjectManager) crashes the game on the save-load path (the 2026-08-05 " +
                "CouncilWardBehavior / DefaultSkills TypeInitializationException). Offenders:\n" +
                string.Join("\n", violations));
        }

        /// <summary>
        ///   The declaring type of a method-call or field-access operand, or null for any other opcode operand.
        ///   Both MethodReference and FieldReference expose a DeclaringType; a property getter (Campaign.Current)
        ///   compiles to a call on its get_Current MethodReference, so it is caught here too.
        /// </summary>
        private static TypeReference? DeclaringTypeOf(object? operand)
        {
            return operand switch
            {
                MethodReference method => method.DeclaringType,
                FieldReference field => field.DeclaringType,
                _ => null
            };
        }

        /// <summary>The referenced member's simple name (method or field), for the violation message.</summary>
        private static string? MemberNameOf(object? operand)
        {
            return operand is MemberReference member ? member.Name : null;
        }

        /// <summary>
        ///   Finds a compiled CalradiaRemembers.dll to scan by PATH (never a ProjectReference: this net10.0 test
        ///   project cannot reference the net472 mod, and doing so would pull the TaleWorlds runtime into the
        ///   process and defeat the point). The mod's configured OutputPath can live OUTSIDE the repo, but its
        ///   obj\Debug|Release copies are the same compiled IL and always land inside the repo after a build, so a
        ///   bounded recursion of the repo root finds a scannable copy. The search is anchored FIRST on this test
        ///   file's own <see cref="CallerFilePathAttribute" /> source path, a compile-time constant that points at
        ///   the real repo tree even under nCrunch (whose runtime TestDirectory sits in an isolated workspace far
        ///   from the repo, which is why the plain TestDirectory anchor found nothing there); the runtime
        ///   TestDirectory is kept as a second anchor for a plain `dotnet test` / CI layout. Newest write time
        ///   wins so the scan reflects the latest build. Returns null only when no built copy exists at all (the
        ///   caller turns that into a clear "build the mod first" failure).
        /// </summary>
        private static string? TryLocateModAssembly([CallerFilePath] string sourceFilePath = "")
        {
            var anchors = new List<DirectoryInfo>();
            if (!string.IsNullOrEmpty(sourceFilePath))
            {
                string? sourceDir = Path.GetDirectoryName(sourceFilePath);
                if (!string.IsNullOrEmpty(sourceDir) && Directory.Exists(sourceDir))
                    anchors.Add(new DirectoryInfo(sourceDir));
            }

            anchors.Add(new DirectoryInfo(TestContext.CurrentContext.TestDirectory));

            var searchRoots = new List<DirectoryInfo>();
            foreach (DirectoryInfo start in anchors)
            {
                DirectoryInfo? repoRoot = WalkUp(start, IsRepoRoot);
                if (repoRoot != null) searchRoots.Add(repoRoot);
                searchRoots.Add(start); // fallback if the repo-root marker is not found from this anchor
            }

            FileInfo? best = null;
            foreach (DirectoryInfo root in Distinct(searchRoots))
            {
                foreach (FileInfo dll in SafeEnumerate(root, "CalradiaRemembers.dll"))
                {
                    if (!IsBuildOutput(dll)) continue;
                    if (best == null || dll.LastWriteTimeUtc > best.LastWriteTimeUtc) best = dll;
                }
            }

            return best?.FullName;
        }

        private static bool IsRepoRoot(DirectoryInfo dir)
        {
            return File.Exists(Path.Combine(dir.FullName, "CalradiaRemembers.slnx")) ||
                   File.Exists(Path.Combine(dir.FullName, "CalradiaRemembers", "CalradiaRemembers.csproj"));
        }

        private static DirectoryInfo? WalkUp(DirectoryInfo start, Func<DirectoryInfo, bool> match)
        {
            for (DirectoryInfo? d = start; d != null; d = d.Parent)
            {
                if (match(d)) return d;
            }

            return null;
        }

        // The mod DLL we want is real build output (a bin\ or obj\ copy of the mod project), never some unrelated
        // stray file that happens to share the name.
        private static bool IsBuildOutput(FileInfo file)
        {
            string path = file.FullName.Replace('/', '\\');
            return path.IndexOf("\\bin\\", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   path.IndexOf("\\obj\\", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        // Recursive file search that tolerates directories it cannot read and skips noisy VCS / IDE / runner
        // folders so a whole-repo walk stays quick.
        private static IEnumerable<FileInfo> SafeEnumerate(DirectoryInfo root, string fileName)
        {
            var stack = new Stack<DirectoryInfo>();
            stack.Push(root);

            while (stack.Count > 0)
            {
                DirectoryInfo current = stack.Pop();

                FileInfo[] files;
                try
                {
                    files = current.GetFiles(fileName);
                }
                catch
                {
                    continue;
                }

                foreach (FileInfo f in files) yield return f;

                DirectoryInfo[] subDirs;
                try
                {
                    subDirs = current.GetDirectories();
                }
                catch
                {
                    continue;
                }

                foreach (DirectoryInfo sub in subDirs)
                {
                    if (IsSkippableDirectory(sub.Name)) continue;
                    stack.Push(sub);
                }
            }
        }

        private static bool IsSkippableDirectory(string name)
        {
            return name.Equals(".git", StringComparison.OrdinalIgnoreCase) ||
                   name.Equals(".vs", StringComparison.OrdinalIgnoreCase) ||
                   name.StartsWith(".NCrunch", StringComparison.OrdinalIgnoreCase) ||
                   name.StartsWith("_NCrunch", StringComparison.OrdinalIgnoreCase);
        }

        private static IEnumerable<DirectoryInfo> Distinct(IEnumerable<DirectoryInfo> dirs)
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (DirectoryInfo d in dirs)
            {
                if (seen.Add(d.FullName)) yield return d;
            }
        }
    }
}
