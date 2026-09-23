using System.Text.RegularExpressions;
using System.Xml.Linq;
using PiuScoresWatcher.Tests.TestHelpers;

namespace PiuScoresWatcher.Tests.ArchitectureTests;

/// <summary>
///     Every string a player reads lives in <c>src/PiuScoresWatcher.App/Copy.cs</c> (watcher.md D36), so the
///     owner rewrites the copy in one file and nothing is missed. A window's text binds to it with
///     <c>{x:Static}</c>; a text property set in code reads from it. Values with no letter in them — a
///     step number, a separator, an empty string — are not copy.
/// </summary>
public sealed partial class CopyLivesInOneFileTests
{
    private static readonly HashSet<string> TextProperties = ["Text", "Content", "Header", "Title", "ToolTip", "ToolTipText"];

    [Fact]
    public void NoWindowSpellsItsOwnText()
    {
        var offenders = RepositoryFiles.Files("src", "*.xaml")
            .SelectMany(file => LiteralText(XDocument.Load(file)).Select(text => $"{RepositoryFiles.Relative(file)}: {text}"))
            .ToArray();

        Assert.Empty(offenders);
    }

    [Fact]
    public void NoCodeSetsTextOutsideTheCopyFile()
    {
        var offenders = RepositoryFiles.Sources("src/PiuScoresWatcher.App")
            .Where(file => !file.EndsWith($"{Path.DirectorySeparatorChar}Copy.cs", StringComparison.Ordinal))
            .SelectMany(file => TextAssignment().Matches(File.ReadAllText(file))
                .Where(match => HasLetters(Holes().Replace(match.Groups["literal"].Value, "")))
                .Select(match => $"{RepositoryFiles.Relative(file)}: {match.Value}"))
            .ToArray();

        Assert.Empty(offenders);
    }

    private static IEnumerable<string> LiteralText(XDocument xaml)
    {
        foreach (var element in xaml.Descendants())
        {
            foreach (var attribute in element.Attributes().Where(a => TextProperties.Contains(a.Name.LocalName)))
                if (!attribute.Value.StartsWith('{') && HasLetters(attribute.Value))
                    yield return $"{element.Name.LocalName} {attribute.Name.LocalName}=\"{attribute.Value}\"";
            foreach (var text in element.Nodes().OfType<XText>())
                if (HasLetters(text.Value))
                    yield return $"{element.Name.LocalName} >{text.Value.Trim()}<";
        }
    }

    private static bool HasLetters(string value)
    {
        return value.Any(char.IsLetter);
    }

    [GeneratedRegex(@"\b(?:Text|Content|Header|Title|ToolTip|ToolTipText)\s*=\s*\$?""(?<literal>(?:[^""\\]|\\.)*)""")]
    private static partial Regex TextAssignment();

    [GeneratedRegex(@"\{[^{}]*\}")]
    private static partial Regex Holes();
}
