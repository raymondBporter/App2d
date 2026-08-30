using App2d.Core;
using System.ComponentModel;
using System.Globalization;
using System.Text;

namespace App2d.Diagnostics;

public sealed class DeveloperConsole
{
    private readonly Dictionary<string, ConsoleVariable> _variables = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, ConsoleCommand> _commands = new(StringComparer.OrdinalIgnoreCase);

    public DeveloperConsole()
    {
        RegisterCommand("help", "Show console help or describe a variable/command.", Help);
        RegisterCommand("list", "List variables, optionally filtered by name.", List);
        RegisterCommand("clear", "Clear the console output.", _ => ConsoleCommandResult.Clear());
        RegisterCommand("toggle", "Flip a boolean variable: toggle <name>.", Toggle);
    }

    public void RegisterVariable<T>(
        string name,
        Func<T> getter,
        Action<T> setter,
        string description = "")
    {
        ArgGuard.ThrowIfNull(getter);
        ArgGuard.ThrowIfNull(setter);
        name = ValidateName(name);
        EnsureNameAvailable(name);

        _variables.Add(name, new ConsoleVariable(
            name,
            typeof(T),
            description,
            () => getter(),
            value => setter((T)value)));
    }

    public void RegisterCommand(
        string name,
        string description,
        Func<IReadOnlyList<string>, ConsoleCommandResult> execute)
    {
        ArgGuard.ThrowIfNull(execute);
        name = ValidateName(name);
        EnsureNameAvailable(name);
        _commands.Add(name, new ConsoleCommand(name, description, execute));
    }

    public ConsoleCommandResult Execute(string commandLine)
    {
        if (string.IsNullOrWhiteSpace(commandLine))
            return ConsoleCommandResult.Empty;

        var tokens = Tokenize(commandLine);
        if (tokens.Count == 0)
            return ConsoleCommandResult.Empty;

        var name = tokens[0];
        if (_commands.TryGetValue(name, out var command))
        {
            try
            {
                return command.Execute([.. tokens.Skip(1)]);
            }
            catch (Exception exception)
            {
                return ConsoleCommandResult.From($"error: {exception.Message}");
            }
        }

        var equalsIndex = commandLine.IndexOf('=');
        if (equalsIndex >= 0)
        {
            name = commandLine[..equalsIndex].Trim();
            return SetVariable(name, commandLine[(equalsIndex + 1)..].Trim());
        }

        if (!_variables.TryGetValue(name, out var variable))
            return ConsoleCommandResult.From($"unknown variable or command '{name}'. Type 'help' for usage.");

        return tokens.Count == 1
            ? ConsoleCommandResult.From(variable.DisplayValue)
            : SetVariable(name, string.Join(' ', tokens.Skip(1)));
    }

    public IReadOnlyList<string> Complete(string prefix)
    {
        prefix = prefix.Trim();
        return _variables.Keys
            .Concat(_commands.Keys)
            .Where(name => name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private ConsoleCommandResult SetVariable(string name, string value)
    {
        if (!_variables.TryGetValue(name, out var variable))
            return ConsoleCommandResult.From($"unknown variable '{name}'.");

        return variable.TrySet(value, out var error)
            ? ConsoleCommandResult.From(variable.DisplayValue)
            : ConsoleCommandResult.From($"error: {error}");
    }

    private ConsoleCommandResult Help(IReadOnlyList<string> arguments)
    {
        if (arguments.Count > 0)
        {
            var name = arguments[0];
            if (_variables.TryGetValue(name, out var variable))
            {
                var description = string.IsNullOrWhiteSpace(variable.Description)
                    ? string.Empty
                    : $" - {variable.Description}";
                return ConsoleCommandResult.From($"{variable.DisplayValue} ({variable.TypeName}){description}");
            }

            if (_commands.TryGetValue(name, out var command))
                return ConsoleCommandResult.From($"{command.Name} - {command.Description}");

            return ConsoleCommandResult.From($"no variable or command named '{name}'.");
        }

        return ConsoleCommandResult.From(
            "Enter a variable name to read it: draw_fps",
            "Set with whitespace or '=': draw_fps true   |   draw_fps = true",
            "Commands: help [name], list [filter], toggle <name>, clear",
            "Use Tab to complete names and Up/Down to browse command history.");
    }

    private ConsoleCommandResult List(IReadOnlyList<string> arguments)
    {
        var filter = arguments.Count > 0 ? arguments[0] : string.Empty;
        var lines = _variables.Values
            .Where(variable => variable.Name.Contains(filter, StringComparison.OrdinalIgnoreCase))
            .OrderBy(variable => variable.Name, StringComparer.OrdinalIgnoreCase)
            .Select(variable => variable.DisplayValue)
            .ToArray();

        return lines.Length == 0
            ? ConsoleCommandResult.From("no matching variables.")
            : new ConsoleCommandResult(false, lines);
    }

    private ConsoleCommandResult Toggle(IReadOnlyList<string> arguments)
    {
        if (arguments.Count != 1)
            return ConsoleCommandResult.From("usage: toggle <boolean-variable>");
        if (!_variables.TryGetValue(arguments[0], out var variable))
            return ConsoleCommandResult.From($"unknown variable '{arguments[0]}'.");
        if (variable.ValueType != typeof(bool))
            return ConsoleCommandResult.From($"'{variable.Name}' is not a boolean variable.");

        return SetVariable(variable.Name, (!(bool)variable.Value).ToString(CultureInfo.InvariantCulture));
    }

    private void EnsureNameAvailable(string name)
    {
        StateGuard.ThrowIf(
            _variables.ContainsKey(name) || _commands.ContainsKey(name),
            $"A console variable or command named '{name}' is already registered.");
    }

    private static string ValidateName(string name)
    {
        ArgGuard.ThrowIfNullOrWhiteSpace(name);
        name = name.Trim();
        if (!name.All(character => char.IsLetterOrDigit(character) || character is '_' or '.'))
            ArgGuard.ThrowInvalid(name, "Console names may only contain letters, digits, underscores, and periods.");
        return name;
    }

    private static List<string> Tokenize(string commandLine)
    {
        var tokens = new List<string>();
        var current = new StringBuilder();
        var quote = '\0';

        foreach (var character in commandLine)
        {
            if (quote != '\0')
            {
                if (character == quote)
                    quote = '\0';
                else
                    current.Append(character);
                continue;
            }

            if (character is '\'' or '"')
            {
                quote = character;
            }
            else if (char.IsWhiteSpace(character))
            {
                if (current.Length > 0)
                {
                    tokens.Add(current.ToString());
                    current.Clear();
                }
            }
            else
            {
                current.Append(character);
            }
        }

        if (current.Length > 0)
            tokens.Add(current.ToString());
        return tokens;
    }

    private sealed class ConsoleVariable(
        string name,
        Type valueType,
        string description,
        Func<object?> getter,
        Action<object> setter)
    {
        public string Name { get; } = name;
        public Type ValueType { get; } = valueType;
        public string Description { get; } = description;
        public object Value => getter() ?? string.Empty;
        public string TypeName => Nullable.GetUnderlyingType(ValueType)?.Name ?? ValueType.Name;
        public string DisplayValue => $"{Name} = {Format(Value)}";

        public bool TrySet(string text, out string error)
        {
            var targetType = Nullable.GetUnderlyingType(ValueType) ?? ValueType;
            try
            {
                object value;
                if (targetType == typeof(bool))
                {
                    if (!TryParseBoolean(text, out var boolean))
                    {
                        error = $"'{text}' is not a boolean (use true/false, on/off, or 1/0).";
                        return false;
                    }
                    value = boolean;
                }
                else if (targetType == typeof(string))
                {
                    value = text;
                }
                else if (targetType.IsEnum)
                {
                    value = Enum.Parse(targetType, text, ignoreCase: true);
                }
                else
                {
                    var converter = TypeDescriptor.GetConverter(targetType);
                    if (!converter.CanConvertFrom(typeof(string)))
                    {
                        error = $"type '{TypeName}' cannot be set from console text.";
                        return false;
                    }
                    value = converter.ConvertFromInvariantString(text)
                        ?? throw new FormatException("The value could not be parsed.");
                }

                setter(value);
                error = string.Empty;
                return true;
            }
            catch (Exception exception) when (exception is FormatException or ArgumentException or NotSupportedException)
            {
                error = $"cannot set {Name} ({TypeName}) to '{text}': {exception.Message}";
                return false;
            }
        }

        private static bool TryParseBoolean(string text, out bool value)
        {
            if (bool.TryParse(text, out value))
                return true;
            if (text.Equals("on", StringComparison.OrdinalIgnoreCase) || text == "1")
            {
                value = true;
                return true;
            }
            if (text.Equals("off", StringComparison.OrdinalIgnoreCase) || text == "0")
            {
                value = false;
                return true;
            }
            return false;
        }

        private static string Format(object value) => value switch
        {
            bool boolean => boolean ? "true" : "false",
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
            _ => value.ToString() ?? string.Empty
        };
    }

    private sealed record ConsoleCommand(
        string Name,
        string Description,
        Func<IReadOnlyList<string>, ConsoleCommandResult> Execute);
}

public readonly record struct ConsoleCommandResult(bool ClearOutput, IReadOnlyList<string> Lines)
{
    public static ConsoleCommandResult Empty { get; } = new(false, []);
    public static ConsoleCommandResult Clear() => new(true, []);
    public static ConsoleCommandResult From(params string[] lines) => new(false, lines);
}
