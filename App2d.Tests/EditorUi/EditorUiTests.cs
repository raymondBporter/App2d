using App2d.EditorUi;
using ImGuiNET;
using System.Numerics;

namespace App2d.Tests.EditorUi;

[CollectionDefinition("ImGui", DisableParallelization = true)]
public sealed class ImGuiCollection;

[Collection("ImGui")]
public sealed class EditorUiTests
{
    [Fact]
    public void RowPlacesPanelsBesideEachOther()
    {
        using var context = new UiContext();
        Vector2 first = default, second = default;
        context.Draw(() => Ui.Row(
            () => Ui.Panel("first", () => first = ImGui.GetWindowPos(), size: new(180, 100)),
            () => Ui.Panel("second", () => second = ImGui.GetWindowPos(), size: new(240, 100))));
        Assert.Equal(first.Y, second.Y);
        Assert.True(second.X >= first.X + 180);
    }

    [Fact]
    public void RepeatedFieldLabelsHaveIndependentIdsInPanelsAndSections()
    {
        using var context = new UiContext();
        var ids = new List<uint>();
        void Fields() => Ui.Section("Properties", () => ids.Add(ImGui.GetID("##Value")));
        context.Draw(() =>
        {
            Ui.Row(
                () => Ui.Panel("first", Fields, size: new(180, 100)),
                () => Ui.Panel("second", Fields, size: new(180, 100)));
            Ui.Section("Position", () => ids.Add(ImGui.GetID("##Value")));
            Ui.Section("Size", () => ids.Add(ImGui.GetID("##Value")));
        });
        Assert.Equal(4, ids.Count);
        Assert.Equal(4, ids.Distinct().Count());
    }

    [Fact]
    public void ThrowingViewRestoresTheParentWindowAndIdScope()
    {
        using var context = new UiContext();
        context.Draw(() =>
        {
            var parentId = ImGui.GetID("parent");
            var parentPosition = ImGui.GetWindowPos();
            Assert.Throws<InvalidOperationException>(() =>
                Ui.Section("Broken", () => Ui.Panel("panel", () => Ui.Row(
                    () => throw new InvalidOperationException("View failed")), size: new(180, 100))));
            Assert.Equal(parentId, ImGui.GetID("parent"));
            Assert.Equal(parentPosition, ImGui.GetWindowPos());
            Ui.Panel("working", () => ImGui.TextUnformatted("Still drawing"), size: new(180, 100));
        });
        Assert.True(ImGui.GetDrawData().TotalVtxCount > 0);
    }

    [Fact]
    public void ClippedPanelSkipsItsContentAndRestoresTheParent()
    {
        using var context = new UiContext();
        context.Draw(() =>
        {
            var cursor = ImGui.GetCursorPos();
            ImGui.SetCursorPos(new(2000, 2000));
            Ui.Panel("clipped", () => throw new InvalidOperationException("Clipped view was invoked"), size: new(100, 100));
            ImGui.SetCursorPos(cursor);
            Ui.Panel("visible", () => ImGui.TextUnformatted("Visible"), size: new(180, 100));
        });
    }

    [Fact]
    public void WinFormsEventsGoToTheReceivingContextInsteadOfTheCurrentContext()
    {
        using var first = new UiContext();
        using var second = new UiContext();
        using var surface = new InputSurface();
        using var platform = new WinFormsImGuiPlatform(surface);
        surface.GainFocus();
        surface.Type('é'); // The other context is current when the native window delivers this event.
        first.Draw(() =>
        {
            var characters = ImGui.GetIO().InputQueueCharacters;
            Assert.Equal(1, characters.Size);
            Assert.Equal((ushort)'é', characters[0]);
        }, platform);
        second.Draw(() => Assert.Equal(0, ImGui.GetIO().InputQueueCharacters.Size));
    }

    [Fact]
    public void WinFormsRetainsAClickWhichStartsAndEndsBetweenFrames()
    {
        using var context = new UiContext();
        using var surface = new InputSurface();
        using var platform = new WinFormsImGuiPlatform(surface);
        surface.GainFocus();
        surface.PressMouse();
        surface.ReleaseMouse();
        context.Draw(() => Assert.True(ImGui.IsMouseClicked(ImGuiMouseButton.Left)), platform);
        context.Draw(() => Assert.True(ImGui.IsMouseReleased(ImGuiMouseButton.Left)), platform);
    }

    [Fact]
    public void LosingWinFormsFocusReleasesTheMouseAndModifiers()
    {
        using var context = new UiContext();
        using var surface = new InputSurface();
        using var platform = new WinFormsImGuiPlatform(surface);
        surface.GainFocus();
        surface.PressKey(Keys.A | Keys.Control);
        surface.PressMouse();
        // ImGui can spread keyboard and mouse transitions over consecutive frames.
        context.Draw(() => { }, platform);
        context.Draw(() =>
        {
            Assert.True(ImGui.GetIO().KeyCtrl);
            Assert.True(ImGui.IsMouseDown(ImGuiMouseButton.Left));
        }, platform);
        surface.LoseFocus();
        context.Draw(() => { }, platform);
        context.Draw(() =>
        {
            Assert.False(ImGui.GetIO().KeyCtrl);
            Assert.False(ImGui.IsMouseDown(ImGuiMouseButton.Left));
        }, platform);
    }

    [Fact]
    public void InvalidPropertyTextStaysInTheDraftAndCanBeCorrectedBeforeApply()
    {
        using var context = new UiContext();
        var model = new ValidatedTextModel();
        var inspector = new PropertyInspector();
        context.Draw(() =>
        {
            ImGui.SetKeyboardFocusHere();
            inspector.Draw(model);
        });
        context.Draw(() => inspector.Draw(model));

        ReplaceText("bad");
        Assert.False(inspector.Commit(model));
        Assert.Equal("original", model.Value);
        Assert.Contains("Invalid value", inspector.Error);

        ReplaceText("corrected");
        Assert.True(inspector.Commit(model));
        Assert.Equal("corrected", model.Value);
        Assert.Null(inspector.Error);

        void ReplaceText(string text)
        {
            var io = ImGui.GetIO();
            io.AddKeyEvent(ImGuiKey.ModCtrl, true);
            io.AddKeyEvent(ImGuiKey.A, true);
            context.Draw(() => inspector.Draw(model));
            io.AddKeyEvent(ImGuiKey.A, false);
            io.AddKeyEvent(ImGuiKey.ModCtrl, false);
            context.Draw(() => inspector.Draw(model));
            io.AddInputCharactersUTF8(text);
            context.Draw(() => inspector.Draw(model));
            context.Draw(() => inspector.Draw(model));
        }
    }

    public sealed class ValidatedTextModel
    {
        private string _value = "original";
        public string Value
        {
            get => _value;
            set
            {
                if (value == "bad")
                {
                    throw new FormatException("Invalid value");
                }

                _value = value;
            }
        }
    }

    private sealed class InputSurface : Control
    {
        public void GainFocus() => OnGotFocus(EventArgs.Empty);
        public void LoseFocus() => OnLostFocus(EventArgs.Empty);
        public void Type(char value) => OnKeyPress(new(value));
        public void PressKey(Keys value) => OnKeyDown(new(value));
        public void PressMouse() => OnMouseDown(new(MouseButtons.Left, 1, 40, 40, 0));
        public void ReleaseMouse() => OnMouseUp(new(MouseButtons.Left, 1, 40, 40, 0));
    }

    private sealed class UiContext : IDisposable
    {
        private readonly nint _context = ImGui.CreateContext();

        public UiContext()
        {
            ImGui.SetCurrentContext(_context);
            var io = ImGui.GetIO();
            io.DisplaySize = new(800, 600);
            io.DeltaTime = 1f / 60;
            io.Fonts.AddFontDefault();
            io.Fonts.GetTexDataAsRGBA32(out nint _, out _, out _, out _);
        }

        public void Draw(Action content, WinFormsImGuiPlatform? platform = null)
        {
            ImGui.SetCurrentContext(_context);
            platform?.UpdateInput(ImGui.GetIO());
            ImGui.NewFrame();
            Ui.Root("Tests", content);
            ImGui.Render();
        }

        public void Dispose() => ImGui.DestroyContext(_context);
    }
}
