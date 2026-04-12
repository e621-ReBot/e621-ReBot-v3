using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace e621_ReBot_v3.CustomControls
{
    internal class ProgressStream : Stream
    {
        private readonly Stream _innerStream;
        private readonly int _chunkSize;
        private readonly long _totalSize;

        public ProgressStream(Stream inner, int chunkSize = 65536)
        {
            _innerStream = inner;
            _chunkSize = chunkSize;
            _totalSize = inner.Length;
        }

        private long _totalRead = 0;
        private DateTime _lastReport = DateTime.MinValue;
        public event Action<int>? ProgressChanged;
        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            // Force smaller reads
            int read = await _innerStream.ReadAsync(buffer, offset, Math.Min(count, _chunkSize), cancellationToken);

            if (read > 0)
            {
                _totalRead += read;

                DateTime now = DateTime.UtcNow;
                if ((now - _lastReport).TotalMilliseconds >= 200)
                {
                    _lastReport = now;

                    int percent = (int)((_totalRead * 100L) / _totalSize);
                    ProgressChanged?.Invoke(percent);
                }
            }

            return read;
        }

        public override bool CanRead => _innerStream.CanRead;

        public override bool CanSeek => _innerStream.CanSeek;

        public override bool CanWrite => _innerStream.CanWrite;

        public override long Length => _innerStream.Length;

        public override long Position { get => _innerStream.Position; set => _innerStream.Position = value; }

        public override void Flush()
        {
            _innerStream.Flush();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            return _innerStream.Read(buffer, offset, count);
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            return _innerStream.Seek(offset, origin);
        }

        public override void SetLength(long value)
        {
            _innerStream.SetLength(value);
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            _innerStream.Write(buffer, offset, count);
        }

        protected override void Dispose(bool disposing)
        {
            int percent = (int)((_totalRead * 100L) / _totalSize);
            ProgressChanged?.Invoke(percent);
            base.Dispose(disposing);
        }
    }
}
