# WinUI ScrollView / ItemsRepeater layout cycle

Standalone WinUI 3 reproduction of a layout cycle during animated scrolling to
the top of a virtualized list with different item heights.

**Confirmed reproduction:** the error occurs in this application after manually
scrolling with the mouse wheel and then clicking **Animated ScrollToTop**. It has
been observed with both large and small application windows. Reproduction can
depend on scroll position, realization history and animation timing.

## Requirements

- Windows with the Windows App SDK runtime, x64. The reported environment is
  Windows build 26200 with .NET 10.0.14.
- Visual Studio with .NET desktop development and the Windows application
  development tools required for WinUI 3.
- .NET 10 SDK and Windows SDK 10.0.26100.0 (the project's target).
- NuGet package **Microsoft.WindowsAppSDK 2.6.4**. No other direct package or
  project dependencies are required. This is an unpackaged application.
- System animation effects enabled. The application respects
  `UISettings.AnimationsEnabled`.

Use **Debug / x64** to enable the native layout-cycle diagnostics. Start at
100% display scaling to match the supplied geometry; the actual scale is shown
in the application. The project has not been built or run as part of preparing
this source package. The confirmed behavior above was reported for the
standalone reproduction before these naming and diagnostic changes; verify it
again with this version.

## Steps to reproduce

1. Open `LayoutCycleRepro.csproj`, build it in Visual Studio and start debugging.
   Allow the initial content to finish loading.
2. Move the pointer over the list and **scroll down manually with the mouse
   wheel**, far enough for elements near the start to be recycled. The optional
   **Initial offset** button is not required for this confirmed reproduction path.
3. Click **Animated ScrollToTop**. Do not change window size or interact with the
   list while the animation is running.
4. Observe the native debugger output and any exception. If necessary, repeat
   the manual scrolling followed by the button click. Also check a large and a
   small window; the failure is not limited to one window size.

Expected: the animated scroll reaches offset 0 and layout converges while
StackLayout continues virtualizing the list.

Actual: repeated layout passes can fail to converge. An item near the beginning
of the realized range is repeatedly prepared and cleared, and the estimated
list height changes even while the scroll offset and effective viewport remain
unchanged. Reported errors include:

```text
AG_E_LAYOUT_CYCLE (0x88000FA8)
Layout cycle detected (0x802B0014)
Layout cycle detected. Layout could not complete.
```

Continuing from a native debugger break can allow the animation to progress and
layout to settle. Reaching offset 0 after continuing is not evidence that the
cycle did not occur.

## Reproduction geometry

`test-case.json` supplies the following values in Item01–Item13 order. Heights
are assigned to simple Borders. The widths are **MinWidth** values matching
their desired widths, not fixed arrange widths. Items can stretch to the list
width.

```text
Heights:        74,74,74,74,74,74,74,122,122,122,137,137,153
Desired widths: 740,791,740,1097,683,709,704,737,737,1018,1006,1012,1111
Spacing:        4 DIP
Viewport:       1183 x 566 DIP
List width:     1135 DIP at this viewport width
Cache lengths:  Horizontal 2, Vertical 2
Zoom:           1
```

There are 24 DIP of horizontal padding per side, 64 DIP before the list and
48 DIP after it. The leading empty row and row spacing provide this geometry
without dynamic content. The application retains ScrollView, ItemsRepeater,
the native virtualizing StackLayout and animated ScrollTo(0, 0). It has no
custom layout, additional scroll viewer or layout workaround.

The optional initial-offset button requests offset 959 without animation.
ScrollView may clamp this request according to its current extent. It does
not replace the manual-wheel step. The reference observed stall offset in the
data is 912.6781005859375; it is not a programmed stop or scroll target.

The exact list height is 1311 + 12 x 4 = **1359 DIP**. The rounded average item
height is 101 DIP. Estimating a missing 74-DIP prefix item with that average
adds 27 DIP. Observed list estimates alternated between **1386 and 1413 DIP**,
and the enclosing extents between **1498 and 1525 DIP**. With all 13 elements
realized the list settled at 1359 DIP, with an enclosing extent of 1471 DIP.
This is consistent with an estimate / layout-origin / realization-boundary
feedback loop. It is not proof of the precise private WinUI invalidation path.

## Loaded WinUI module versions

The package reference does not identify the binaries actually loaded by a
running process. Available numeric module information is:

| Module | Reported file version |
| --- | --- |
| Microsoft.UI.Xaml.dll | 3.2.0.2511 |
| Microsoft.UI.Xaml.Controls.dll | Not yet recorded; collect from the current run |

At startup the Debug output reports **both loaded module names and numeric file
and product versions**. Copy these lines with any report. Absolute module paths
and free-form version/build labels are deliberately omitted. Do not infer the
second module's version from the first or from the NuGet version.

## Debug diagnostics

In Debug builds, `App` enables:

```csharp
DebugSettings.LayoutCycleTracingLevel = LayoutCycleTracingLevel.High;
DebugSettings.LayoutCycleDebugBreakLevel = LayoutCycleDebugBreakLevel.Low;
```

Enable managed and native debugging in Visual Studio. Configure Microsoft
symbols before reproducing the failure. Matching symbols and source checksums
are needed to attribute private WinUI calls; unresolved native addresses alone
cannot identify an invalidating operation.

The in-memory history starts when the window is constructed, **before manual
scrolling**. It includes mouse-wheel delta/direction, ViewChanged, StateChanged,
extent and effective-viewport events, realization/recycling with index and
neutral item ID, element instance identity, and animation/completion events.
Events share a monotonically increasing sequence and elapsed-time clock.
Starting ScrollToTop does not reset either or clear the history.

The most recent **1024 events before the ScrollToTop click are preserved** in a
separate snapshot; another live ring retains up to 1024 recent events. This
keeps the preceding manual-scroll context available even if the live ring
wraps during a cycle. Dumps merge the two without duplicate sequences and
report any intervening gap. The logger performs no layout invalidation,
property writes, dispatcher work or scrolling of its own.

At the native break, save the native output and mixed call stack. From a
managed frame, use the Immediate window:

```csharp
LayoutCycleRepro.MainWindow.DumpCapture()
```

This prints saved strings without querying layout. If function evaluation is
unavailable at the native break, inspect the saved buffer in a managed frame.
Then continue within the same reproduction and retain the automatic dump at
completion or an unhandled exception. **Dump capture** also prints the buffer.
Keep the startup module-version lines alongside it.

For a definitive native diagnosis, capture two successive failing layout
passes: StackLayout's first/last realized index, average size and extent;
FlowLayoutAlgorithm's actual RealizationRect; ViewportManager's LayoutOrigin,
cache and pending/expected/unshiftable shifts; and the stack of the repeated
InvalidateMeasure/InvalidateArrange. Compare these while the public offset is
unchanged. Missing private symbols are an evidence gap, not a negative result.

Inspect any debugger output before sharing it: debugger-generated call stacks
or module listings may add local paths independently of this application's
logger. Keep dumps, build outputs, IDE state and local files out of version
control; common formats are excluded by `.gitignore`.
