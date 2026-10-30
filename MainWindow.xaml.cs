using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;

using System.Diagnostics;
using System.Runtime.CompilerServices;

using global::Windows.UI.ViewManagement;

namespace LayoutCycleRepro;

public sealed partial class MainWindow : Window
{
    private static MainWindow? _active;
    private readonly TestCase _case;
    private readonly UISettings _settings = new();
    private readonly Queue<(long Sequence, string Text)> _tail = new(1024);
    private (long Sequence, string Text)[] _beforeScrollToTop = [];
    private readonly Dictionary<UIElement, int> _realized = [];
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private long _sequence;
    private string _phase = "history";

    public MainWindow(TestCase capture)
    {
        _case = capture;
        InitializeComponent();
        _active = this;
        Closed += (_, _) => _active = null;
        CaseScrollView.Width = capture.ViewportWidth;
        CaseScrollView.Height = capture.ViewportHeight;
        PageContentContainer.Padding = new Thickness(capture.HorizontalGutter, 0, capture.HorizontalGutter, 0);
        PagePadding.Padding = new Thickness(0, capture.TopPadding, 0, capture.BottomPadding);
        PageContent.RowSpacing = capture.RowSpacing;
        LeadingRow.Height = capture.LeadingRowHeight;
        CaseLayout.Spacing = capture.Spacing;
        CaseRepeater.HorizontalCacheLength = capture.HorizontalCacheLength;
        CaseRepeater.VerticalCacheLength = capture.VerticalCacheLength;
        CaseRepeater.ElementPrepared += OnPrepared;
        CaseRepeater.ElementClearing += OnClearing;
        CaseRepeater.EffectiveViewportChanged += (_, args) => Record("EffectiveViewport", FormattableString.Invariant(
            $"Rect={args.EffectiveViewport.X:R},{args.EffectiveViewport.Y:R},{args.EffectiveViewport.Width:R},{args.EffectiveViewport.Height:R} {Snapshot()}"));
        CaseRepeater.SizeChanged += (_, _) => Record("Repeater.SizeChanged", Snapshot());
        CaseScrollView.ExtentChanged += (_, _) => Record("ExtentChanged", Snapshot());
        CaseScrollView.ViewChanged += (_, _) => Record("ViewChanged", Snapshot());
        CaseScrollView.StateChanged += (_, _) => Record("StateChanged", Snapshot());
        CaseScrollView.AddHandler(UIElement.PointerWheelChangedEvent,
            new PointerEventHandler(OnPointerWheelChanged), true);
        CaseScrollView.ScrollAnimationStarting += (_, args) => Record("AnimationStarting", FormattableString.Invariant(
            $"CorrelationId={args.CorrelationId} Start={args.StartPosition.X:R},{args.StartPosition.Y:R} End={args.EndPosition.X:R},{args.EndPosition.Y:R}"));
        CaseScrollView.ScrollCompleted += (_, args) =>
        {
            Record("ScrollCompleted", $"CorrelationId={args.CorrelationId} {Snapshot()}");
            DumpCapture();
            _phase = "history";
        };
        CaseRepeater.ItemsSource = capture.Items;
        Root.Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs args)
    {
        Instructions.Text = FormattableString.Invariant(
            $"Viewport {_case.ViewportWidth} x {_case.ViewportHeight}, DPI {_case.DpiScale}, zoom {_case.ZoomFactor}. Actual DPI: {Root.XamlRoot.RasterizationScale}. Scroll all the way down with the mouse wheel, or scroll partway down and click Jump Down. Then click Scroll to Top. Print Debug Log writes events to the Visual Studio Output window.");
        DumpRuntimeModules();
        Record("Loaded", Snapshot());
        Debug.WriteLine("[LayoutCycleRepro] Loaded " + Snapshot());
    }

    private void OnPrepare(object sender, RoutedEventArgs args)
    {
        Begin("prepare");
        var id = CaseScrollView.ScrollTo(0, _case.InitialOffset,
            new ScrollingScrollOptions(ScrollingAnimationMode.Disabled, ScrollingSnapPointsMode.Ignore));
        Record("Requested", $"CorrelationId={id} Target={_case.InitialOffset}");
    }

    private void OnScrollToTop(object sender, RoutedEventArgs args)
    {
        Begin("top");
        // Transfer focus and respect the system animation setting.
        CaseScrollView.Focus(FocusState.Programmatic);
        var id = CaseScrollView.ScrollTo(0, 0, new ScrollingScrollOptions(
            _settings.AnimationsEnabled ? ScrollingAnimationMode.Enabled : ScrollingAnimationMode.Disabled,
            ScrollingSnapPointsMode.Ignore));
        Record("Requested", $"CorrelationId={id} Target=0 SystemAnimations={_settings.AnimationsEnabled}");
    }

    private void Begin(string phase)
    {
        // Preserve the preceding manual scroll even if the live ring wraps during a layout cycle.
        if (phase == "top") _beforeScrollToTop = _tail.ToArray();
        _phase = phase;
        Record("Begin", Snapshot());
        Debug.WriteLine($"[LayoutCycleRepro] Capture phase={phase}; prior history, sequence and elapsed time retained.");
    }

    private void OnPointerWheelChanged(object sender, PointerRoutedEventArgs args)
    {
        var properties = args.GetCurrentPoint(CaseScrollView).Properties;
        Record("PointerWheel", $"Delta={properties.MouseWheelDelta} Horizontal={properties.IsHorizontalMouseWheel} {Snapshot()}");
    }

    private void OnPrepared(ItemsRepeater sender, ItemsRepeaterElementPreparedEventArgs args)
    {
        _realized[args.Element] = args.Index;
        Record("Prepared", $"Index={args.Index} Item={_case.Items[args.Index].Id} Element={RuntimeHelpers.GetHashCode(args.Element):X8}");
    }

    private void OnClearing(ItemsRepeater sender, ItemsRepeaterElementClearingEventArgs args)
    {
        int index = _realized.TryGetValue(args.Element, out int knownIndex) ? knownIndex : -1;
        Record("Clearing", $"Index={index} Element={RuntimeHelpers.GetHashCode(args.Element):X8}");
        _realized.Remove(args.Element);
    }

    private string Snapshot() => FormattableString.Invariant(
        $"Offset={CaseScrollView.VerticalOffset:R} Extent={CaseScrollView.ExtentWidth:R}x{CaseScrollView.ExtentHeight:R} Viewport={CaseScrollView.ViewportWidth:R}x{CaseScrollView.ViewportHeight:R} RepeaterDesired={CaseRepeater.DesiredSize.Width:R}x{CaseRepeater.DesiredSize.Height:R} RepeaterActual={CaseRepeater.ActualWidth:R}x{CaseRepeater.ActualHeight:R} State={CaseScrollView.State} Zoom={CaseScrollView.ZoomFactor:R} Realized=[{string.Join(",", _realized.Values.Order())}]");

    private void Record(string kind, string details)
    {
        if (!_clock.IsRunning) return;
        if (_tail.Count == 1024) _tail.Dequeue();
        long sequence = ++_sequence;
        _tail.Enqueue((sequence, FormattableString.Invariant(
            $"[LayoutCycleRepro] {_phase} #{sequence} +{_clock.Elapsed.TotalMilliseconds:F3}ms {kind} {details}")));
    }

    private void OnDump(object sender, RoutedEventArgs args) => DumpCapture();

    public static void DumpCapture()
    {
        if (_active is not { } window) return;
        // Only saved strings; safe to invoke from a managed frame at a native debugger break.
        Debug.WriteLine($"[LayoutCycleRepro] Dump: preserved={window._beforeScrollToTop.Length} recent={window._tail.Count} lastSequence={window._sequence}");
        long lastSequence = 0;
        foreach (var entry in window._beforeScrollToTop)
        {
            Debug.WriteLine(entry.Text);
            lastSequence = entry.Sequence;
        }
        foreach (var entry in window._tail)
        {
            if (entry.Sequence <= lastSequence) continue;
            if (lastSequence != 0 && entry.Sequence > lastSequence + 1)
                Debug.WriteLine($"[LayoutCycleRepro] Ring capacity exceeded: sequences {lastSequence + 1}..{entry.Sequence - 1} omitted.");
            Debug.WriteLine(entry.Text);
            lastSequence = entry.Sequence;
        }
    }

    private static void DumpRuntimeModules()
    {
        using var process = Process.GetCurrentProcess();
        foreach (ProcessModule module in process.Modules)
        {
            if (!module.ModuleName.Equals("Microsoft.UI.Xaml.dll", StringComparison.OrdinalIgnoreCase)
                && !module.ModuleName.Equals("Microsoft.UI.Xaml.Controls.dll", StringComparison.OrdinalIgnoreCase)) continue;
            // Numeric versions only: build labels and absolute module paths can contain private data.
            var version = module.FileVersionInfo;
            Debug.WriteLine($"[LayoutCycleRepro] Module={module.ModuleName} FileVersion={version.FileMajorPart}.{version.FileMinorPart}.{version.FileBuildPart}.{version.FilePrivatePart} ProductVersion={version.ProductMajorPart}.{version.ProductMinorPart}.{version.ProductBuildPart}.{version.ProductPrivatePart}");
        }
    }
}
