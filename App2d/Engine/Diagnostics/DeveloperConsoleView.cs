namespace App2d.Engine.Diagnostics;

internal sealed class DeveloperConsoleView : UserControl
{
    private readonly DeveloperConsole _console;
    private readonly RichTextBox _output;
    private readonly TextBox _input;
    private readonly List<string> _history = [];
    private int _historyIndex;

    public DeveloperConsoleView(DeveloperConsole console)
    {
        _console = console;
        BackColor = Color.FromArgb(12, 17, 27);
        BorderStyle = BorderStyle.FixedSingle;

        var header = new Label
        {
            Dock = DockStyle.Top,
            Height = 30,
            Text = "  DEVELOPER CONSOLE   [ click anywhere to type | ` close | Tab complete | help ]",
            TextAlign = ContentAlignment.MiddleLeft,
            BackColor = Color.FromArgb(25, 35, 52),
            ForeColor = Color.FromArgb(145, 205, 255),
            Font = new Font("Consolas", 10f, FontStyle.Bold)
        };
        _output = new RichTextBox
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            BorderStyle = BorderStyle.None,
            BackColor = Color.FromArgb(12, 17, 27),
            ForeColor = Color.FromArgb(221, 231, 242),
            Font = new Font("Consolas", 11f),
            DetectUrls = false,
            Cursor = Cursors.Default,
            TabStop = false
        };
        _input = new TextBox
        {
            Dock = DockStyle.Fill,
            BorderStyle = BorderStyle.None,
            BackColor = Color.FromArgb(20, 28, 42),
            ForeColor = Color.White,
            Font = new Font("Consolas", 12f)
        };
        var prompt = new Label
        {
            Dock = DockStyle.Left,
            Width = 28,
            Text = ">",
            TextAlign = ContentAlignment.MiddleCenter,
            BackColor = Color.FromArgb(20, 28, 42),
            ForeColor = Color.FromArgb(100, 220, 170),
            Font = new Font("Consolas", 12f, FontStyle.Bold)
        };
        var inputRow = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 34,
            BackColor = Color.FromArgb(20, 28, 42),
            Padding = new Padding(0, 6, 8, 4)
        };
        inputRow.Controls.Add(_input);
        inputRow.Controls.Add(prompt);

        Controls.Add(_output);
        Controls.Add(inputRow);
        Controls.Add(header);
        _input.KeyDown += OnInputKeyDown;
        _output.MouseDown += OnConsolePaneMouseDown;
        header.MouseDown += OnConsolePaneMouseDown;
        AppendLine("Developer console ready. Type 'help' for usage or 'list' for variables.");
    }

    public bool IsOpen => Visible;

    public void Open()
    {
        Visible = true;
        BringToFront();
    }

    public void FocusInput()
    {
        if (!IsOpen)
            return;

        ActiveControl = _input;
        _input.Select();
        _input.Focus();
        _input.SelectionStart = _input.TextLength;
    }

    public bool HandleCommandKey(Keys keyCode)
    {
        switch (keyCode)
        {
            case Keys.Enter:
                RunInput();
                return true;
            case Keys.Up:
                MoveHistory(-1);
                return true;
            case Keys.Down:
                MoveHistory(1);
                return true;
            case Keys.Tab:
                CompleteInput();
                return true;
            default:
                return false;
        }
    }

    public bool InsertCharacter(char character)
    {
        FocusInput();
        if (character == '\b')
        {
            if (_input.SelectionLength > 0)
            {
                _input.SelectedText = string.Empty;
            }
            else if (_input.SelectionStart > 0)
            {
                var removeAt = _input.SelectionStart - 1;
                _input.Text = _input.Text.Remove(removeAt, 1);
                _input.SelectionStart = removeAt;
            }
            return true;
        }

        if (char.IsControl(character))
            return false;

        _input.SelectedText = character.ToString();
        return true;
    }

    public void CloseAndClearFocus()
    {
        Visible = false;
        _input.Clear();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _input.KeyDown -= OnInputKeyDown;
            _output.MouseDown -= OnConsolePaneMouseDown;
            _input.Font.Dispose();
            _output.Font.Dispose();
            foreach (Control control in Controls)
            {
                if (control is Label label)
                    label.Font.Dispose();
            }
        }
        base.Dispose(disposing);
    }

    private void OnInputKeyDown(object? sender, KeyEventArgs e)
    {
        if (HandleCommandKey(e.KeyCode))
        {
            e.Handled = true;
            e.SuppressKeyPress = true;
        }
    }

    private void OnConsolePaneMouseDown(object? sender, MouseEventArgs e) =>
        BeginInvoke((Action)FocusInput);

    private void RunInput()
    {
        var commandLine = _input.Text.Trim();
        _input.Clear();
        if (commandLine.Length == 0)
            return;

        if (_history.Count == 0 || !string.Equals(_history[^1], commandLine, StringComparison.Ordinal))
            _history.Add(commandLine);
        _historyIndex = _history.Count;
        AppendLine($"> {commandLine}", Color.FromArgb(125, 225, 180));

        var result = _console.Execute(commandLine);
        if (result.ClearOutput)
            _output.Clear();
        foreach (var line in result.Lines)
            AppendLine(line);
    }

    private void MoveHistory(int offset)
    {
        if (_history.Count == 0)
            return;

        _historyIndex = Math.Clamp(_historyIndex + offset, 0, _history.Count);
        _input.Text = _historyIndex < _history.Count ? _history[_historyIndex] : string.Empty;
        _input.SelectionStart = _input.TextLength;
    }

    private void CompleteInput()
    {
        var text = _input.Text;
        var firstWhitespace = text.IndexOfAny([' ', '\t']);
        var prefix = firstWhitespace < 0 ? text : text[..firstWhitespace];
        var matches = _console.Complete(prefix);
        if (matches.Count == 1)
        {
            _input.Text = matches[0] + (firstWhitespace < 0 ? " " : text[firstWhitespace..]);
            _input.SelectionStart = _input.TextLength;
        }
        else if (matches.Count > 1)
        {
            AppendLine(string.Join("   ", matches), Color.FromArgb(145, 205, 255));
        }
    }

    private void AppendLine(string text, Color? color = null)
    {
        _output.SelectionStart = _output.TextLength;
        _output.SelectionColor = color ?? _output.ForeColor;
        _output.AppendText(text + Environment.NewLine);
        _output.SelectionStart = _output.TextLength;
        _output.ScrollToCaret();
    }
}
