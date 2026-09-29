// Code written by Gabriel Mailhot, 05/07/2026.
// A runtime VRAM read, to settle empirically whether the chat's render-targets drive the
// create_render_texture crash or whether the GPU-memory ceiling is falsely low (a known Bannerlord bug).

#region

using System;
using System.Diagnostics;

#endregion

namespace CalradiaRemembers.Diagnostics
{
    /// <summary>
    ///   Samples GPU memory usage at runtime via the Windows GPU performance counters. Reports this process's
    ///   (the game's) dedicated + shared usage and the whole system's dedicated usage, in MB. Managed API only —
    ///   no COM/DXGI vtable interop — and fully guarded: any failure disables the probe and reports "n/a" rather
    ///   than ever disturbing the game. Read once per LLM turn so a VRAM climb can be lined up against the chat
    ///   resources created that turn.
    /// </summary>
    public static class VramProbe
    {
        private const string ProcessMemoryCategory = "GPU Process Memory";
        private const string AdapterMemoryCategory = "GPU Adapter Memory";
        private const string DedicatedUsageCounter = "Dedicated Usage";
        private const string SharedUsageCounter = "Shared Usage";

        private static bool _disabled;
        private static readonly int Pid = SafePid();

        /// <summary>
        ///   One line of GPU-memory state, or a clear "n/a" reason. Never throws. Once a read fails, the probe
        ///   stays disabled for the session so it costs nothing on the hot path afterwards.
        /// </summary>
        public static string Describe()
        {
            if (_disabled) return "VRAM n/a (probe disabled)";

            try
            {
                if (!PerformanceCounterCategory.Exists(ProcessMemoryCategory))
                {
                    _disabled = true;

                    return "VRAM n/a (no 'GPU Process Memory' counter — locale or OS; DXGI fallback needed)";
                }

                long procDedicated = SumInstances(ProcessMemoryCategory, DedicatedUsageCounter, forThisProcessOnly: true);
                long procShared    = SumInstances(ProcessMemoryCategory, SharedUsageCounter, forThisProcessOnly: true);
                long sysDedicated  = PerformanceCounterCategory.Exists(AdapterMemoryCategory)
                    ? SumInstances(AdapterMemoryCategory, DedicatedUsageCounter, forThisProcessOnly: false)
                    : -1;

                string system = sysDedicated < 0 ? "n/a" : $"{Mb(sysDedicated)}MB";

                return $"game dedicated={Mb(procDedicated)}MB shared={Mb(procShared)}MB | system dedicated={system}";
            }
            catch (Exception ex)
            {
                _disabled = true;

                return $"VRAM n/a ({ex.GetType().Name}: {ex.Message})";
            }
        }

        /// <summary>
        ///   The same read as <see cref="Describe" />, as numbers for the balance survey (29/09/2026): the game's dedicated
        ///   and shared memory and the machine's dedicated memory, in MB, -1 for each the probe cannot read. Never throws.
        /// </summary>
        public static (long GameDedicatedMb, long GameSharedMb, long SystemDedicatedMb) Read()
        {
            if (_disabled) return (-1, -1, -1);

            try
            {
                if (!PerformanceCounterCategory.Exists(ProcessMemoryCategory))
                {
                    _disabled = true;

                    return (-1, -1, -1);
                }

                long system = PerformanceCounterCategory.Exists(AdapterMemoryCategory)
                    ? Mb(SumInstances(AdapterMemoryCategory, DedicatedUsageCounter, forThisProcessOnly: false))
                    : -1;

                return (Mb(SumInstances(ProcessMemoryCategory, DedicatedUsageCounter, forThisProcessOnly: true)),
                        Mb(SumInstances(ProcessMemoryCategory, SharedUsageCounter, forThisProcessOnly: true)),
                        system);
            }
            catch
            {
                _disabled = true;

                return (-1, -1, -1);
            }
        }

        /// <summary>
        ///   Sums a raw byte counter across GPU-memory instances. When <paramref name="forThisProcessOnly" /> is
        ///   set, only instances named for this process id are counted (the game's own usage); otherwise every
        ///   instance is summed (the whole adapter's usage).
        /// </summary>
        private static long SumInstances(string category, string counter, bool forThisProcessOnly)
        {
            PerformanceCounterCategory cat = new PerformanceCounterCategory(category);
            string pidTag = $"pid_{Pid}_";
            long sum = 0;

            foreach (string instance in cat.GetInstanceNames())
            {
                if (forThisProcessOnly && instance.IndexOf(pidTag, StringComparison.OrdinalIgnoreCase) < 0) continue;

                try
                {
                    using (PerformanceCounter pc = new PerformanceCounter(category, counter, instance, readOnly: true))
                        sum += pc.RawValue; // a snapshot byte count — no NextValue() sampling needed
                }
                catch
                {
                    // an instance can vanish between enumeration and read; skip it
                }
            }

            return sum;
        }

        private static long Mb(long bytes) => bytes / (1024L * 1024L);

        private static int SafePid()
        {
            try { return Process.GetCurrentProcess().Id; }
            catch { return -1; }
        }
    }
}
