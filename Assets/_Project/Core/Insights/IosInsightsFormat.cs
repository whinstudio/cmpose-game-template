using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Kturker.UnityBase.Core.Insights
{
    // What IosInsightsGateway hands to CmposeInsights.mm: event parameters as a JSON object and stack frames as
    // "symbol\tfile\tline" lines. Kept out of the iOS-only gateway so EditMode tests reach it.
    public static class IosInsightsFormat
    {
        // Firebase takes strings, integers and doubles; null values are left out, any other value is sent as its text
        // (as on Android)
        public static string ToJson(IReadOnlyDictionary<string, object> parameters)
        {
            var json = new StringBuilder("{");
            foreach (var pair in parameters)
            {
                if (pair.Value == null) continue;
                if (json.Length > 1) json.Append(',');
                AppendString(json, pair.Key);
                json.Append(':');
                switch (pair.Value)
                {
                    case long or int or short or byte or sbyte or ushort or uint:
                        json.Append(Convert.ToInt64(pair.Value).ToString(CultureInfo.InvariantCulture));
                        break;
                    case double or float or decimal:
                        AppendNumber(json, Convert.ToDouble(pair.Value));
                        break;
                    case string text:
                        AppendString(json, text);
                        break;
                    default:
                        AppendString(json, Convert.ToString(pair.Value, CultureInfo.InvariantCulture));
                        break;
                }
            }
            return json.Append('}').ToString();
        }

        // One line per frame, file empty when the trace has none; tabs and line breaks inside a field become spaces
        public static string ToTsv(IReadOnlyList<StackFrame> frames)
        {
            var text = new StringBuilder();
            foreach (var frame in frames)
            {
                text.Append(Field(frame.Symbol)).Append('\t').Append(Field(frame.File)).Append('\t')
                    .Append(frame.Line.ToString(CultureInfo.InvariantCulture)).Append('\n');
            }
            return text.ToString();
        }

        private static string Field(string value) =>
            value == null ? string.Empty : value.Replace('\t', ' ').Replace('\r', ' ').Replace('\n', ' ');

        // JSON has no NaN or infinity: those go as text
        private static void AppendNumber(StringBuilder json, double number)
        {
            if (double.IsNaN(number) || double.IsInfinity(number))
                AppendString(json, number.ToString(CultureInfo.InvariantCulture));
            else json.Append(number.ToString("R", CultureInfo.InvariantCulture));
        }

        private static void AppendString(StringBuilder json, string value)
        {
            json.Append('"');
            foreach (var c in value ?? string.Empty)
            {
                switch (c)
                {
                    case '"':
                        json.Append("\\\"");
                        break;
                    case '\\':
                        json.Append("\\\\");
                        break;
                    default:
                        if (c < ' ') json.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                        else json.Append(c);
                        break;
                }
            }
            json.Append('"');
        }
    }
}
