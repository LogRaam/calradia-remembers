// Code written by Gabriel Mailhot, 19/07/2026.
// Turns Bannerlord's eye-colour slider offset (0..1) into the word an NPC uses for it. Extracted from
// FaceColorReader.ClassifyEye, which was private inside the net472 mod and therefore had no test at all:
// a colour gradient is the last thing that should drift unwatched, because nothing crashes when it does.
//
// WHY IT WAS RECALIBRATED. Rhagaea's eyes are amber; the prompt told the model they were green, and the
// model dutifully wrote "her green eyes". Her measured offset is 0.68, which the old table placed inside
// "deep green" (0.60 to 0.72). Gabriel then sampled the slider at 18 evenly spaced points and photographed
// each result, and the samples show the warm end starting EARLIER than the old table assumed: by 0.65 the
// iris is already turning, and at 0.71 it is plainly amber rather than any kind of green.
//
// The boundaries below are read off those 18 samples (offset = n/17, n = 0..17):
//   0.00 ice-pale blue . 0.12-0.18 blue . 0.24-0.29 blue-grey . 0.35 grey . 0.41-0.59 green, deepening
//   0.65-0.71 amber . 0.76-0.88 brown . 0.94-1.00 nearly black
//
// The old table's other fault, visible in the same series: 0.20 to 0.30 was called "deep blue" when the
// samples are plainly greying, and the greens were compressed into 0.44-0.60 when they in fact run from
// about 0.40 to 0.63.

namespace CalradiaRemembers.Logic
{
    /// <summary>
    ///   The words for Bannerlord's eye-colour gradient, banded from the slider offset. TUNE: every
    ///   boundary is read from an 18-point photographic sample rather than derived, so a boundary sitting
    ///   one sample too early or late is a judgement call, not a defect.
    /// </summary>
    public static class EyeColorBands
    {
        /// <summary>
        ///   The descriptor for this offset, or null when the engine could not supply one (negative). Null
        ///   means "say nothing about their eyes", never "guess": an invented colour is exactly the fault
        ///   this class exists to stop.
        /// </summary>
        public static string Describe(float offset)
        {
            if (offset < 0) return null;

            if (offset < 0.05f) return "pale blue";
            if (offset < 0.18f) return "blue";
            if (offset < 0.30f) return "blue-grey";
            if (offset < 0.40f) return "grey";
            if (offset < 0.52f) return "green";
            if (offset < 0.63f) return "deep green";
            if (offset < 0.74f) return "amber";
            if (offset < 0.92f) return "brown";

            return "dark brown";
        }
    }
}
