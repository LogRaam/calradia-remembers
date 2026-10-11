// Code written by Gabriel Mailhot, 11/10/2026.
// Where and how large a tale's picture stands beside the chat (Manuscript skin). Gabriel (08/10/2026) put it large in the
// dark margin, centred beside the chat; then (11/10/2026): "si une image de conte doit apparaitre pendant une
// conversation, il faudrait qu'elle soit plus petite, affichee en haut a gauche de l'ecran pour ne pas masquer le NPC
// anime." So a scene with nobody in it (a tale's exploration scene, narrated) keeps the large picture; any conversation
// with a character gets a small one in the top-left corner, the character left in plain view.

#region

using System;

#endregion

namespace CalradiaRemembers.Logic.AuthoredQuests
{
   /// <summary>The size of a tale's picture beside the chat, kept to its own proportions. Pure.</summary>
   public static class SceneImageLayout
   {
      /// <summary>The box a picture fits in when the scene has no character to keep in view (the margin beside the chat).</summary>
      public const float LargeWidth = 1100f;

      /// <inheritdoc cref="LargeWidth" />
      public const float LargeHeight = 820f;

      /// <summary>TUNE: the box a picture fits in during a conversation, in the top-left corner (Gabriel, 11/10/2026).</summary>
      public const float CompactWidth = 400f;

      /// <inheritdoc cref="CompactWidth" />
      public const float CompactHeight = 300f;

      /// <summary>
      ///   The picture's size: as large as fits its box without distortion; the small box when a character is spoken to
      ///   (<paramref name="compact" />), the large one otherwise. A picture whose proportions are unknown is taken as 4:3.
      /// </summary>
      public static (float Width, float Height) Fit(float aspectW, float aspectH, bool compact)
      {
         if (aspectW <= 0 || aspectH <= 0)
         {
            aspectW = 4f;
            aspectH = 3f;
         }

         float boxW = compact ? CompactWidth : LargeWidth;
         float boxH = compact ? CompactHeight : LargeHeight;
         float fit = Math.Min(boxW / aspectW, boxH / aspectH);

         return (aspectW * fit, aspectH * fit);
      }
   }
}
