namespace Midnight.Cli.Common;

public class ConsoleOutput : IConsoleOutput
{
    private readonly CliContext _context;

    public bool IsJsonMode { get; set; }

    public ConsoleOutput(CliContext context)
    {
        _context = context;
    }

    public void Write(string message)
    {
        if (IsJsonMode) return;
        Console.Write(message);
    }

    public void WriteLine(string message = "")
    {
        if (IsJsonMode) return;
        Console.WriteLine(message);
    }

    public void WriteInfo(string message)
    {
        if (IsJsonMode) return;
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.Write("[INFO] ");
        Console.ResetColor();
        Console.WriteLine(message);
    }

    public void WriteSuccess(string message)
    {
        if (IsJsonMode) return;
        Console.ForegroundColor = ConsoleColor.Green;
        Console.Write("[✓] ");
        Console.ResetColor();
        Console.WriteLine(message);
    }

    public void WriteWarning(string message)
    {
        if (IsJsonMode) return;
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.Write("[!] ");
        Console.ResetColor();
        Console.WriteLine(message);
    }

    public void WriteError(string message)
    {
        if (IsJsonMode) return;
        Console.ForegroundColor = ConsoleColor.Red;
        Console.Write("[✗] ");
        Console.ResetColor();
        Console.WriteLine(message);
    }

    public void WriteHeader(string title)
    {
        if (IsJsonMode) return;
        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.Magenta;
        Console.WriteLine($"═══ {title.ToUpperInvariant()} ═══");
        Console.ResetColor();
    }

    public void WriteCheckItem(CheckItem item)
    {
        if (IsJsonMode) return;

        switch (item.Status)
        {
            case CheckStatus.Ok:
                Console.ForegroundColor = ConsoleColor.Green;
                Console.Write("  [✓] ");
                break;
            case CheckStatus.Warning:
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.Write("  [!] ");
                break;
            case CheckStatus.Failed:
                Console.ForegroundColor = ConsoleColor.Red;
                Console.Write("  [✗] ");
                break;
        }

        Console.ResetColor();
        Console.ForegroundColor = ConsoleColor.White;
        Console.Write($"{item.Name,-24} ");
        Console.ResetColor();
        Console.WriteLine(item.Details);

        if (!string.IsNullOrEmpty(item.Remediation) && item.Status != CheckStatus.Ok)
        {
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine($"      -> Fix: {item.Remediation}");
            Console.ResetColor();
        }
    }

    public void WriteChecklist(string title, IEnumerable<CheckItem> items)
    {
        if (IsJsonMode) return;
        WriteHeader(title);
        foreach (var item in items)
        {
            WriteCheckItem(item);
        }
        Console.WriteLine();
    }

    public bool Confirm(string prompt, bool defaultYes = true)
    {
        if (_context.Yes)
        {
            WriteInfo($"{prompt} [y/n] -> Auto-confirmed via -y flag.");
            return true;
        }

        var defaultHint = defaultYes ? "[Y/n]" : "[y/N]";
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.Write($"? {prompt} {defaultHint}: ");
        Console.ResetColor();

        var input = Console.ReadLine()?.Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(input))
            return defaultYes;

        return input is "y" or "yes" or "j" or "ja";
    }

    public int SelectOption(string prompt, IReadOnlyList<string> options, int defaultIndex = 0)
    {
        if (_context.Yes)
            return defaultIndex;

        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine($"? {prompt}");
        Console.ResetColor();

        for (var i = 0; i < options.Count; i++)
        {
            var isDef = i == defaultIndex ? " (Standard)" : "";
            Console.WriteLine($"  [{i + 1}] {options[i]}{isDef}");
        }

        while (true)
        {
            Console.Write($"Auswahl [1-{options.Count}]: ");
            var input = Console.ReadLine()?.Trim();
            if (string.IsNullOrEmpty(input))
                return defaultIndex;

            if (int.TryParse(input, out var choice) && choice >= 1 && choice <= options.Count)
                return choice - 1;

            WriteWarning($"Ungültige Eingabe. Bitte Zahl von 1 bis {options.Count} eingeben.");
        }
    }

    public string Ask(string prompt, string? defaultValue = null, bool isPassword = false)
    {
        if (_context.Yes && !string.IsNullOrEmpty(defaultValue))
            return defaultValue;

        var defaultHint = !string.IsNullOrEmpty(defaultValue) ? $" [{defaultValue}]" : "";
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.Write($"? {prompt}{defaultHint}: ");
        Console.ResetColor();

        if (isPassword)
        {
            var pwd = ReadPassword();
            return string.IsNullOrEmpty(pwd) && defaultValue != null ? defaultValue : pwd;
        }

        var input = Console.ReadLine()?.Trim();
        return string.IsNullOrEmpty(input) && defaultValue != null ? defaultValue : input ?? "";
    }

    public void ShowLogTail(string logFilePath, int lines = 25)
    {
        if (IsJsonMode || !File.Exists(logFilePath))
            return;

        try
        {
            var allLines = File.ReadAllLines(logFilePath);
            var tail = allLines.TakeLast(lines);

            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.DarkYellow;
            Console.WriteLine($"--- Auszug aus dem Installer-Log ({logFilePath}, letzte {lines} Zeilen) ---");
            Console.ResetColor();

            foreach (var l in tail)
            {
                Console.WriteLine(SecretScrubber.Redact(l));
            }
            Console.WriteLine();
        }
        catch
        {
            // Ignorieren falls nicht lesbar
        }
    }

    private static string ReadPassword()
    {
        var password = string.Empty;
        while (true)
        {
            var key = Console.ReadKey(intercept: true);
            if (key.Key is ConsoleKey.Enter)
            {
                Console.WriteLine();
                break;
            }
            if (key.Key is ConsoleKey.Backspace && password.Length > 0)
            {
                password = password[..^1];
                Console.Write("\b \b");
            }
            else if (!char.IsControl(key.KeyChar))
            {
                password += key.KeyChar;
                Console.Write("*");
            }
        }
        return password;
    }
}
