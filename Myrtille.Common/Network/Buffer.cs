using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Myrtille.Network
{
    public class DataBuffer<T>
    {
        public int Size { get; set; }
        public int Delay { get; set; }

        private List<T> _data;
        private object _dataLock;

        public delegate void FlushDelegate(List<T> data);
        public FlushDelegate SendBufferData { get; set; }

        private bool _enabled;
        private CancellationTokenSource _timeout;

        public DataBuffer(int size, int delay)
        {
            Size = size;
            Delay = delay;

            _data = new List<T>();
           _dataLock = new object();
        }

        public void Start()
        {
            if (_enabled)
                return;

            _enabled = true;

            _timeout = new CancellationTokenSource();
            Task.Delay(Delay > 0 ? Delay : 1, _timeout.Token).ContinueWith(task =>
            {
                Flush();
            }, TaskContinuationOptions.NotOnCanceled);
        }

        public void Stop()
        {
            _enabled = false;
        }

        public void AddItem(T item)
        {
            lock (_dataLock)
            {
                if (_data.Count >= Size)
                {
                    Flush();
                }
                _data.Add(item);
            }
        }

        public void Flush()
        {
            if (!_enabled)
                return;

            if (_timeout != null)
            {
                _timeout.Cancel();
                _timeout = null;
            }

            lock (_dataLock)
            {
                if (_data.Count > 0)
                {
                    SendBufferData?.Invoke(_data);
                    _data.Clear();
                }
            }

            _timeout = new CancellationTokenSource();
            Task.Delay(Delay > 0 ? Delay : 1, _timeout.Token).ContinueWith(task =>
            {
                Flush();
            }, TaskContinuationOptions.NotOnCanceled);
        }
    }
}