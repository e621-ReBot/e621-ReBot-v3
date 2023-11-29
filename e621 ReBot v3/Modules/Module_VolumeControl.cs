using System;
using System.Runtime.InteropServices;

namespace e621_ReBot_v3
{
    partial class Module_VolumeControl
    {
        [LibraryImport("winmm.dll")]
        public static partial uint waveOutSetVolume(IntPtr hwo, uint dwVolume);

        [LibraryImport("winmm.dll")]
        public static partial uint waveOutGetVolume(IntPtr hwo, ref uint pdwVolume);

        public static int GetApplicationVolume()
        {
            uint vol = 0;
            waveOutGetVolume(IntPtr.Zero, ref vol);
            return (int)((vol & 0xFFFF) / (ushort.MaxValue / 100));
        }
    }
}