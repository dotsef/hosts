using System.Net;
using System.Text.RegularExpressions;

var path = Environment.OSVersion.Platform switch
{
    PlatformID.Unix => "/etc/hosts",
    PlatformID.Win32NT => @"C:\Windows\System32\drivers\etc\hosts",
    _ => throw new InvalidOperationException("Unsupported platform")
};

var hostsFileLines = File.ReadAllLines(path).Select((line, index) => new HostsLine(line, index));
var entryLines = hostsFileLines
    .WithoutComments()
    .WithoutEmptyLines()
    .SelectValidHostEntries();

Action command = args switch
{
    ["clear", var host] => () => File.WriteAllLines(path, hostsFileLines
        .Where(line => line.IsComment() || line.IsWhiteSpace() || line.Text.Contains(host) is false)
        .Select(line => line.Text)),

    [var address, var host] => () =>
    {
        var existingEntry = hostsFileLines
            .SelectValidHostEntries()
            .FirstOrDefault(entry => entry.Name == host);

        if (existingEntry is not null)
        {
            File.WriteAllLines(path, hostsFileLines
                .Select(line => line.Index == existingEntry.Index
                    ? $"{address} {host}"
                    : line.Text));
        }
        else
        {
            File.AppendAllLines(path, new[] { $"{address} {host}" });
        }
    }
    ,
    [] => () =>
    {
        foreach (var entry in entryLines)
        {
            Console.WriteLine($"{entry.Address} {entry.Name}");
        }
    }
    ,

    [.. var rest] => () => Console.WriteLine("""
        Invalid arguments
        
        Usage:

            hosts [address] [host]
            hosts clear [host]
        """),
};

command();

internal record HostName(IPAddress Address, string Name, int Index);
internal record HostsLine(string Text, int Index);

internal static partial class Ext
{
    [GeneratedRegex("^\\s*(?<address>\\d{1,3}\\.\\d{1,3}\\.\\d{1,3}\\.\\d{1,3})\\s+(?<hostname>\\S+)\\s*$", RegexOptions.Compiled | RegexOptions.Singleline)]
    internal static partial Regex HostsFileEntryRegex();

    public static bool IsComment(this HostsLine line)
        => line.Text.TrimStart().StartsWith('#');

    public static bool IsWhiteSpace(this HostsLine line)
        => string.IsNullOrWhiteSpace(line.Text);

    public static IEnumerable<HostsLine> WithoutComments(this IEnumerable<HostsLine> lines)
        => lines.Where(line => line.IsComment() is false);

    public static IEnumerable<HostsLine> WithoutEmptyLines(this IEnumerable<HostsLine> lines)
        => lines.Where(line => line.IsWhiteSpace() is false);

    public static bool HasHostName(this HostsLine line, string hostName)
    {
        var match = HostsFileEntryRegex().Match(line.Text);
        return match.Success && match.Groups["hostname"].Value == hostName;
    }

    public static IEnumerable<HostName> SelectValidHostEntries(this IEnumerable<HostsLine> lines) => lines
        .Select(line => (line.Index, match: HostsFileEntryRegex().Match(line.Text)))
        .Where(t => t.match.Success)
        .Select(t => new HostName(IPAddress.Parse(t.match.Groups["address"].Value), t.match.Groups["hostname"].Value, t.Index));
}
