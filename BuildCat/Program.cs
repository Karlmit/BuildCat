namespace BuildCat;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        using var singleInstance = new Mutex(initiallyOwned: true, "Local\\BuildCat", out var createdNew);
        if (!createdNew)
        {
            return;
        }

        ApplicationConfiguration.Initialize();
        Application.Run(new BuildCatApplicationContext());
        GC.KeepAlive(singleInstance);
    }
}
