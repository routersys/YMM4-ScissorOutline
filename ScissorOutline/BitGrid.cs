namespace ScissorOutline;

internal static class BitGrid
{
    public static int WordsPerRow(int width) => (width + 63) >> 6;

    public static bool Get(ReadOnlySpan<ulong> bits, int wordsPerRow, int x, int y)
        => ((bits[y * wordsPerRow + (x >> 6)] >> (x & 63)) & 1UL) != 0;

    public static void Set(Span<ulong> bits, int wordsPerRow, int x, int y)
        => bits[y * wordsPerRow + (x >> 6)] |= 1UL << (x & 63);
}
