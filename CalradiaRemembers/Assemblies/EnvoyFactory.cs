// Code written by Gabriel Mailhot, 29/08/2026.
// Convergence Inc 4 of 5: builds the ephemeral synthetic NpcProfile for a council seat CouncilAttendanceResolver
// (Inc 2) resolved to Ambassador. Mirrors CommonsProfileFactory's own recipe exactly (a synthetic Id, no real
// Hero backing, never persisted): that factory is the standing proof a hand-built NpcProfile with no
// HeroStringId is fully voiceable through the real NpcChatService pipeline (CommonsDialogBehavior wires its
// output straight into a live conversation). Deliberately does NOT wire into ChatManager / CouncilSessionBehavior
// yet: that seating is Inc 3's job, this increment only proves the identity exists and is correct.

#region

using System;
using System.IO;
using CalradiaRemembers.Logic.Assemblies;
using NpcMemoryService.Core.Models;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Library;

#endregion

namespace CalradiaRemembers.Assemblies
{
   /// <summary>
   ///   Builds the rankless, generated herald that stands in for an other-faction lord at a parley (an
   ///   <see cref="CalradiaRemembers.Logic.Assemblies.CouncilAttendance.Ambassador" /> seat). NEVER a real Hero,
   ///   NEVER persisted: the profile lives only for the session that seats it, same as
   ///   <see cref="CalradiaRemembers.Commoners.CommonsProfileFactory" />'s own transient commoner profiles.
   /// </summary>
   public static class EnvoyFactory
   {
      /// <summary>
      ///   Full path to the one generic herald portrait, following the exact pattern
      ///   <c>BalanceOfPowerViewModel.BackgroundPath</c> uses for its own leather background
      ///   (<c>BasePath.Name/Modules/CalradiaRemembers/GUI/Images/...</c>). The art asset itself (cr_herald.jpg)
      ///   is TO BE PROVIDED by Gabriel; it does not exist on disk yet. This is safe to reference before the
      ///   file exists because <see cref="TaleWorlds.Library.AssetThumbnailCache" />-style consumers in this mod
      ///   never assume a path resolves: <c>RuntimePortraitFactory.TryCreate</c> checks <c>File.Exists</c> first
      ///   and returns null on a miss (cached, so a missing file is not retried every render) rather than
      ///   throwing, and <c>CrRuntimeImageWidget</c> simply shows nothing when its bound Sprite is null. Inc 3
      ///   (the live seat rendering) inherits that same tolerance for free; no extra guard is needed here.
      /// </summary>
      public static string HeraldPortraitPath => Path.Combine(
         CalradiaRemembers.Paths.ModulePaths.Root, "GUI", "Images", "cr_herald.jpg");

      /// <summary>
      ///   Builds one ambassador's synthetic profile from the represented lord and faction an Ambassador seat
      ///   carries (<see cref="CalradiaRemembers.Logic.Assemblies.CouncilAttendanceSeat.RepresentedHero" /> /
      ///   <see cref="CalradiaRemembers.Logic.Assemblies.CouncilAttendanceSeat.RepresentedFaction" />). Null
      ///   faction or null lord returns null rather than throwing, so a caller (Inc 3) can simply skip the seat
      ///   on a bad projection instead of crashing a whole council screen over one candidate.
      ///   <para>
      ///     <see cref="NpcProfile.Id" /> is <c>"cr_envoy_" + faction.StringId</c>: safe against collision with a
      ///     real Hero.StringId because every live hero id is minted by the engine's own object system (never
      ///     prefixed "cr_envoy_"), the same reasoning <see cref="CalradiaRemembers.Commoners.CommonsProfileFactory" />
      ///     relies on for its own "cr_commoner_" ids. Stable per faction (not per call) so a player who reopens
      ///     the same parley within one session addresses the same herald identity rather than a freshly
      ///     randomized one each time; harmless in practice either way since the profile is never persisted.
      ///   </para>
      ///   <para>
      ///     NpcProfile has no dedicated Culture field (checked against the SDK model): the accent/tone the LLM
      ///     voices comes from <see cref="NpcProfile.Faction" /> alone (PromptLore's cultural_traditions.json is
      ///     keyed by CULTURE display name, resolved live from a real Hero elsewhere; a synthetic envoy has none
      ///     to resolve, so it simply reads by faction like every other synthetic profile in this mod).
      ///   </para>
      /// </summary>
      public static NpcProfile Create(Hero representedLord, IFaction faction)
      {
         if (representedLord == null || faction == null) return null;

         try
         {
            string factionName = faction.Name?.ToString();
            string leaderName = representedLord.Name?.ToString();

            return new NpcProfile {
               Id = "cr_envoy_" + faction.StringId,
               Name = EnvoyIdentityPolicy.DisplayName(factionName),
               Clan = "No clan", // the recognized clanless sentinel (PromptBuilder.AppendIdentity): a herald belongs to no house
               Faction = factionName ?? "an unknown realm",
               IsFemale = false, // one generic herald for now, ratified design
               Personality = EnvoyIdentityPolicy.Persona(factionName, leaderName),
               ReputationWithPlayer = 0 // neutral: a herald carries no personal history with the player
            };
         }
         catch (Exception)
         {
            return null;
         }
      }
   }
}
