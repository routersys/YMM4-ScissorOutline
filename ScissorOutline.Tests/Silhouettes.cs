namespace ScissorOutline.Tests;

internal static class Silhouettes
{
    public static byte[] Empty(int width, int height) => new byte[width * height];

    public static byte[] Disc(int width, int height, double centerX, double centerY, double radius)
        => Fill(width, height, (x, y) => Math.Sqrt((x + 0.5 - centerX) * (x + 0.5 - centerX) + (y + 0.5 - centerY) * (y + 0.5 - centerY)) <= radius);

    public static byte[] Ring(int width, int height, double centerX, double centerY, double outer, double inner)
        => Fill(width, height, (x, y) =>
        {
            var distance = Math.Sqrt((x + 0.5 - centerX) * (x + 0.5 - centerX) + (y + 0.5 - centerY) * (y + 0.5 - centerY));
            return distance <= outer && distance >= inner;
        });

    public static byte[] Strokes(int width, int height)
        => Fill(width, height, (x, y) =>
            (x >= 20 && x < 23 && y >= 20 && y < 90)
            || (x >= 60 && x < 63 && y >= 20 && y < 90)
            || (x >= 20 && x < 63 && y >= 54 && y < 57)
            || (Math.Abs(x - y - 40) <= 1 && x >= 80 && x < 150 && y >= 30 && y < 100)
            || (x == 120 && y >= 110 && y < 140));

    public static byte[] Blobs(int width, int height, int seed, int count)
    {
        var state = (uint)seed * 2654435761u + 777u;
        var discs = new (double X, double Y, double Radius)[count];
        for (var i = 0; i < count; i++)
        {
            state = state * 1664525u + 1013904223u;
            var x = (state >> 8) % (uint)width;
            state = state * 1664525u + 1013904223u;
            var y = (state >> 8) % (uint)height;
            state = state * 1664525u + 1013904223u;
            var radius = 4 + (state >> 8) % 18;
            discs[i] = (x, y, radius);
        }

        return Fill(width, height, (x, y) =>
        {
            foreach (var (cx, cy, radius) in discs)
            {
                if ((x + 0.5 - cx) * (x + 0.5 - cx) + (y + 0.5 - cy) * (y + 0.5 - cy) <= radius * radius)
                    return true;
            }

            return false;
        });
    }

    public static byte[] Fill(int width, int height, Func<int, int, bool> inside)
    {
        var alpha = new byte[width * height];
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
                alpha[y * width + x] = inside(x, y) ? byte.MaxValue : (byte)0;
        }

        return alpha;
    }

    public static IEnumerable<(double X, double Y)> Centers(byte[] alpha, int width, int height)
    {
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                if (alpha[y * width + x] > ScissorOutlineSettings.AlphaThreshold)
                    yield return (x + 0.5, y + 0.5);
            }
        }
    }
}
