using System.Text.Json;

namespace LayoutCycleRepro;

public sealed class ReproItem
{
    public int Index { get; init; }
    public required string Id { get; init; }
    public double Height { get; init; }
    public double DesiredWidth { get; init; }
    public string Label => $"{Index}: {Id}";
}

public sealed class TestCase
{
    public double ViewportWidth { get; init; }
    public double ViewportHeight { get; init; }
    public double DpiScale { get; init; }
    public double ZoomFactor { get; init; }
    public double Spacing { get; init; }
    public double HorizontalGutter { get; init; }
    public double TopPadding { get; init; }
    public double BottomPadding { get; init; }
    public double LeadingRowHeight { get; init; }
    public double RowSpacing { get; init; }
    public double InitialOffset { get; init; }
    public double ObservedStallOffset { get; init; }
    public double HorizontalCacheLength { get; init; }
    public double VerticalCacheLength { get; init; }
    public required ReproItem[] Items { get; init; }

    internal static TestCase Load()
    {
        using var input = File.OpenRead(Path.Combine(AppContext.BaseDirectory, "test-case.json"));
        var capture = JsonSerializer.Deserialize<TestCase>(input,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new InvalidDataException("Missing test case.");
        if (capture.Items.Length != 13 || capture.ZoomFactor != 1
            || !double.IsFinite(capture.ViewportWidth) || capture.ViewportWidth <= 0
            || !double.IsFinite(capture.ViewportHeight) || capture.ViewportHeight <= 0
            || capture.Items.Where((item, index) => item.Index != index
                || !double.IsFinite(item.Height) || item.Height <= 0
                || !double.IsFinite(item.DesiredWidth) || item.DesiredWidth <= 0).Any())
        {
            throw new InvalidDataException("Expected 13 positive element sizes in index order and zoom 1.");
        }
        return capture;
    }
}
