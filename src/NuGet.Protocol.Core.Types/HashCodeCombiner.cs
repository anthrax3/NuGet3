﻿namespace NuGet.Protocol.Core.Types
{
    /// <summary>
    /// Hash code creator, based on the original NuGet hash code combiner/ASP hash code combiner implementations, and then slightly improved for usage in C# 6 Expression-bodied members
    /// </summary>
    internal sealed class HashCodeCombiner
    {
        // seed from String.GetHashCode()
        private const long Seed = 0x1505L;

        private long _combinedHash;

        private HashCodeCombiner()
        {
            _combinedHash = Seed;
        }

        internal static HashCodeCombiner Start()
        {
            return new HashCodeCombiner();
        }

        internal int CombinedHash
        {
            get { return _combinedHash.GetHashCode(); }
        }

        public static implicit operator int (HashCodeCombiner combiner)
        {
            return combiner.CombinedHash;
        }

        internal HashCodeCombiner AddInt32(int i)
        {
            _combinedHash = ((_combinedHash << 5) + _combinedHash) ^ i;
            return this;
        }

        internal HashCodeCombiner AddObject(int i)
        {
            AddInt32(i);
            return this;
        }

        internal HashCodeCombiner AddObject(bool b)
        {
            AddInt32(b.GetHashCode());
            return this;
        }

        internal HashCodeCombiner AddObject(object o)
        {
            if (o != null)
            {
                AddInt32(o.GetHashCode());
            }
            return this;
        }

        /// <summary>
        /// Create a unique hash code for the given set of items
        /// </summary>
        internal static int GetHashCode(params object[] objects)
        {
            HashCodeCombiner combiner = new HashCodeCombiner();

            foreach (object obj in objects)
            {
                combiner.AddObject(obj);
            }

            return combiner.CombinedHash;

        }
    }
}