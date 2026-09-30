// Code written by Gabriel Mailhot, 30/09/2026.
// SELF-ESTEEM, increment E1 (ROADMAP, ruled 30/09/2026). Lordfadooboo (Nexus, 29/09/2026): a hidden regard a character
// has for himself, lowered by being humbled and raised by doing well, starting from station and a personal draw, with
// traits deciding how easily it moves. Maifailun: repeated captivity as trauma. Gabriel: build it, and let it and the
// mood of the day make a perceived image of the character.
//
// Ruling 1 (colour, never an outcome): nothing here returns a permission. The value is a baseline (who he is) plus a
// shift (what has befallen him lately) that mends with time, so one defeat never breaks a man for good and a king
// who is humbled still stands higher than a hedge knight who is not. Stored as the SHIFT, not the total, so a man who
// rises in station rises with it.

#region

using System;
using System.Collections.Generic;
using System.Linq;
using NpcMemoryService.Core.Mood;

#endregion

namespace CalradiaRemembers.Logic
{
    /// <summary>What befell a character that moves how he regards himself.</summary>
    public enum SelfEsteemEvent
    {
        BattleLost,
        BattleWon,
        Captured,
        Released,
        TournamentWon,
        TournamentLost,
        FiefLost,
        FiefGained,
        KinDied,
        DuelLostToPlayer,
        DuelWonAgainstPlayer,

        /// <summary>The player lifted his spirits on a day he was low (the mood of the day's "lifted", when shaken or worse).</summary>
        Reassured,

        /// <summary>The player beat or robbed him (player violence).</summary>
        Humiliated
    }

    /// <summary>Who he is, for where he starts.</summary>
    public sealed class SelfEsteemBaselineFacts
    {
        /// <summary>Stature.Station: 0 no house of note, 1 established, 2 great house, 3 ruler.</summary>
        public int Station { get; set; }

        /// <summary>The vanilla Valor trait, -2..2.</summary>
        public int Valor { get; set; }

        /// <summary>The personal draw (<see cref="SelfEsteemPolicy.PersonalDraw" />).</summary>
        public int PersonalDraw { get; set; }
    }

    /// <summary>The traits that decide how easily it moves.</summary>
    public sealed class SelfEsteemTemper
    {
        /// <summary>Valor -2..2: a brave man takes a defeat less to heart.</summary>
        public int Valor { get; set; }

        /// <summary>Calculating -2..2: a calculating mind swings less, both ways.</summary>
        public int Calculating { get; set; }
    }

    /// <summary>What is kept on the character: the shift, the day it was last set, and his recent captivities.</summary>
    public sealed class SelfEsteemState
    {
        /// <summary>How far what has befallen him lately moves him from his baseline.</summary>
        public int Shift { get; set; }

        /// <summary>The day the shift was last set; it mends from there.</summary>
        public int? ShiftDay { get; set; }

        /// <summary>The days he was taken captive, within <see cref="SelfEsteemPolicy.TraumaMemoryDays" />.</summary>
        public List<int> CapturedOnDays { get; set; } = new List<int>();
    }

    /// <summary>Self-esteem's rules. Pure.</summary>
    public static class SelfEsteemPolicy
    {
        /// <summary>How far the personal draw moves a baseline, either way.</summary>
        public const int PersonalDrawRange = 12;

        /// <summary>Days for a shift to mend by one point toward nothing.</summary>
        public const int RecoveryDaysPerPoint = 3;

        /// <summary>How long a captivity counts toward making the next one land harder.</summary>
        public const int TraumaMemoryDays = 180;

        /// <summary>Each captivity still remembered adds this much to the next one's cost.</summary>
        public const int TraumaPerRecentCaptivity = 4;

        /// <summary>The most a single captivity can cost, however many came before.</summary>
        public const int WorstCaptivity = 25;

        /// <summary>At or below this, broken.</summary>
        public const int BrokenAtOrBelow = -45;

        /// <summary>At or below this, shaken.</summary>
        public const int ShakenAtOrBelow = -15;

        /// <summary>From this, assured.</summary>
        public const int AssuredFrom = 15;

        /// <summary>From this, proud.</summary>
        public const int ProudFrom = 45;

        /// <summary>Where a man starts: his station, his valour, and his own draw.</summary>
        public static int Baseline(SelfEsteemBaselineFacts f)
            => f == null ? 0 : Clamp(f.Station * 8 - 4 + f.Valor * 4 + f.PersonalDraw);

        /// <summary>A stable per-character draw in [-PersonalDrawRange, PersonalDrawRange]: the same man every time.</summary>
        public static int PersonalDraw(string heroId)
        {
            unchecked
            {
                uint h = 2166136261u ^ 0xA5A5u;
                foreach (char c in heroId ?? string.Empty)
                {
                    h ^= c;
                    h *= 16777619u;
                }

                h ^= h >> 13;
                h *= 0x5BD1E995u;
                h ^= h >> 15;

                return (int) (h % (uint) (PersonalDrawRange * 2 + 1)) - PersonalDrawRange;
            }
        }

        /// <summary>What one event does to him, with his temper and how many captivities he still remembers.</summary>
        public static int Delta(SelfEsteemEvent e, SelfEsteemTemper temper, int recentCaptivities)
        {
            int raw = e switch {
                SelfEsteemEvent.BattleLost => -8,
                SelfEsteemEvent.BattleWon => 5,
                SelfEsteemEvent.Captured => -Math.Min(WorstCaptivity, 10 + TraumaPerRecentCaptivity * Math.Max(0, recentCaptivities)),
                SelfEsteemEvent.Released => 2,
                SelfEsteemEvent.TournamentWon => 6,
                SelfEsteemEvent.TournamentLost => -2,
                SelfEsteemEvent.FiefLost => -10,
                SelfEsteemEvent.FiefGained => 8,
                SelfEsteemEvent.KinDied => -6,
                SelfEsteemEvent.DuelLostToPlayer => -8,
                SelfEsteemEvent.DuelWonAgainstPlayer => 5,
                SelfEsteemEvent.Reassured => 2,
                SelfEsteemEvent.Humiliated => -8,
                _ => 0
            };

            double scale = 1.0;
            int valor = Math.Max(-2, Math.Min(2, temper?.Valor ?? 0));
            int calculating = Math.Max(0, Math.Min(2, temper?.Calculating ?? 0));
            if (raw < 0) scale *= 1.0 - 0.15 * valor; // the brave take a loss less to heart, the timid more
            scale *= 1.0 - 0.15 * calculating;         // a calculating mind swings less, both ways

            var scaled = (int) Math.Round(raw * scale, MidpointRounding.AwayFromZero);

            return scaled == 0 && raw != 0 ? Math.Sign(raw) : scaled;
        }

        /// <summary>A shift after <paramref name="days" /> left alone: one point per RecoveryDaysPerPoint toward nothing.</summary>
        public static int Decay(int shift, int days)
        {
            if (shift == 0 || days <= 0) return shift;

            int mended = days / RecoveryDaysPerPoint;

            return shift > 0 ? Math.Max(0, shift - mended) : Math.Min(0, shift + mended);
        }

        /// <summary>Records one event: the old shift mends to today first, then the event lands.</summary>
        public static SelfEsteemState Record(SelfEsteemState state, SelfEsteemEvent e, SelfEsteemTemper temper, int today)
        {
            state ??= new SelfEsteemState();
            List<int> captures = (state.CapturedOnDays ?? new List<int>()).Where(d => today - d <= TraumaMemoryDays).ToList();

            int mended = Decay(state.Shift, state.ShiftDay.HasValue ? today - state.ShiftDay.Value : 0);
            int shift = Clamp(mended + Delta(e, temper, captures.Count));
            if (e == SelfEsteemEvent.Captured) captures.Add(today);

            return new SelfEsteemState {Shift = shift, ShiftDay = today, CapturedOnDays = captures};
        }

        /// <summary>How he regards himself today: his baseline and what is left of his shift.</summary>
        public static int Current(int baseline, SelfEsteemState state, int today)
        {
            if (state == null) return Clamp(baseline);

            return Clamp(baseline + Decay(state.Shift, state.ShiftDay.HasValue ? today - state.ShiftDay.Value : 0));
        }

        /// <summary>The word for a value.</summary>
        public static SelfEsteemBand BandOf(int value)
        {
            if (value <= BrokenAtOrBelow) return SelfEsteemBand.Broken;
            if (value <= ShakenAtOrBelow) return SelfEsteemBand.Shaken;
            if (value < AssuredFrom) return SelfEsteemBand.Steady;

            return value < ProudFrom ? SelfEsteemBand.Assured : SelfEsteemBand.Proud;
        }

        private static int Clamp(int v) => Math.Max(-100, Math.Min(100, v));
    }
}
