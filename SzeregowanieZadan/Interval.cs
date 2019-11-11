using System;
using System.Collections.Generic;

namespace SzeregowanieZadan
{
    class Interval
    {
        public int Left { get; set; }
        public int Right { get; set; }

        public Interval(int left, int right)
        {
            Left = left;
            Right = right;
        }

        public int LeftIsContainedIn(Interval right)
        {
            if (Left < right.Left) return -1;
            return Left > right.Right ? 1 : 0;
        }

        public int IsContainedIn(Interval right)
        {
            if (Left < right.Left) return -1;
            return Right <= right.Right ? 0 : 1;
        }
    }
}
