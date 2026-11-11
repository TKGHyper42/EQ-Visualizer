namespace EQVisualizer;

internal static class MediaSourceIdentity
{
    public static bool Matches(string candidate, string selected)
    {
        if (string.Equals(candidate, selected, StringComparison.Ordinal)) return true;
        // Store apps may publish their player or launcher identity after a
        // restart; the package family remains the same.
        int candidateSeparator = candidate.IndexOf('!');
        int selectedSeparator = selected.IndexOf('!');
        return candidateSeparator > 0 && selectedSeparator > 0 &&
            candidate.AsSpan(0, candidateSeparator).SequenceEqual(selected.AsSpan(0, selectedSeparator));
    }
}
