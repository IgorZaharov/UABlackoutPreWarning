// UABlackoutPreWarning - warns before scheduled power outages (ZTOE schedule).
// Copyright (C) 2026 Igor Zakharov
// Licensed under the GNU General Public License v3.0 or later. See LICENSE.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace UABlackoutPreWarning.Core
{
    /// <summary>One entry of donations.json: a title and an https link.</summary>
    public sealed class DonationLink
    {
        public DonationLink(string title, string url)
        {
            Title = title;
            Url = url;
        }

        public string Title { get; private set; }
        public string Url { get; private set; }

        /// <summary>The link's host without "www.", e.g. "send.monobank.ua".</summary>
        public string Host
        {
            get
            {
                var host = new Uri(Url).Host;
                return host.StartsWith("www.", StringComparison.OrdinalIgnoreCase) ? host.Substring(4) : host;
            }
        }
    }

    /// <summary>
    /// Reads donations.json (the same shape as the Android app's file): a JSON array of objects with
    /// string fields "title" and "url". A small hand-written reader, so no JSON library is needed.
    /// Entries without a title or with a link that is not absolute https are skipped.
    /// </summary>
    public static class DonationList
    {
        public const int MaxEntries = 20;
        public const int MaxTitleLength = 80;

        /// <summary>The valid entries; an empty list when the text is not a JSON array of objects.</summary>
        public static List<DonationLink> Parse(string json)
        {
            var result = new List<DonationLink>();
            if (string.IsNullOrEmpty(json)) return result;
            try
            {
                var reader = new Reader(json);
                reader.Expect('[');
                if (reader.TryConsume(']')) return result;
                do
                {
                    var fields = reader.ReadObject();
                    string title, url;
                    fields.TryGetValue("title", out title);
                    fields.TryGetValue("url", out url);
                    title = CleanTitle(title);
                    if (title.Length > 0 && IsHttpsUrl(url) && result.Count < MaxEntries)
                    {
                        result.Add(new DonationLink(title, new Uri(url.Trim()).AbsoluteUri));
                    }
                }
                while (reader.TryConsume(','));
                reader.Expect(']');
            }
            catch (FormatException)
            {
                result.Clear();
            }
            return result;
        }

        /// <summary>
        /// One line of plain text: control and format characters (newlines, bidi overrides such as U+202E)
        /// removed, cut at <see cref="MaxTitleLength"/> without splitting a surrogate pair.
        /// </summary>
        private static string CleanTitle(string title)
        {
            if (title == null) return "";
            var sb = new StringBuilder(title.Length);
            foreach (char c in title)
            {
                var category = char.GetUnicodeCategory(c);
                if (category == UnicodeCategory.Control || category == UnicodeCategory.Format) sb.Append(c == '\n' || c == '\t' ? ' ' : '\0');
                else sb.Append(c);
            }
            var clean = sb.ToString().Replace("\0", "").Trim();
            if (clean.Length <= MaxTitleLength) return clean;
            int cut = MaxTitleLength;
            if (char.IsHighSurrogate(clean[cut - 1])) cut--;
            return clean.Substring(0, cut).TrimEnd() + "…";
        }

        public static bool IsHttpsUrl(string value)
        {
            if (string.IsNullOrEmpty(value)) return false;
            Uri uri;
            return Uri.TryCreate(value.Trim(), UriKind.Absolute, out uri) && uri.Scheme == Uri.UriSchemeHttps;
        }

        /// <summary>
        /// Just enough JSON: the string fields of each object are kept; numbers, booleans, null and nested
        /// arrays or objects are read and skipped, so a field added later does not hide the whole list.
        /// </summary>
        private sealed class Reader
        {
            private readonly string text;
            private int pos;

            public Reader(string text)
            {
                this.text = text;
                if (text.Length > 0 && text[0] == '﻿') pos = 1;
            }

            public void Expect(char c)
            {
                if (!TryConsume(c)) throw new FormatException("Expected '" + c + "' at " + pos);
            }

            public bool TryConsume(char c)
            {
                SkipSpace();
                if (pos < text.Length && text[pos] == c)
                {
                    pos++;
                    return true;
                }
                return false;
            }

            /// <summary>An object's string fields; other value types are read and ignored.</summary>
            public Dictionary<string, string> ReadObject()
            {
                return ReadObject(0);
            }

            private const int MaxDepth = 16;

            private Dictionary<string, string> ReadObject(int depth)
            {
                if (depth > MaxDepth) throw new FormatException("Nested too deeply at " + pos);
                var fields = new Dictionary<string, string>(StringComparer.Ordinal);
                Expect('{');
                if (TryConsume('}')) return fields;
                do
                {
                    SkipSpace();
                    var name = ReadString();
                    Expect(':');
                    SkipSpace();
                    if (pos < text.Length && text[pos] == '"') fields[name] = ReadString();
                    else SkipValue(depth + 1);
                }
                while (TryConsume(','));
                Expect('}');
                return fields;
            }

            private void SkipValue(int depth)
            {
                if (depth > MaxDepth) throw new FormatException("Nested too deeply at " + pos);
                SkipSpace();
                if (pos >= text.Length) throw new FormatException("Unexpected end");
                char c = text[pos];
                if (c == '"')
                {
                    ReadString();
                }
                else if (c == '{')
                {
                    ReadObject(depth);
                }
                else if (c == '[')
                {
                    pos++;
                    if (TryConsume(']')) return;
                    do
                    {
                        SkipValue(depth + 1);
                    }
                    while (TryConsume(','));
                    Expect(']');
                }
                else
                {
                    SkipScalar();
                }
            }

            private string ReadString()
            {
                if (pos >= text.Length || text[pos] != '"') throw new FormatException("Expected a string at " + pos);
                pos++;
                var sb = new StringBuilder();
                while (pos < text.Length)
                {
                    char c = text[pos++];
                    if (c == '"') return sb.ToString();
                    if (c != '\\')
                    {
                        sb.Append(c);
                        continue;
                    }
                    if (pos >= text.Length) break;
                    char e = text[pos++];
                    switch (e)
                    {
                        case '"': sb.Append('"'); break;
                        case '\\': sb.Append('\\'); break;
                        case '/': sb.Append('/'); break;
                        case 'b': sb.Append('\b'); break;
                        case 'f': sb.Append('\f'); break;
                        case 'n': sb.Append('\n'); break;
                        case 'r': sb.Append('\r'); break;
                        case 't': sb.Append('\t'); break;
                        case 'u':
                            int code;
                            if (pos + 4 > text.Length ||
                                !int.TryParse(text.Substring(pos, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out code))
                            {
                                throw new FormatException("Bad \\u escape at " + pos);
                            }
                            sb.Append((char)code);
                            pos += 4;
                            break;
                        default:
                            throw new FormatException("Bad escape at " + pos);
                    }
                }
                throw new FormatException("Unterminated string");
            }

            private void SkipScalar()
            {
                int start = pos;
                while (pos < text.Length && text[pos] != ',' && text[pos] != '}' && text[pos] != ']' &&
                       !char.IsWhiteSpace(text[pos])) pos++;
                var word = text.Substring(start, pos - start);
                double number;
                if (word != "true" && word != "false" && word != "null" &&
                    !double.TryParse(word, NumberStyles.Float, CultureInfo.InvariantCulture, out number))
                {
                    throw new FormatException("Unsupported value at " + start);
                }
            }

            private void SkipSpace()
            {
                while (pos < text.Length && char.IsWhiteSpace(text[pos])) pos++;
            }
        }
    }
}
