using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using PiuScoresWatcher.Core.Settings;
using PiuScoresWatcher.Tests.TestHelpers;

namespace PiuScoresWatcher.Tests.ArchitectureTests;

/// <summary>
///     Every English line in <c>src/PiuScoresWatcher.App/Copy.cs</c> has its translation in each language's
///     <c>Resources/Strings.&lt;code&gt;.resx</c>, keyed and kept the way PIU Scores keeps its own (watcher.md D60):
///     the English is the key; keys are in alphabetical order, so two branches' new keys never meet at the end of
///     a file; no two differ only by case, which MSBuild's resource compiler merges without a word; and a
///     translation keeps exactly its English's holes and key caps. The suite cannot reference App, so Copy.cs is
///     read as text: a line is the first argument of <c>L(…)</c> or the second and third of <c>Plural(…)</c>, the
///     game's own words are the argument of <c>Verbatim(…)</c>, and any other literal with a letter in it is a line
///     that would never be translated.
/// </summary>
public sealed partial class TranslationTests
{
    private static readonly string CopyFile = Path.Combine(RepositoryFiles.Root, "src", "PiuScoresWatcher.App", "Copy.cs");
    private static readonly string ResourcesFolder = Path.Combine(RepositoryFiles.Root, "src", "PiuScoresWatcher.App", "Resources");

    /// <summary>Literals in Copy.cs that are not copy: the brand (a const), the translations' resource name, two number and time formats.</summary>
    private static readonly HashSet<string> NotCopy = ["PIU Scores Watcher", "PiuScoresWatcher.App.Resources.Strings", "N0", "t"];

    private enum Role
    {
        Line,
        Verbatim,
        Loose
    }

    [Fact]
    public void EveryLiteralWithWordsIsALineOrTheGamesOwn()
    {
        var untranslated = CopyLiterals()
            .Where(literal => literal.Role == Role.Loose && HasLetters(literal.Text) && !NotCopy.Contains(literal.Text))
            .Select(literal => literal.Text)
            .ToArray();

        Assert.Empty(untranslated);
    }

    [Fact]
    public void ALineIsAPlainString()
    {
        // a key has to be the same text every time it is looked up
        var interpolated = CopyLiterals().Where(literal => literal.Role == Role.Line && literal.Interpolated).Select(literal => literal.Text).ToArray();

        Assert.Empty(interpolated);
    }

    [Fact]
    public void NoTwoLinesDifferOnlyByCase()
    {
        var twins = Lines().GroupBy(line => line, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => string.Join(" / ", group))
            .ToArray();

        Assert.Empty(twins);
    }

    [Fact]
    public void EveryLanguageTheWatcherSpeaksHasAFileAndNoOtherDoes()
    {
        // English is Copy.cs itself (D60)
        var spoken = Languages.All.Where(code => code != Languages.English).Select(code => $"Strings.{code}.resx").Order(StringComparer.Ordinal);

        Assert.Equal(spoken, Translations().Select(file => file.Name).Order(StringComparer.Ordinal));
    }

    [Fact]
    public void EveryLanguageHasEveryLineAndNoOther()
    {
        var lines = Lines().ToHashSet(StringComparer.Ordinal);
        var drift = Translations().SelectMany(file =>
            lines.Where(line => !file.Entries.Any(entry => entry.Key == line)).Select(line => $"{file.Name} lacks: {line}")
                .Concat(file.Entries.Where(entry => !lines.Contains(entry.Key)).Select(entry => $"{file.Name} keeps a line Copy.cs no longer has: {entry.Key}")))
            .ToArray();

        Assert.Empty(drift);
    }

    [Fact]
    public void KeysAreStoredAlphabetically()
    {
        var misplaced = ResourceFiles().SelectMany(file => file.Entries.Zip(file.Entries.Skip(1))
                .Where(pair => StringComparer.OrdinalIgnoreCase.Compare(pair.First.Key, pair.Second.Key) > 0)
                .Select(pair => $"{file.Name}: \"{pair.Second.Key}\" belongs before \"{pair.First.Key}\""))
            .ToArray();

        Assert.Empty(misplaced);
    }

    [Fact]
    public void NoTwoKeysInAFileDifferOnlyByCase()
    {
        var twins = ResourceFiles().SelectMany(file => file.Entries.GroupBy(entry => entry.Key, StringComparer.OrdinalIgnoreCase)
                .Where(group => group.Count() > 1)
                .Select(group => $"{file.Name}: {string.Join(" / ", group.Select(entry => entry.Key))}"))
            .ToArray();

        Assert.Empty(twins);
    }

    [Fact]
    public void ATranslationKeepsItsHolesAndKeyCaps()
    {
        var broken = Translations().SelectMany(file => file.Entries
                .Where(entry => !Holes(entry.Key).SequenceEqual(Holes(entry.Value)))
                .Select(entry => $"{file.Name}: \"{entry.Value}\" for \"{entry.Key}\""))
            .ToArray();

        Assert.Empty(broken);
    }

    [Fact]
    public void ATranslationIsNeverBlank()
    {
        var blank = Translations().SelectMany(file => file.Entries.Where(entry => string.IsNullOrWhiteSpace(entry.Value))
                .Select(entry => $"{file.Name}: {entry.Key}"))
            .ToArray();

        Assert.Empty(blank);
    }

    [Fact]
    public void TheNeutralFileHoldsNoLines()
    {
        // Copy.cs is the English; a second English would drift from it
        var neutral = ResourceFiles().Single(file => file.Name == "Strings.resx");

        Assert.Empty(neutral.Entries);
    }

    private static bool HasLetters(string text)
    {
        return Hole().Replace(text, "").Any(char.IsLetter);
    }

    private static IEnumerable<string> Holes(string text)
    {
        return Hole().Matches(text).Select(match => match.Value).Order(StringComparer.Ordinal);
    }

    private static IEnumerable<string> Lines()
    {
        return CopyLiterals().Where(literal => literal.Role == Role.Line).Select(literal => literal.Text).Distinct(StringComparer.Ordinal);
    }

    /// <summary>Every translation file: <c>Strings.&lt;code&gt;.resx</c>.</summary>
    private static IEnumerable<ResourceFile> Translations()
    {
        return ResourceFiles().Where(file => file.Name != "Strings.resx");
    }

    private static IReadOnlyList<ResourceFile> ResourceFiles()
    {
        return Directory.EnumerateFiles(ResourcesFolder, "Strings*.resx")
            .Select(path => new ResourceFile(Path.GetFileName(path),
                XDocument.Load(path).Root!.Elements("data")
                    .Select(data => new KeyValuePair<string, string>((string)data.Attribute("name")!, (string?)data.Element("value") ?? ""))
                    .ToList()))
            .ToList();
    }

    /// <summary>Every string literal in Copy.cs outside comments, with what it is to the call it sits in.</summary>
    private static IReadOnlyList<CopyLiteral> CopyLiterals()
    {
        var source = File.ReadAllText(CopyFile);
        // the same text with every comment and literal blanked out, so a parenthesis in one never counts
        var code = new StringBuilder(source);
        var found = new List<(int Start, string Text, bool Interpolated)>();
        for (var i = 0; i < source.Length;)
        {
            int end;
            if (At(source, i, "//"))
            {
                end = source.IndexOf('\n', i);
                end = end < 0 ? source.Length : end;
            }
            else if (At(source, i, "/*"))
            {
                end = source.IndexOf("*/", i + 2, StringComparison.Ordinal) + 2;
            }
            else if (source[i] == '\'')
            {
                end = CharLiteralEnd(source, i);
            }
            else if (At(source, i, "@\"") || At(source, i, "$@") || At(source, i, "@$") || At(source, i, "\"\"\"") || At(source, i, "$$"))
            {
                throw new InvalidOperationException($"Copy.cs keeps to plain and $\"…\" strings; found another kind at {Position(source, i)}.");
            }
            else if (At(source, i, "$\"") || source[i] == '"')
            {
                var interpolated = source[i] == '$';
                (end, var text) = StringLiteral(source, interpolated ? i + 1 : i, interpolated);
                found.Add((i, text, interpolated));
            }
            else
            {
                i++;
                continue;
            }

            for (var blank = i; blank < end; blank++)
                if (code[blank] != '\n')
                    code[blank] = ' ';
            i = end;
        }

        var skeleton = code.ToString();
        return found.Select(literal => new CopyLiteral(literal.Text, literal.Interpolated, RoleOf(skeleton, literal.Start))).ToList();
    }

    /// <summary>A literal's place in the call around it: a line (<c>L</c>'s first argument, <c>Plural</c>'s second and third), the game's words, or loose.</summary>
    private static Role RoleOf(string code, int start)
    {
        var (function, argument) = EnclosingCall(code, start);
        return (function, argument) switch
        {
            ("L", 0) => Role.Line,
            ("Plural", 1 or 2) => Role.Line,
            ("Verbatim", 0) => Role.Verbatim,
            _ => Role.Loose
        };
    }

    private static (string? Function, int Argument) EnclosingCall(string code, int start)
    {
        var depth = 0;
        var commas = 0;
        for (var i = start - 1; i >= 0; i--)
        {
            var c = code[i];
            if (c is ')' or ']' or '}')
            {
                depth++;
            }
            else if (c is '(' or '[' or '{')
            {
                if (depth > 0)
                {
                    depth--;
                    continue;
                }

                if (c != '(')
                    return (null, 0);
                var nameEnd = i;
                while (nameEnd > 0 && char.IsWhiteSpace(code[nameEnd - 1]))
                    nameEnd--;
                var nameStart = nameEnd;
                while (nameStart > 0 && (char.IsLetterOrDigit(code[nameStart - 1]) || code[nameStart - 1] == '_'))
                    nameStart--;
                return (code[nameStart..nameEnd], commas);
            }
            else if (depth == 0 && c == ',')
            {
                commas++;
            }
            else if (depth == 0 && c == ';')
            {
                return (null, 0);
            }
        }

        return (null, 0);
    }

    private static (int End, string Text) StringLiteral(string source, int quote, bool interpolated)
    {
        var text = new StringBuilder();
        var i = quote + 1;
        while (i < source.Length)
        {
            var c = source[i];
            if (c == '"')
                return (i + 1, text.ToString());
            if (c == '\\')
            {
                var (escaped, length) = Unescape(source, i);
                text.Append(escaped);
                i += length;
                continue;
            }

            if (interpolated && (At(source, i, "{{") || At(source, i, "}}")))
            {
                text.Append(c).Append(c);
                i += 2;
                continue;
            }

            if (interpolated && c == '{')
            {
                // a hole is kept as written; a string inside one would hide from the scan, so there is none
                var close = i;
                var nesting = 0;
                for (; close < source.Length; close++)
                {
                    if (source[close] == '"')
                        throw new InvalidOperationException($"Copy.cs keeps string literals out of interpolation holes; found one at {Position(source, close)}.");
                    if (source[close] == '{')
                        nesting++;
                    else if (source[close] == '}' && --nesting == 0)
                        break;
                }

                text.Append(source, i, close - i + 1);
                i = close + 1;
                continue;
            }

            text.Append(c);
            i++;
        }

        throw new InvalidOperationException($"An unterminated string in Copy.cs at {Position(source, quote)}.");
    }

    private static (string Text, int Length) Unescape(string source, int backslash)
    {
        var next = source[backslash + 1];
        return next switch
        {
            'u' => (((char)int.Parse(source.AsSpan(backslash + 2, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture)).ToString(), 6),
            'n' => ("\n", 2),
            't' => ("\t", 2),
            'r' => ("\r", 2),
            '0' => ("\0", 2),
            _ => (next.ToString(), 2)
        };
    }

    private static int CharLiteralEnd(string source, int quote)
    {
        var i = quote + 1;
        i += source[i] == '\\' ? 2 : 1;
        while (source[i] != '\'')
            i++;
        return i + 1;
    }

    private static bool At(string source, int index, string token)
    {
        return string.CompareOrdinal(source, index, token, 0, token.Length) == 0;
    }

    private static string Position(string source, int index)
    {
        return $"line {source.Take(index).Count(c => c == '\n') + 1}";
    }

    [GeneratedRegex(@"\{[^{}]*\}")]
    private static partial Regex Hole();

    private sealed record CopyLiteral(string Text, bool Interpolated, Role Role);

    private sealed record ResourceFile(string Name, IReadOnlyList<KeyValuePair<string, string>> Entries);
}
