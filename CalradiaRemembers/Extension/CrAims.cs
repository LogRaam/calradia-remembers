// Code written by Gabriel Mailhot, 20/10/2026.
// Extension Surface: lords with aims of their own. tashmetu (Nexus, 14/10/2026), from two of his testers: "in Calradia,
// nobody takes the first step with the player." Ruling (Gabriel, 14/10/2026): another mod says WHAT a lord wants; Calradia
// Remembers decides WHEN it surfaces and remembers that it did. The pure core and the conversation glue were built on
// 14-15/10 (Logic/Aims, Aims/AimsBehavior) and the door was proposed in ROADMAP; Gabriel approved it as proposed
// (20/10/2026). This is that door, over the same book CR's own aims (the vassal war duty) already use.

#region

using System;
using System.Collections.Generic;
using System.Linq;
using CalradiaRemembers.Aims;
using CalradiaRemembers.Logic.Aims;
using TaleWorlds.CampaignSystem;

#endregion

namespace CalradiaRemembers
{
   /// <summary>What became of an aim handed to <see cref="CrAims.Register" />.</summary>
   public enum AimResult
   {
      /// <summary>The aim now stands.</summary>
      Accepted,

      /// <summary>An aim of yours with the same id stood and has been replaced; whether it was raised is kept.</summary>
      Updated,

      /// <summary>The id, the lord or the source tag was missing.</summary>
      MissingIdentity,

      /// <summary>The want was empty.</summary>
      EmptyWant,

      /// <summary>No living hero has this NpcId, or no campaign is loaded.</summary>
      UnknownHero,

      /// <summary>The lord already holds another aim (any source): retract yours first, or wait.</summary>
      LordAlreadyHasAnAim,

      /// <summary>Your mod already holds the most standing aims a source may (40).</summary>
      SourceAtCapacity,

      /// <summary>Its last day is already past.</summary>
      AlreadyExpired,

      /// <summary>The source tag is Calradia Remembers' own: choose your mod's name.</summary>
      ReservedSource
   }

   /// <summary>A lord's aim of his own, as another mod hands it to <see cref="CrAims.Register" />. Plain values only.</summary>
   public sealed class ExternalAim
   {
      /// <summary>Your stable id for this aim: update and retraction go by it, with <see cref="SourceMod" />.</summary>
      public string Id { get; set; }

      /// <summary>Your mod's name.</summary>
      public string SourceMod { get; set; }

      /// <summary>The lord's hero StringId.</summary>
      public string NpcId { get; set; }

      /// <summary>The want itself, in the lord's own terms (at most 400 characters).</summary>
      public string Want { get; set; }

      /// <summary>One line on what the player has to do with it (at most 200 characters). May be empty.</summary>
      public string PlayerPart { get; set; }

      /// <summary>
      ///   Optional: a <see cref="MissionBoard" /> mission id. With one, the service the lord asks for is a real, verified
      ///   deed and he may ask for it outright; without, he may only sound the player out and promises nothing.
      /// </summary>
      public string MissionId { get; set; }

      /// <summary>
      ///   Where it may surface: "conversation", "letter", "audience". Empty means all. Today only conversation carries an
      ///   aim of another mod; letters and audiences will follow without a change on your side.
      /// </summary>
      public List<string> Channels { get; set; } = new();

      /// <summary>Optional: the last game day it stands. Null: until you retract it.</summary>
      public int? ExpiresOnDay { get; set; }
   }

   /// <summary>A lord was given the floor for an aim (<see cref="CrAims.Raised" />).</summary>
   public sealed class CrAimRaised
   {
      /// <summary>Your id for the aim.</summary>
      public string AimId { get; set; }

      /// <summary>The source that registered it.</summary>
      public string SourceMod { get; set; }

      /// <summary>The lord's hero StringId.</summary>
      public string NpcId { get; set; }

      /// <summary>"conversation", "letter" or "audience".</summary>
      public string Channel { get; set; }

      /// <summary>The game day.</summary>
      public int GameDay { get; set; }
   }

   /// <summary>The door through which other mods give lords aims of their own.</summary>
   public static class CrAims
   {
      /// <summary>
      ///   Gives a lord an aim. CR decides when it surfaces: in a conversation the player opens, the lord has leave to bring
      ///   it up himself, at most twice (the second only after 60 days while it still stands), never toward an enemy or in
      ///   captivity, and always as himself (an honest lord asks openly, a calculating one keeps his purpose close, and
      ///   neither ever states a falsehood). One aim per lord across all mods. Persisted with the save.
      /// </summary>
      public static AimResult Register(ExternalAim aim)
      {
         AimsBehavior behavior = AimsBehavior.Instance;
         if (aim == null || string.IsNullOrWhiteSpace(aim.Id) || string.IsNullOrWhiteSpace(aim.NpcId) || string.IsNullOrWhiteSpace(aim.SourceMod))
            return AimResult.MissingIdentity;
         if (IsReserved(aim.SourceMod)) return AimResult.ReservedSource;
         if (behavior == null || !Mapping.HeroPresence.IsInTheWorld(Hero.FindFirst(h => h.StringId == aim.NpcId))) return AimResult.UnknownHero;

         AimAdmission admission = behavior.Register(new StandingAim
         {
            Id = aim.Id.Trim(), SourceMod = aim.SourceMod.Trim(), NpcId = aim.NpcId, Want = aim.Want, PlayerPart = aim.PlayerPart,
            MissionId = aim.MissionId, Channels = ChannelsOf(aim.Channels), ExpiresOnDay = aim.ExpiresOnDay
         });

         return admission switch
         {
            AimAdmission.Accepted => AimResult.Accepted,
            AimAdmission.Updated => AimResult.Updated,
            AimAdmission.MissingIdentity => AimResult.MissingIdentity,
            AimAdmission.EmptyWant => AimResult.EmptyWant,
            AimAdmission.LordAlreadyHasAnAim => AimResult.LordAlreadyHasAnAim,
            AimAdmission.SourceAtCapacity => AimResult.SourceAtCapacity,
            _ => AimResult.AlreadyExpired
         };
      }

      /// <summary>Takes back one of your aims. False when none of yours has this id.</summary>
      public static bool Retract(string sourceMod, string id)
         => !IsReserved(sourceMod) && AimsBehavior.Instance?.Retract(sourceMod?.Trim(), id?.Trim()) == true;

      /// <summary>
      ///   Your mod's aims CR still holds, as copies (changing them changes nothing; Register again with the same id to
      ///   update, Retract to take one back). Channels lists the ways each may surface ("conversation", "letter",
      ///   "audience"; empty when none). An aim that expired or was retracted is gone. Empty for a blank or reserved source
      ///   or outside a campaign. Call it on the game thread. Since 2.8.10 (Bhelogan, Discord, 14/11/2026).
      /// </summary>
      public static IReadOnlyList<ExternalAim> List(string sourceMod)
      {
         AimsBehavior behavior = AimsBehavior.Instance;
         if (behavior == null || string.IsNullOrWhiteSpace(sourceMod) || IsReserved(sourceMod)) return new List<ExternalAim>();

         string source = sourceMod.Trim();

         return behavior.Book.All.ToList()
                        .Where(a => a != null && string.Equals(a.SourceMod, source, StringComparison.OrdinalIgnoreCase))
                        .Select(a => new ExternalAim {
                           Id = a.Id, SourceMod = a.SourceMod, NpcId = a.NpcId, Want = a.Want, PlayerPart = a.PlayerPart, MissionId = a.MissionId,
                           Channels = NamesOf(a.Channels), ExpiresOnDay = a.ExpiresOnDay
                        })
                        .ToList();
      }

      /// <summary>
      ///   A lord was given the floor for an aim: the conversation opened with his leave to raise it (a letter released, an
      ///   audience granted, once those channels carry aims). It means he was GIVEN the floor, not that he said it: in a
      ///   conversation he may judge the moment wrong and stay silent.
      /// </summary>
      public static event Action<CrAimRaised> Raised;

      // AimsBehavior's own notice, connected by the submodule at load. CR's own aims are not announced.
      internal static void OnRaised(StandingAim aim, AimChannels channel)
      {
         if (aim == null || IsReserved(aim.SourceMod) || Raised == null) return;

         var info = new CrAimRaised
         {
            AimId = aim.Id, SourceMod = aim.SourceMod, NpcId = aim.NpcId, Channel = channel.ToString().ToLowerInvariant(),
            GameDay = (int) CampaignTime.Now.ToDays
         };
         foreach (Delegate d in Raised.GetInvocationList())
            try { ((Action<CrAimRaised>) d)(info); }
            catch (Exception ex) { Foundation.ModLog.Error($"[AIMS] a CrAims.Raised subscriber threw: {ex.GetType().Name}: {ex.Message}"); }
      }

      private static bool IsReserved(string sourceMod)
         => string.Equals(sourceMod?.Trim(), AimsBehavior.CrSource, StringComparison.OrdinalIgnoreCase);

      private static List<string> NamesOf(AimChannels channels)
      {
         var names = new List<string>();
         if ((channels & AimChannels.Conversation) != 0) names.Add("conversation");
         if ((channels & AimChannels.Letter) != 0) names.Add("letter");
         if ((channels & AimChannels.Audience) != 0) names.Add("audience");

         return names;
      }

      private static AimChannels ChannelsOf(List<string> names)
      {
         if (names == null || names.All(string.IsNullOrWhiteSpace)) return AimChannels.All;

         AimChannels c = AimChannels.None;
         foreach (string n in names)
            c |= (n ?? "").Trim().ToLowerInvariant() switch
            {
               "conversation" => AimChannels.Conversation,
               "letter" => AimChannels.Letter,
               "audience" => AimChannels.Audience,
               _ => AimChannels.None
            };

         return c;
      }
   }
}
