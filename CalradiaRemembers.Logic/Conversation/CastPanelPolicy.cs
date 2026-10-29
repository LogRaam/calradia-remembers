// Code written by Gabriel Mailhot, 29/10/2026.
// The participants panel in the chat. RaveN017Mod (Nexus PM, 29/10/2026): "I invite people from my clan to join a
// conversation I'm having with a lord, but I find that they often don't interact much at all, or maybe we should add
// an @ system so it's clear who I'm addressing". They were witnesses: a witness speaks only when the model chooses
// to let them react, and the one way to draw them in (speaking their name) was invisible. Gabriel, 29/10/2026: a
// panel opening on the left that lists the active voices and the witnesses, where a click on a witness makes them
// active; the witnesses keep behaving as they do today. This decides what the panel shows and what each row allows;
// the cast itself (ConversationCast) still holds the roles and the cap.

#region

using System.Collections.Generic;
using System.Linq;

#endregion

namespace CalradiaRemembers.Logic.Conversation
{
   /// <summary>Where a row stands in the scene, in the order the panel lists them.</summary>
   public enum CastPanelStanding
   {
      /// <summary>The character the conversation opened on: always speaking, never sent back.</summary>
      Anchor,

      /// <summary>A witness drawn in: speaks every turn until sent back.</summary>
      Active,

      /// <summary>A witness at the edge: reacts now and then, may be drawn in.</summary>
      Witness,

      /// <summary>A bystander with no character behind them (a nameless guard): listed, never drawn in.</summary>
      Bystander
   }

   /// <summary>One person present, as the chat knows them.</summary>
   public sealed class CastPanelMember
   {
      /// <summary>Their hero id; blank for a bystander with no character behind them.</summary>
      public string Id { get; init; }

      /// <summary>The name the player reads.</summary>
      public string Name { get; init; }
   }

   /// <summary>What the panel is built from.</summary>
   public sealed class CastPanelFacts
   {
      /// <summary>The conversation's roles. Null when there is no real anchor hero, and then there is no panel.</summary>
      public ConversationCast Cast { get; init; }

      /// <summary>The anchor's name.</summary>
      public string AnchorName { get; init; }

      /// <summary>Everyone present other than the anchor, in the order they came. Only these are listed: the cast may still remember someone who left.</summary>
      public IReadOnlyList<CastPanelMember> Present { get; init; } = new List<CastPanelMember>();

      /// <summary>Who the player is speaking to now; blank means the anchor.</summary>
      public string AddressedId { get; init; }
   }

   /// <summary>One line of the panel.</summary>
   public sealed class CastPanelRow
   {
      /// <summary>Their hero id (blank for a bystander).</summary>
      public string Id { get; init; }

      /// <summary>The name shown.</summary>
      public string Name { get; init; }

      /// <summary>Anchor, active, witness or bystander.</summary>
      public CastPanelStanding Standing { get; init; }

      /// <summary>The player's next words go to them.</summary>
      public bool IsAddressed { get; init; }

      /// <summary>A click on the name turns the player to them (and draws a witness in when there is room).</summary>
      public bool CanAddress { get; init; }

      /// <summary>A click on a witness's name would draw them in: true only while the table has room.</summary>
      public bool WouldJoin { get; init; }

      /// <summary>An active voice the player may send back among the witnesses.</summary>
      public bool CanStepBack { get; init; }
   }

   /// <summary>Builds the participants panel's rows.</summary>
   public static class CastPanelPolicy
   {
      /// <summary>
      ///   The rows, anchor first, then the active voices, the witnesses and the bystanders, each group in the order
      ///   they came. Empty when there is no cast.
      /// </summary>
      public static IReadOnlyList<CastPanelRow> Rows(CastPanelFacts facts)
      {
         var rows = new List<CastPanelRow>();
         ConversationCast cast = facts?.Cast;
         if (cast == null) return rows;

         bool anchorAddressed = string.IsNullOrEmpty(facts.AddressedId) || facts.AddressedId == cast.AnchorId;
         rows.Add(new CastPanelRow {
            Id = cast.AnchorId, Name = facts.AnchorName ?? "", Standing = CastPanelStanding.Anchor,
            IsAddressed = anchorAddressed, CanAddress = !anchorAddressed
         });

         var others = new List<CastPanelRow>();
         foreach (CastPanelMember m in facts.Present ?? new List<CastPanelMember>())
         {
            if (m == null || (!string.IsNullOrEmpty(m.Id) && m.Id == cast.AnchorId)) continue;

            CastPanelStanding standing = string.IsNullOrEmpty(m.Id) || cast.RoleOf(m.Id) == null
               ? CastPanelStanding.Bystander
               : cast.IsActive(m.Id) ? CastPanelStanding.Active : CastPanelStanding.Witness;
            bool addressed = !string.IsNullOrEmpty(m.Id) && m.Id == facts.AddressedId;

            others.Add(new CastPanelRow {
               Id = m.Id ?? "", Name = m.Name ?? "", Standing = standing, IsAddressed = addressed,
               CanAddress = standing != CastPanelStanding.Bystander && !addressed,
               WouldJoin = standing == CastPanelStanding.Witness && cast.HasRoom,
               CanStepBack = standing == CastPanelStanding.Active
            });
         }

         rows.AddRange(others.Where(r => r.Standing == CastPanelStanding.Active));
         rows.AddRange(others.Where(r => r.Standing == CastPanelStanding.Witness));
         rows.AddRange(others.Where(r => r.Standing == CastPanelStanding.Bystander));

         return rows;
      }
   }
}
