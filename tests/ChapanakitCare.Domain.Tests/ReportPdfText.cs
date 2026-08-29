using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;

namespace ChapanakitCare.Domain.Tests;

internal static class ReportPdfText
{
    public static string Extract(byte[] pdf)
    {
        var source = Encoding.Latin1.GetString(pdf);
        var objects = ParseObjects(source);
        var fontMaps = ParseFontMaps(objects);
        var text = new StringBuilder();

        foreach (var contentObjectId in ContentObjectIds(objects))
        {
            AppendContentText(text, Inflate(objects[contentObjectId]), fontMaps);
            text.AppendLine();
        }

        return text.ToString();
    }

    private static Dictionary<int, string> ParseObjects(string source) => ObjectPattern.Matches(source)
        .ToDictionary(
            match => ParseInt(match.Groups["id"].Value),
            match => match.Groups["contents"].Value);

    private static IReadOnlyDictionary<string, IReadOnlyDictionary<int, string>> ParseFontMaps(IReadOnlyDictionary<int, string> objects)
    {
        var unicodeMapsByFontObject = new Dictionary<int, IReadOnlyDictionary<int, string>>();

        foreach (var (objectId, contents) in objects)
        {
            var toUnicode = ToUnicodePattern.Match(contents);
            if (!toUnicode.Success) continue;

            var unicodeObjectId = ParseInt(toUnicode.Groups["id"].Value);
            unicodeMapsByFontObject[objectId] = ParseCMap(Inflate(objects[unicodeObjectId]));
        }

        var mapsByFontName = new Dictionary<string, IReadOnlyDictionary<int, string>>();
        foreach (var contents in objects.Values)
        {
            foreach (Match font in FontPattern.Matches(contents))
            {
                var fontObjectId = ParseInt(font.Groups["id"].Value);
                if (unicodeMapsByFontObject.TryGetValue(fontObjectId, out var map))
                {
                    mapsByFontName[font.Groups["name"].Value] = map;
                }
            }
        }

        return mapsByFontName;
    }

    private static IReadOnlyDictionary<int, string> ParseCMap(string cMap)
    {
        var map = new Dictionary<int, string>();

        foreach (Match section in BfCharSectionPattern.Matches(cMap))
        {
            foreach (Match entry in CMapPairPattern.Matches(section.Groups["entries"].Value))
            {
                map[ParseHex(entry.Groups["source"].Value)] = DecodeUnicode(entry.Groups["target"].Value);
            }
        }

        foreach (Match section in BfRangeSectionPattern.Matches(cMap))
        {
            foreach (Match entry in CMapRangePattern.Matches(section.Groups["entries"].Value))
            {
                var first = ParseHex(entry.Groups["first"].Value);
                var last = ParseHex(entry.Groups["last"].Value);
                var unicode = ParseHex(entry.Groups["target"].Value);

                for (var code = first; code <= last; code++)
                {
                    map[code] = char.ConvertFromUtf32(unicode + code - first);
                }
            }
        }

        return map;
    }

    private static IEnumerable<int> ContentObjectIds(IReadOnlyDictionary<int, string> objects)
    {
        foreach (var contents in objects.Values.Where(value => PagePattern.IsMatch(value)))
        {
            var content = ContentsPattern.Match(contents);
            if (!content.Success) continue;

            if (content.Groups["single"].Success)
            {
                yield return ParseInt(content.Groups["single"].Value);
                continue;
            }

            foreach (Match entry in ObjectReferencePattern.Matches(content.Groups["many"].Value))
            {
                yield return ParseInt(entry.Groups["id"].Value);
            }
        }
    }

    private static void AppendContentText(StringBuilder output, string content, IReadOnlyDictionary<string, IReadOnlyDictionary<int, string>> fontMaps)
    {
        var currentFont = string.Empty;
        var textContent = ActualTextPattern.Replace(content, string.Empty);

        foreach (Match token in ContentTokenPattern.Matches(textContent))
        {
            if (token.Groups["font"].Success)
            {
                currentFont = token.Groups["font"].Value;
                continue;
            }

            if (!fontMaps.TryGetValue(currentFont, out var unicodeMap)) continue;

            var glyphs = token.Groups["glyphs"].Value;
            for (var index = 0; index + 3 < glyphs.Length; index += 4)
            {
                var glyph = ParseHex(glyphs.Substring(index, 4));
                if (unicodeMap.TryGetValue(glyph, out var value)) output.Append(value);
            }
        }
    }

    private static string Inflate(string objectContents)
    {
        if (!objectContents.Contains("/Filter /FlateDecode", StringComparison.Ordinal))
        {
            throw new InvalidDataException("Expected a Flate-compressed PDF stream.");
        }

        var streamStart = StreamStartPattern.Match(objectContents);
        var length = LengthPattern.Match(objectContents);
        if (!streamStart.Success || !length.Success)
        {
            throw new InvalidDataException("Expected a PDF stream with an explicit length.");
        }

        var bytes = Encoding.Latin1.GetBytes(objectContents);
        var start = streamStart.Index + streamStart.Length;
        var streamLength = ParseInt(length.Groups["length"].Value);

        using var compressed = new MemoryStream(bytes, start, streamLength, writable: false);
        using var zlib = new ZLibStream(compressed, CompressionMode.Decompress);
        using var reader = new StreamReader(zlib, Encoding.Latin1);
        return reader.ReadToEnd();
    }

    private static int ParseInt(string value) => int.Parse(value, NumberStyles.None, CultureInfo.InvariantCulture);
    private static int ParseHex(string value) => int.Parse(value, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
    private static string DecodeUnicode(string value) => Encoding.BigEndianUnicode.GetString(Convert.FromHexString(value));

    private static readonly Regex ObjectPattern = new("(?ms)^(?<id>\\d+)\\s+\\d+\\s+obj\\s*(?<contents>.*?)\\s*endobj", RegexOptions.Compiled);
    private static readonly Regex ToUnicodePattern = new("/ToUnicode\\s+(?<id>\\d+)\\s+\\d+\\s+R", RegexOptions.Compiled);
    private static readonly Regex FontPattern = new("/(?<name>F\\d+)\\s+(?<id>\\d+)\\s+\\d+\\s+R", RegexOptions.Compiled);
    private static readonly Regex BfCharSectionPattern = new("(?s)\\d+\\s+beginbfchar(?<entries>.*?)endbfchar", RegexOptions.Compiled);
    private static readonly Regex BfRangeSectionPattern = new("(?s)\\d+\\s+beginbfrange(?<entries>.*?)endbfrange", RegexOptions.Compiled);
    private static readonly Regex CMapPairPattern = new("<(?<source>[0-9A-Fa-f]+)>\\s*<(?<target>[0-9A-Fa-f]+)>", RegexOptions.Compiled);
    private static readonly Regex CMapRangePattern = new("<(?<first>[0-9A-Fa-f]+)>\\s*<(?<last>[0-9A-Fa-f]+)>\\s*<(?<target>[0-9A-Fa-f]+)>", RegexOptions.Compiled);
    private static readonly Regex PagePattern = new("/Type\\s*/Page\\b", RegexOptions.Compiled);
    private static readonly Regex ContentsPattern = new("(?s)/Contents\\s+(?:(?<single>\\d+)\\s+\\d+\\s+R|\\[(?<many>.*?)\\])", RegexOptions.Compiled);
    private static readonly Regex ObjectReferencePattern = new("(?<id>\\d+)\\s+\\d+\\s+R", RegexOptions.Compiled);
    private static readonly Regex ActualTextPattern = new("/ActualText\\s+<[^>]+>", RegexOptions.Compiled);
    private static readonly Regex ContentTokenPattern = new("/(?<font>F\\d+)\\s+[0-9.]+\\s+Tf|<(?<glyphs>[0-9A-Fa-f]+)>", RegexOptions.Compiled);
    private static readonly Regex StreamStartPattern = new("stream\\r?\\n", RegexOptions.Compiled);
    private static readonly Regex LengthPattern = new("/Length\\s+(?<length>\\d+)", RegexOptions.Compiled);
}
