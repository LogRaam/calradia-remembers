// Code written by Gabriel Mailhot, 19/10/2026.
// WHERE A ROW SITS on a character's encyclopedia page. Every CR row is a UIExtenderEx patch appended right after the
// vanilla InfoContainer, and UIExtenderEx applies them in the assembly's type order, so the LAST applied lands on TOP:
// a row's place on the page is decided by its patch's SOURCE FILE name (the compiler emits types file by file,
// alphabetically), which no compiler warning and no other test sees. tashmetu
// (Regard and Standing, 18/10/2026, ask 6) asked for "the player's own regard first" in the regard block; Gabriel said
// yes on 19/10/2026. If this fails, a renamed or added patch has moved the regard row back under the houses, or put a
// list above its own heading, and nobody would know before a player opened a page.
//
// It reads the COMPILED mod assembly with Mono.Cecil (no TaleWorlds DLL loaded), as the engine-registry guard does.

#region

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FluentAssertions;
using Mono.Cecil;
using Mono.Cecil.Cil;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests.Guardrails
{
    [TestFixture]
    public sealed class HeroPageRowOrderTests
    {
        // STAKE (tashmetu, 18/10/2026): the reader of a lord's page looks first for what that lord thinks of their own
        // character; under the friends, foes and houses of a lord with many ties it was the last thing found.
        [Test]
        public void GIVEN_a_character_s_page_WHEN_its_regard_rows_are_stacked_THEN_the_regard_toward_the_player_reads_first()
        {
            List<string> rows = RowsTopToBottom();

            rows.Should().Contain("CR_HeroPersonalRelation");
            rows.IndexOf("CR_HeroPersonalRelation").Should().BeLessThan(rows.IndexOf("CR_HeroRegardWhyLabel"));
            rows.IndexOf("CR_HeroPersonalRelation").Should().BeLessThan(rows.IndexOf("CR_HeroRegardHousesLabel"));
        }

        // STAKE: a list read above its own heading ("Hertogi, friend: ..." and only then "Why:") reads as belonging to
        // the section above it.
        [Test]
        public void GIVEN_a_character_s_page_WHEN_a_section_has_a_heading_THEN_the_heading_reads_above_its_list()
        {
            List<string> rows = RowsTopToBottom();

            foreach (string label in rows.Where(r => r.EndsWith("Label", StringComparison.Ordinal)))
            {
                string items = label.Substring(0, label.Length - "Label".Length) + "Items";
                if (rows.Contains(items)) rows.IndexOf(label).Should().BeLessThan(rows.IndexOf(items), $"{label} heads {items}");
            }
        }

        // The prefab file of every patch appended after InfoContainer, in the order the page shows them: type order
        // reversed, since each Append inserts right after the container.
        private static List<string> RowsTopToBottom()
        {
            string? dllPath = EngineRegistryInitializationGuardTests.TryLocateModAssembly();
            if (dllPath == null)
            {
                if (Environment.GetEnvironmentVariable("NCrunch") == "1") Assert.Ignore("nCrunch cannot reach the built mod assembly; this runs under dotnet test.");
                Assert.Fail("Build the mod first (dotnet build CalradiaRemembers/CalradiaRemembers.csproj): this guard reads the compiled assembly.");
            }

            var applied = new List<string>();
            using (ModuleDefinition module = ModuleDefinition.ReadModule(dllPath!))
            {
                foreach (TypeDefinition type in module.Types)
                {
                    CustomAttribute? at = type.CustomAttributes.FirstOrDefault(a => a.AttributeType.Name == "PrefabExtensionAttribute");
                    if (at == null || at.ConstructorArguments.Count < 2) continue;
                    if (at.ConstructorArguments[0].Value as string != "EncyclopediaHeroPage") continue;
                    if (!(at.ConstructorArguments[1].Value as string ?? "").Contains("InfoContainer")) continue;

                    string? file = FileNameOf(type);
                    if (file != null) applied.Add(file);
                }
            }

            applied.Should().NotBeEmpty("the hero page patches must be found in the compiled assembly");
            applied.Reverse();

            return applied;
        }

        // The literal returned by the patch's FileName getter.
        private static string? FileNameOf(TypeDefinition type)
        {
            MethodDefinition? getter = type.Properties.FirstOrDefault(p => p.Name == "FileName")?.GetMethod;

            return getter?.Body?.Instructions.FirstOrDefault(i => i.OpCode == OpCodes.Ldstr)?.Operand as string;
        }
    }
}
