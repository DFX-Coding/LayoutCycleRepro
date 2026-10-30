# WinUI 3 ScrollView / ItemsRepeater Layout Cycle Repro

Minimal WinUI 3 reproduction of a layout cycle when using animated scrolling with a virtualized `ItemsRepeater` containing items of different heights.

## Environment

- Windows 11
- .NET 10
- Microsoft.WindowsAppSDK 2.6.4
- Visual Studio 2026
- x64

## Steps to Reproduce

1. Open `LayoutCycleRepro.csproj` in Visual Studio.
2. Build and run the application in Debug/x64.
3. Scroll all the way to the bottom of the list using the mouse wheel. Alternatively, scroll partway down using the mouse wheel, then click **Initial offset (optional)**.
4. Click **Animated ScrollToTop**.
5. Observe the layout cycle exception.

The issue has been observed with both large and small window sizes. Reproduction may depend on the scroll position and previous scrolling activity.

## Expected Behavior

The list scrolls smoothly to the top without causing a layout cycle.

## Actual Behavior

WinUI may enter a layout cycle and terminate the application.

```text
AG_E_LAYOUT_CYCLE (0x88000FA8)
Layout cycle detected (0x802B0014)
```

## Technical Details

The reproduction uses:

- `ScrollView`
- `ItemsRepeater` with virtualizing `StackLayout`
- 13 items with different heights
- Animated `ScrollTo(0, 0)`

Item dimensions and layout parameters are defined in `test-case.json`.

## Diagnostics

Debug builds enable WinUI layout cycle tracing and log scrolling, element realization, and layout events.

Use **Dump capture** to print diagnostic events. Startup output also includes the loaded WinUI module versions.

The anonymized version should be verified again before publication.
