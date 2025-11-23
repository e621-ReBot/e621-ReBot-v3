using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace e621_ReBot_v3.CustomControls
{
    public partial class UgoiraPlayer : UserControl
    {
        private Image? _image;
        private List<WriteableBitmap>? _frames;
        private List<int>? _delays;
        private int _currentFrame = 0;
        private bool _isPlaying = false;

        public UgoiraPlayer()
        {
            InitializeComponent();
        }

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            _image = FrameImage;
        }

        public bool _Loaded = false;
        internal void LoadUgoira(List<WriteableBitmap> FrameFiles, List<int> FrameTimes)
        {
            _frames = FrameFiles;
            _delays = FrameTimes;

            _currentFrame = 0;
            _image.Source = _frames[_currentFrame];

            _Loaded = true;

            Play();
        }

        internal void UnloadUgoira()
        {
            Stop();
            _frames.Clear();
            _delays.Clear();
            FrameImage.Source = null;
            _Loaded = false;
        }

        private double _LastRenderTime = 0;
        private void CompositionTarget_Rendering(object? sender, EventArgs e)
        {
            RenderingEventArgs renderingEventArgs = (RenderingEventArgs)e;
            double deltams = renderingEventArgs.RenderingTime.TotalMilliseconds - _LastRenderTime;

            if (deltams >= _delays[_currentFrame])
            {
                _LastRenderTime = renderingEventArgs.RenderingTime.TotalMilliseconds;
                _image.Source = _frames[_currentFrame];
                _currentFrame++;
                if (_currentFrame >= _frames.Count) _currentFrame = 0;
            }
        }

        private void Play()
        {
            if (!_isPlaying)
            {
                CompositionTarget.Rendering += CompositionTarget_Rendering; ;
                _isPlaying = true;
            }
            PlayButton.IsEnabled = false;
            StopButton.IsEnabled = true;
        }

        private void Play_Click(object sender, RoutedEventArgs e)
        {
            Play();
        }

        private void Stop()
        {
            if (_isPlaying)
            {
                CompositionTarget.Rendering -= CompositionTarget_Rendering;
                _isPlaying = false;
            }
            PlayButton.IsEnabled = true;
            StopButton.IsEnabled = false;
        }

        private void Stop_Click(object sender, RoutedEventArgs e)
        {
            Stop();
        }

        private void Previous_Click(object sender, RoutedEventArgs e)
        {
            Stop();

            _currentFrame--;
            if (_currentFrame < 0) _currentFrame = _frames.Count - 1;

            _image.Source = _frames[_currentFrame];
        }

        private void Next_Click(object sender, RoutedEventArgs e)
        {
            Stop();

            _currentFrame++;
            if (_currentFrame >= _frames.Count) _currentFrame = 0;

            _image.Source = _frames[_currentFrame];
        }
    }
}