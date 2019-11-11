using System;
using System.Collections.Generic;

namespace SzeregowanieZadan
{
    public class Machine
    {
        private SortedSet<Interval> _freeIntervals;

        public Machine()
        {
            _freeIntervals = new SortedSet<Interval>(new FreeComparer());
            _freeIntervals.Add(new Interval(0, int.MaxValue));
        }

        public virtual bool AddTask(int start, int end)
        {
            if (!_freeIntervals.TryGetValue(new Interval(start, end), out var x)) return false;
            var leftInterval = new Interval(x.Left, start-1);
            var rightInterval = new Interval(end+1, x.Right);
            _freeIntervals.Remove(x);
            if(leftInterval.Left <= leftInterval.Right)
                _freeIntervals.Add(leftInterval);
            if(rightInterval.Right >= rightInterval.Left)
                _freeIntervals.Add(rightInterval);
            return true;
        }

        public int FirstFree(int start, int end)
        {
            var diff = end - start;
            var interval = new Interval(start, end);
            foreach (var freeInterval in _freeIntervals)
            {
                if (freeInterval.Left > interval.Left)
                {
                    interval.Left = freeInterval.Left;
                    interval.Right = interval.Left + diff;
                }
                if (interval.IsContainedIn(freeInterval) == 0) return interval.Left;
            }
            throw new Exception("Coś się popsuło");
        }

        public void ClearFree()
        {
            _freeIntervals.Clear();
            _freeIntervals.Add(new Interval(0, int.MaxValue));
        }
    }
}
