#nullable enable

using System;
using System.Text;

namespace OperatorConsole
{
    /// <summary>Rich text for the Log tab: a message shown as plain text, with the search matches marked.</summary>
    internal static class OCLogMarkup
    {
        private const string MarkOpen = "<mark=#FFC83D99><color=#FFFFFF><b>";
        private const string MarkClose = "</b></color></mark>";
        private const string NoParseOpen = "<noparse>";
        private const string NoParseClose = "</noparse>";
        // A zero-width space keeps a literal "</noparse>" in the message from ending the escape.
        private const string EscapedNoParseClose = "<\u200B/noparse>";

        private static readonly StringBuilder s_builder = new();

        public static bool Matches(string text, string query) =>
            query.Length > 0 && text.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;

        /// <summary>Rich text that shows <paramref name="text"/> as written, with every case-insensitive match of <paramref name="query"/> marked. Main thread only.</summary>
        public static string Highlight(string text, string query)
        {
            var b = s_builder.Clear();
            var start = 0;
            if (query.Length > 0)
            {
                int match;
                while ((match = text.IndexOf(query, start, StringComparison.OrdinalIgnoreCase)) >= 0)
                {
                    AppendText(b, text, start, match - start);
                    b.Append(MarkOpen);
                    AppendText(b, text, match, query.Length);
                    b.Append(MarkClose);
                    start = match + query.Length;
                }
            }
            AppendText(b, text, start, text.Length - start);
            return b.ToString();
        }

        private static void AppendText(StringBuilder b, string text, int start, int length)
        {
            if (length <= 0) return;
            var at = b.Length;
            b.Append(NoParseOpen).Append(text, start, length);
            b.Replace(NoParseClose, EscapedNoParseClose, at, b.Length - at);
            b.Append(NoParseClose);
        }
    }
}
