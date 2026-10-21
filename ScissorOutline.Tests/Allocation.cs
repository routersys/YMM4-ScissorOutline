namespace ScissorOutline.Tests;

internal static class Allocation
{
    const int Rounds = 1000;
    const int Attempts = 16;

    public static long Of(Action work)
    {
        var previous = Window(work);
        for (var attempt = 1; attempt < Attempts; attempt++)
        {
            var current = Window(work);
            if (current == previous)
                return current;

            previous = current;
        }

        return previous;
    }

    static long Window(Action work)
    {
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var round = 0; round < Rounds; round++)
            work();

        return GC.GetAllocatedBytesForCurrentThread() - before;
    }
}
