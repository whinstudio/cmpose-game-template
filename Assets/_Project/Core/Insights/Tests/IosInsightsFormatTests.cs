using System.Collections.Generic;
using NUnit.Framework;

namespace Kturker.UnityBase.Core.Insights.Tests
{
    public sealed class IosInsightsFormatTests
    {
        [Test]
        public void Parameters_become_a_json_object_of_strings_integers_and_doubles()
        {
            var json = IosInsightsFormat.ToJson(new Dictionary<string, object>
            {
                ["level"] = 3,
                ["coins"] = 1234567890123L,
                ["ratio"] = 0.25,
                ["mode"] = "hard",
                ["won"] = true,
                ["skipped"] = null,
            });

            Assert.AreEqual("{\"level\":3,\"coins\":1234567890123,\"ratio\":0.25,\"mode\":\"hard\",\"won\":\"True\"}", json);
        }

        [Test]
        public void No_parameters_is_an_empty_object()
        {
            Assert.AreEqual("{}", IosInsightsFormat.ToJson(new Dictionary<string, object>()));
        }

        [Test]
        public void Quotes_backslashes_and_control_characters_are_escaped()
        {
            var json = IosInsightsFormat.ToJson(new Dictionary<string, object> { ["a\"b"] = "c\\d\ne\u0001" });

            Assert.AreEqual("{\"a\\\"b\":\"c\\\\d\\u000ae\\u0001\"}", json);
        }

        [Test]
        public void Not_finite_numbers_are_sent_as_text()
        {
            var json = IosInsightsFormat.ToJson(new Dictionary<string, object> { ["x"] = double.NaN, ["y"] = float.PositiveInfinity });

            StringAssert.StartsWith("{\"x\":\"", json);
            StringAssert.DoesNotContain(":NaN", json);
            StringAssert.DoesNotContain(":Infinity", json);
        }

        [Test]
        public void Frames_are_one_tab_separated_line_each_with_an_empty_file_when_missing()
        {
            var tsv = IosInsightsFormat.ToTsv(new[]
            {
                new StackFrame("Game.Board:Drop (int)", "Assets/Game/Board.cs", 42),
                new StackFrame("Game.Loop:Update ()", null, 0),
            });

            Assert.AreEqual("Game.Board:Drop (int)\tAssets/Game/Board.cs\t42\nGame.Loop:Update ()\t\t0\n", tsv);
        }

        [Test]
        public void Tabs_and_line_breaks_inside_a_frame_cannot_split_it()
        {
            var tsv = IosInsightsFormat.ToTsv(new[] { new StackFrame("A\tB\nC", "d\r\ne", 1) });

            Assert.AreEqual("A B C\td  e\t1\n", tsv);
        }

        [Test]
        public void A_frame_without_a_symbol_keeps_its_columns()
        {
            Assert.AreEqual("\t\t0\n", IosInsightsFormat.ToTsv(new[] { new StackFrame(null, null, 0) }));
        }
    }
}
