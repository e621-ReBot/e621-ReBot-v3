using System;
using System.Windows;
using System.Windows.Controls;
using CefSharp.DevTools.CSS;

namespace e621_ReBot_v3.CustomControls
{
    public partial class VolumeSlider : UserControl
    {
        public VolumeSlider()
        {
            InitializeComponent();
        }

        private void VolumeSliderX_Loaded(object sender, RoutedEventArgs e)
        {
            VolumeSliderX.Value = Module_VolumeControl.GetApplicationVolume();
            Loaded -= VolumeSliderX_Loaded; //Changing back to tab page triggers load events
        }

        private void VolumeSliderX_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            uint vol = (uint)(ushort.MaxValue / 100 * VolumeSliderX.Value);
            Module_VolumeControl.waveOutSetVolume(IntPtr.Zero, (vol & 0xFFFF) | (vol << 16));
            AppSettings.Volume = (ushort)VolumeSliderX.Value;
        }

        internal void SetVolume(ushort Volume) 
        {
            VolumeSliderX.Value = Volume;
        }
    }
}