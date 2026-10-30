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
3. Scroll all the way to the bottom of the list using the mouse wheel. Alternatively, scroll partway down using the mouse wheel, then click **Jump Down**.
4. Click **Scroll to Top**.
5. Observe the layout cycle exception.

The layout cycle has been successfully reproduced with this version in both large and small windows. Reproduction may depend on the scroll position and previous scrolling activity.

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

Use **Print Debug Log** to write diagnostic events to the Visual Studio **Output** window. Startup output also includes the loaded WinUI module versions.
