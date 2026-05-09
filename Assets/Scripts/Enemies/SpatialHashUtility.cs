using Unity.Collections;
using Unity.Mathematics;

namespace TMG.Survivors
{
    public struct SpatialHashAndIndex : System.IComparable<SpatialHashAndIndex>
    {
        public int Hash;
        public int Index;

        public int CompareTo(SpatialHashAndIndex other)
        {
            return Hash.CompareTo(other.Hash);
        }
    }

    public static class SpatialHashUtility
    {
        public static int2 GridPosition(float2 position, float cellSize)
        {
            return new int2(math.floor(position / cellSize));
        }

        public static int Hash(int2 gridPosition)
        {
            unchecked
            {
                return gridPosition.x * 73856093 ^ gridPosition.y * 19349663;
            }
        }

        public static int BinarySearchFirst(NativeArray<SpatialHashAndIndex> sortedHashAndIndices, int hash)
        {
            var left = 0;
            var right = sortedHashAndIndices.Length - 1;
            var result = -1;

            while (left <= right)
            {
                var mid = (left + right) / 2;
                var midHash = sortedHashAndIndices[mid].Hash;
                if (midHash == hash)
                {
                    result = mid;
                    right = mid - 1;
                }
                else if (midHash < hash)
                {
                    left = mid + 1;
                }
                else
                {
                    right = mid - 1;
                }
            }

            return result;
        }
    }
}
