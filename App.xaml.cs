using Microsoft.UI.Xaml;

namespace LayoutCycleRepro;

public partial class App : Application
{
    private Window? _window;

    public App()
    {
        InitializeComponent();
#if DEBUG
        DebugSettings.LayoutCycleTracingLevel = LayoutCycleTracingLevel.High;
        DebugSettings.LayoutCycleDebugBreakLevel = LayoutCycleDebugBreakLevel.Low;
#endif
        UnhandledException += (_, _) => MainWindow.DumpCapture();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        var capture = TestCase.Load();
        _window = new MainWindow(capture);
        // At scale 1 this leaves room for the 1183 x 566 DIP viewport and controls.
        _window.AppWindow.Resize(new global::Windows.Graphics.SizeInt32(
            (int)Math.Ceiling(capture.ViewportWidth + 80),
            (int)Math.Ceiling(capture.ViewportHeight + 220)));
        _window.Activate();
    }
}
