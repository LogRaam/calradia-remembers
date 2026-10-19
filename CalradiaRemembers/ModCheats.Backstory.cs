// Code written by Gabriel Mailhot, 11/07/2026.

#region

using System;
using System.Collections.Generic;
using System.Linq;
using CalradiaRemembers.Foundation;
using CalradiaRemembers.Logic;
using CalradiaRemembers.Lore;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Library;

#endregion

namespace CalradiaRemembers
{
    public static partial class ModCheats
    {
        /// <summary>
        ///   cr.backstory_test [name]
        ///   Injects a strong NAMED persona (a Kratos-style warrior) as a runtime character override on the
        ///   named lord (or the current conversation NPC), WITHOUT editing character_overrides.json, and forces
        ///   custom traits on for the session, so the authored-backstory prompt path is exercised end to end.
        ///   This reproduces the case the v1.30.6 regression broke: a backstory that names a bold figure but
        ///   spells out no literal speech quirks. Send a message in chat afterwards, the persona should land
        ///   forcefully (a low, hard, brutal warrior voice), never Calradian lore about God of War. A session
        ///   reload restores the file-backed overrides.
        /// </summary>
        [CommandLineFunctionality.CommandLineArgumentFunction("backstory_test", "cr")]
        public static string InjectTestBackstory(List<string> args)
        {
            Hero hero = ResolveForBackstory(args);
            if (hero == null) return "No NPC found. Pass a name fragment or start a conversation first.";

            const string kratos =
                "You carry yourself like Kratos from God of War: a battle-scarred, rage-forged warrior. Your " +
                "voice is low, hard, and merciless. You speak in short, brutal, commanding sentences. You have " +
                "no patience for weakness or flattery, and a buried grief hardens every word you speak.";

            CharacterOverrideLoader.InjectRuntimeOverride(new CharacterOverride {
                Id = hero.StringId,
                Hero = hero.Name?.ToString(),
                Backstory = kratos
            });

            bool wasEnabled = ModSettings.UseCustomTraits;
            if (!wasEnabled && CrSettings.Instance != null) CrSettings.Instance.UseCustomTraits = true;

            string note = wasEnabled
                ? "custom traits already ON"
                : "custom traits forced ON for this session (toggle back in MCM if you want)";

            ModLog.Info($"[OVERRIDE] backstory_test injected a Kratos-style persona on {hero.Name} ({hero.StringId}); {note}. " +
                        "The per-turn sync will carry it into the next prompt. Enable Verbose Logging to see the BACKSTORY AND VOICE block.");

            return $"{hero.Name}: injected a Kratos-style named persona ({note}). Send a message in chat now, " +
                   "the persona should come through forcefully. A reload restores character_overrides.json.";
        }

        /// <summary>
        ///   Resolves the target for the backstory test, preferring an EXACT (case-insensitive) full-name match
        ///   before the shared fuzzy resolver, so a query like "Mina" is not swallowed by a substring hero
        ///   like "Vamina". Falls back to <see cref="ResolveHero" /> (fuzzy fragment, or the conversation NPC
        ///   when no name is given).
        /// </summary>
        private static Hero ResolveForBackstory(List<string> args)
        {
            string query = args == null ? "" : string.Join(" ", args).Trim();
            if (!string.IsNullOrEmpty(query))
            {
                List<Hero> exact = Hero.AllAliveHeroes
                    .Where(h => h != Hero.MainHero && h.Name != null
                                && string.Equals(h.Name.ToString(), query, StringComparison.OrdinalIgnoreCase))
                    .ToList();
                if (exact.Count == 1) return exact[0];
            }

            return ResolveHero(args);
        }
    }
}
