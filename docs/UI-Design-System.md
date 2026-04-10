# USS Desktop UI Design System

## Layout

- Use 18px outer page spacing and 18px card padding as the default shell rhythm.
- Keep primary cards at 18px corner radius and nested surfaces at 12px corner radius.
- Align related controls on the same baseline and use matching control heights for side-by-side actions.
- Prefer adaptive grids over fixed-width button groups when labels can grow in localized UI.

## Controls

- Primary, secondary, and danger actions use the shared rounded button template and palette-driven hover states.
- Text input, combo boxes, popups, and scrollbars must resolve from the application palette instead of default WPF theme colors.
- Long action labels should wrap before clipping.
- Status badges should always expose whether the app is idle, running, or stopping a workflow.

## Color Roles

- `PageBrush` and `ChromeBrush` define the shell.
- `CardBrush`, `CardAltBrush`, and `CardStrokeBrush` define content surfaces.
- `PrimaryBrush`, `AccentBrush`, and `DangerBrush` define action emphasis.
- `InkBrush` and `MutedInkBrush` define readable foreground contrast in both light and dark themes.
- Interaction colors such as hover, focus, scrollbar, and status brushes are derived from the active palette so theme switches stay coherent.

## Responsiveness

- The project selector and diagnostics surfaces should stay readable from the minimum supported window size upward.
- Any label that can expand in Russian or future localizations must either wrap or have enough width budget.
- Theme changes must update the active window tree immediately without restart.
