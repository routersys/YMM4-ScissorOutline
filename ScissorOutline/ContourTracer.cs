using System.Numerics;

namespace ScissorOutline;

internal sealed class ContourTracer
{
    private static readonly int[] StepX = [1, 0, -1, 0];
    private static readonly int[] StepY = [0, 1, 0, -1];

    private byte[] _corners = [];

    public void Trace(ReadOnlySpan<ulong> inside, int width, int height, ContourLoops loops)
    {
        loops.Clear();
        var stride = width + 1;
        var size = stride * (height + 1);
        if (_corners.Length < size)
            _corners = new byte[size];
        var corners = _corners.AsSpan(0, size);
        corners.Clear();
        MarkEdges(inside, width, height, stride, corners);

        var start = 0;
        while (true)
        {
            var next = corners.Slice(start).IndexOfAnyExcept((byte)0);
            if (next < 0)
                break;

            start += next;
            while (corners[start] != 0)
                TraceLoop(corners, stride, start, loops);
        }
    }

    private static void MarkEdges(ReadOnlySpan<ulong> inside, int width, int height, int stride, Span<byte> corners)
    {
        var wordsPerRow = BitGrid.WordsPerRow(width);
        for (var y = 0; y < height; y++)
        {
            for (var word = 0; word < wordsPerRow; word++)
            {
                var current = inside[y * wordsPerRow + word];
                if (current == 0)
                    continue;

                var above = y > 0 ? inside[(y - 1) * wordsPerRow + word] : 0UL;
                var below = y < height - 1 ? inside[(y + 1) * wordsPerRow + word] : 0UL;
                var previous = word > 0 ? inside[y * wordsPerRow + word - 1] >> 63 : 0UL;
                var next = word < wordsPerRow - 1 ? inside[y * wordsPerRow + word + 1] << 63 : 0UL;
                Mark(corners, current & ~above, word, y * stride, 0);
                Mark(corners, current & ~((current >> 1) | next), word, y * stride + 1, 1);
                Mark(corners, current & ~below, word, (y + 1) * stride + 1, 2);
                Mark(corners, current & ~((current << 1) | previous), word, (y + 1) * stride, 3);
            }
        }
    }

    private static void Mark(Span<byte> corners, ulong edges, int word, int rowOffset, int direction)
    {
        var bit = (byte)(1 << direction);
        while (edges != 0)
        {
            var x = (word << 6) + BitOperations.TrailingZeroCount(edges);
            corners[rowOffset + x] |= bit;
            edges &= edges - 1;
        }
    }

    private static void TraceLoop(Span<byte> corners, int stride, int start, ContourLoops loops)
    {
        var firstDirection = BitOperations.TrailingZeroCount((int)corners[start]);
        corners[start] &= (byte)~(1 << firstDirection);
        var x = start % stride;
        var y = start / stride;
        var direction = firstDirection;
        while (true)
        {
            x += StepX[direction];
            y += StepY[direction];
            var index = y * stride + x;
            var next = Pick(corners[index], direction);
            if (next < 0)
            {
                if (direction != firstDirection)
                    loops.AddPoint(x, y);
                break;
            }

            if (next != direction)
                loops.AddPoint(x, y);
            corners[index] &= (byte)~(1 << next);
            direction = next;
        }

        loops.CloseLoop();
    }

    private static int Pick(byte mask, int direction)
    {
        var right = (direction + 1) & 3;
        if ((mask & (1 << right)) != 0)
            return right;
        if ((mask & (1 << direction)) != 0)
            return direction;
        var left = (direction + 3) & 3;
        if ((mask & (1 << left)) != 0)
            return left;
        return -1;
    }
}
