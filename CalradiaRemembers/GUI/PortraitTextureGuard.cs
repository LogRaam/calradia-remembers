// Code written by Gabriel Mailhot, 05/07/2026.

#region

using System;
using System.Collections.Generic;
using CalradiaRemembers.Logic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Core.ViewModelCollection.ImageIdentifiers;

#endregion

namespace CalradiaRemembers.GUI
{
    /// <summary>
    ///   The single gate for every character portrait the mod shows (chat lines, the courier letter). A portrait
    ///   is backed by a GPU render-target (a character tableau) that Bannerlord allocates PER widget, so left
    ///   unmanaged a long scene accumulates dozens and crashes on rglGPU_device_d3d11::create_render_texture
    ///   (out of VRAM). Every window creates its portraits through <see cref="Create" /> and enforces the VRAM
    ///   policy through <see cref="EnforceOnePerParticipant" /> / <see cref="ReleaseAll" />, delegating the
    ///   engine-free decision to the unit-tested <see cref="PortraitCapPolicy" />.
    /// </summary>
    public static class PortraitTextureGuard
    {
        /// <summary>
        ///   Resolves a character into the flat id/provider/args a bound ImageIdentifierWidget renders from.
        ///   A null character (a synthesized speaker with no CharacterObject) yields an empty, image-less result.
        ///   Not cached: the strings encode the character's live body and equipment, and caching them would risk
        ///   a stale portrait (wrong gear) for no VRAM gain — VRAM is bounded by the release policy, not here.
        /// </summary>
        public static PortraitData Create(CharacterObject character)
        {
            if (character == null) return PortraitData.None;

            try
            {
                CharacterImageIdentifierVM portrait = new CharacterImageIdentifierVM(CharacterCode.CreateFrom(character));

                return new PortraitData(
                    portrait.Id ?? "",
                    portrait.TextureProviderName ?? "",
                    portrait.AdditionalArgs ?? "");
            }
            catch (Exception ex)
            {
                ModLog.Error($"PortraitTextureGuard.Create failed: {ex.GetType().Name}: {ex.Message}");

                return PortraitData.None;
            }
        }

        /// <summary>
        ///   Collapses a transcript to at most one live render-target per distinct participant (freeing older
        ///   duplicates), keeping VRAM bounded by the number of participants rather than the scene length.
        /// </summary>
        public static void EnforceOnePerParticipant(IReadOnlyList<IManagedPortrait> lines)
        {
            try
            {
                int freed = PortraitCapPolicy.KeepOnePerParticipant(lines);
                if (freed > 0)
                    ModLog.Info($"[PORTRAIT] one per participant: {freed} tableau(s) freed.");
            }
            catch (Exception ex)
            {
                ModLog.Error($"PortraitTextureGuard.EnforceOnePerParticipant failed: {ex.GetType().Name}: {ex.Message}");
            }
        }

        /// <summary>Frees every live render-target in the set on window teardown, so none survives a close.</summary>
        public static void ReleaseAll(IReadOnlyList<IManagedPortrait> lines)
        {
            try
            {
                int freed = PortraitCapPolicy.ReleaseAll(lines);
                if (freed > 0)
                    ModLog.Info($"[PORTRAIT] released {freed} tableau(s) on close.");
            }
            catch (Exception ex)
            {
                ModLog.Error($"PortraitTextureGuard.ReleaseAll failed: {ex.GetType().Name}: {ex.Message}");
            }
        }
    }

    /// <summary>The flat portrait binding a widget renders from: image id, texture provider name, extra args.</summary>
    public readonly struct PortraitData
    {
        public PortraitData(string id, string textureProviderName, string additionalArgs)
        {
            Id = id ?? "";
            TextureProviderName = textureProviderName ?? "";
            AdditionalArgs = additionalArgs ?? "";
        }

        /// <summary>An image-less portrait (no character), rendering nothing.</summary>
        public static PortraitData None => new PortraitData("", "", "");

        public string Id { get; }
        public string TextureProviderName { get; }
        public string AdditionalArgs { get; }

        /// <summary>True when this actually resolves to a rendered character (a non-empty image id).</summary>
        public bool HasImage => !string.IsNullOrEmpty(Id);
    }
}
