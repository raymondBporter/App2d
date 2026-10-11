using ImGuiNET;
using GameKeys = Microsoft.Xna.Framework.Input.Keys;

namespace App2d.EditorUi;

internal static class ImGuiKeyMap
{
    public static ImGuiKey? FromWinForms(Keys key) => key switch
    {
        Keys.ControlKey => ImGuiKey.LeftCtrl,
        Keys.ShiftKey => ImGuiKey.LeftShift,
        Keys.Menu => ImGuiKey.LeftAlt,
        // WinForms and MonoGame key enums use Windows virtual-key codes.
        _ => FromMonoGame((GameKeys)(int)(key & Keys.KeyCode))
    };

    public static ImGuiKey? FromMonoGame(GameKeys key) => key switch
    {
        >= GameKeys.A and <= GameKeys.Z => ImGuiKey.A + (key - GameKeys.A),
        >= GameKeys.D0 and <= GameKeys.D9 => ImGuiKey._0 + (key - GameKeys.D0),
        >= GameKeys.F1 and <= GameKeys.F12 => ImGuiKey.F1 + (key - GameKeys.F1),
        >= GameKeys.NumPad0 and <= GameKeys.NumPad9 => ImGuiKey.Keypad0 + (key - GameKeys.NumPad0),
        GameKeys.Tab => ImGuiKey.Tab,
        GameKeys.Enter => ImGuiKey.Enter,
        GameKeys.Escape => ImGuiKey.Escape,
        GameKeys.Space => ImGuiKey.Space,
        GameKeys.Left => ImGuiKey.LeftArrow,
        GameKeys.Right => ImGuiKey.RightArrow,
        GameKeys.Up => ImGuiKey.UpArrow,
        GameKeys.Down => ImGuiKey.DownArrow,
        GameKeys.Back => ImGuiKey.Backspace,
        GameKeys.Delete => ImGuiKey.Delete,
        GameKeys.Home => ImGuiKey.Home,
        GameKeys.End => ImGuiKey.End,
        GameKeys.PageUp => ImGuiKey.PageUp,
        GameKeys.PageDown => ImGuiKey.PageDown,
        GameKeys.Insert => ImGuiKey.Insert,
        GameKeys.LeftControl => ImGuiKey.LeftCtrl,
        GameKeys.RightControl => ImGuiKey.RightCtrl,
        GameKeys.LeftShift => ImGuiKey.LeftShift,
        GameKeys.RightShift => ImGuiKey.RightShift,
        GameKeys.LeftAlt => ImGuiKey.LeftAlt,
        GameKeys.RightAlt => ImGuiKey.RightAlt,
        GameKeys.LeftWindows => ImGuiKey.LeftSuper,
        GameKeys.RightWindows => ImGuiKey.RightSuper,
        GameKeys.OemSemicolon => ImGuiKey.Semicolon,
        GameKeys.OemPlus => ImGuiKey.Equal,
        GameKeys.OemComma => ImGuiKey.Comma,
        GameKeys.OemMinus => ImGuiKey.Minus,
        GameKeys.OemPeriod => ImGuiKey.Period,
        GameKeys.OemQuestion => ImGuiKey.Slash,
        GameKeys.OemTilde => ImGuiKey.GraveAccent,
        GameKeys.OemOpenBrackets => ImGuiKey.LeftBracket,
        GameKeys.OemPipe => ImGuiKey.Backslash,
        GameKeys.OemCloseBrackets => ImGuiKey.RightBracket,
        GameKeys.OemQuotes => ImGuiKey.Apostrophe,
        GameKeys.CapsLock => ImGuiKey.CapsLock,
        GameKeys.Scroll => ImGuiKey.ScrollLock,
        GameKeys.NumLock => ImGuiKey.NumLock,
        GameKeys.PrintScreen => ImGuiKey.PrintScreen,
        GameKeys.Pause => ImGuiKey.Pause,
        GameKeys.Decimal => ImGuiKey.KeypadDecimal,
        GameKeys.Divide => ImGuiKey.KeypadDivide,
        GameKeys.Multiply => ImGuiKey.KeypadMultiply,
        GameKeys.Subtract => ImGuiKey.KeypadSubtract,
        GameKeys.Add => ImGuiKey.KeypadAdd,
        _ => null
    };
}
