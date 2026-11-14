namespace EQVisualizer;

internal static class WallpaperRecovery
{
    public static string? Choose(string current, string ours, string? saved, IEnumerable<string> history, Func<string, bool> exists)
    {
        static bool Equal(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
        if (!Equal(current, ours)) return null;
        bool Valid(string? path) => !string.IsNullOrWhiteSpace(path) && !Equal(path, ours) && exists(path);
        if (saved == "" || Valid(saved)) return saved;
        return history.FirstOrDefault(Valid);
    }
}
