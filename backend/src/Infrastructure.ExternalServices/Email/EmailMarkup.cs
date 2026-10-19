using System.Text;

namespace PointLurker.Infrastructure.ExternalServices.Email;

/// <summary>Markup helpers shared by every email template.</summary>
internal static class EmailMarkup
{
    /// <summary>
    /// Escapes the five characters that carry meaning in markup, and nothing else.
    /// </summary>
    /// <remarks>
    /// <c>WebUtility.HtmlEncode</c> also turns every character above ASCII into a numeric
    /// character reference, which would scatter escapes through the Portuguese catalog's accented
    /// words inside a document that already declares UTF-8. Accented text is not a markup hazard,
    /// so it travels as itself.
    /// </remarks>
    public static string Encode(string value)
    {
        var encoded = new StringBuilder(value.Length);

        foreach (var c in value)
        {
            switch (c)
            {
                case '&':
                    encoded.Append("&amp;");
                    break;
                case '<':
                    encoded.Append("&lt;");
                    break;
                case '>':
                    encoded.Append("&gt;");
                    break;
                case '"':
                    encoded.Append("&quot;");
                    break;
                case '\'':
                    encoded.Append("&#39;");
                    break;
                default:
                    encoded.Append(c);
                    break;
            }
        }

        return encoded.ToString();
    }
}
