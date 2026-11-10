// Code written by Gabriel Mailhot, 09/07/2026.
// Portrait spike console surface: prove a runtime JPG/PNG renders in a GauntletUI widget and that repeated
// load/release keeps VRAM flat (the create_render_texture leak sank v1.23, so this is the load-bearing check).

#region

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CalradiaRemembers.Portraits;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Library;
using TaleWorlds.TwoDimension;
using EngTex = TaleWorlds.Engine.Texture;

#endregion

namespace CalradiaRemembers
{
    public static partial class ModCheats
    {
        /// <summary>
        ///   cr.portrait_spike [full\path\to\image.png]
        ///   Loads an image from disk at runtime and shows it in a test overlay. With no argument, reads
        ///   cr_portrait_test.png from the Bannerlord Documents folder. Close releases the texture.
        /// </summary>
        [CommandLineFunctionality.CommandLineArgumentFunction("portrait_spike", "cr")]
        public static string PortraitSpike(List<string> args)
        {
            string path = ResolvePortraitPath(args, 0);

            if (!RuntimePortraitFactory.TryCreate(path, true, default, out Sprite sprite, out EngTex texture, out string diag))
                return $"Portrait spike FAILED: {diag}\nDrop a PNG/JPG at {path}, or pass a full path.";

            PortraitSpikeManager.Open(sprite, texture, diag);

            return $"Portrait spike: {diag}. Overlay opened; Close releases the texture.";
        }

        /// <summary>
        ///   cr.portrait_spike_stress [count] [full\path]
        ///   Loads and immediately releases the image [count] times (default 50) without showing it, to check
        ///   VRAM stays flat under repeated runtime texture loads. Watch dedicated GPU memory in Task Manager.
        /// </summary>
        [CommandLineFunctionality.CommandLineArgumentFunction("portrait_spike_stress", "cr")]
        public static string PortraitSpikeStress(List<string> args)
        {
            int count = 50;
            if (args != null && args.Count > 0 && int.TryParse(args[0], out int n)) count = Math.Max(1, Math.Min(500, n));

            string path = ResolvePortraitPath(args, 1);
            var ok = 0;
            string lastDiag = "";

            for (var i = 0; i < count; i++)
            {
                if (!RuntimePortraitFactory.TryCreate(path, true, default, out _, out EngTex texture, out string diag))
                {
                    lastDiag = diag;

                    break;
                }

                ok++;
                if (texture != null && !texture.IsReleased) texture.Release();
            }

            string tail = ok < count ? $" (stopped early: {lastDiag})" : "";

            return $"Stress: loaded+released {ok}/{count} textures from {Path.GetFileName(path)}.{tail} Watch GPU memory in Task Manager: it should not climb.";
        }

        /// <summary>The joined path argument from <paramref name="skip" /> onward, else the default test file.</summary>
        private static string ResolvePortraitPath(List<string> args, int skip)
        {
            if (args != null && args.Count > skip)
                return string.Join(" ", args.GetRange(skip, args.Count - skip));

            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "Mount and Blade II Bannerlord", "cr_portrait_test.png");
        }

        /// <summary>
        ///   cr.portrait_id [name]
        ///   Prints the exact file name to drop in ModuleData/ChatPortraits/ for the current (or named) NPC, since
        ///   a hero's StringId is opaque otherwise. Also reports whether an image is already found for them.
        /// </summary>
        [CommandLineFunctionality.CommandLineArgumentFunction("portrait_id", "cr")]
        public static string PortraitId(List<string> args)
        {
            string nameFragment = args is {Count: > 0} ? string.Join(" ", args).Trim() : null;
            Hero hero = !string.IsNullOrWhiteSpace(nameFragment)
                ? Hero.AllAliveHeroes.FirstOrDefault(h => h != Hero.MainHero && h.Name != null && h.Name.ToString().IndexOf(nameFragment, StringComparison.OrdinalIgnoreCase) >= 0)
                : Hero.OneToOneConversationHero;

            if (hero == null) return "No NPC found. Talk to one or pass a name fragment.";

            string hint = ChatPortraitResolver.FileNameHint(hero.StringId, hero.Name?.ToString());
            string existing = ChatPortraitResolver.ResolveForHero(hero.StringId, hero.Name?.ToString());
            string tail = existing != null ? $" Already found: {Path.GetFileName(existing)}." : " None dropped yet.";

            return $"{hero.Name}: drop a square PNG/JPG named {hint} in {ChatPortraitResolver.CustomPortraitDir() ?? "ModuleData\\Custom\\ChatPortraits"} (or ModuleData\\ChatPortraits\\).{tail}";
        }
    }
}
