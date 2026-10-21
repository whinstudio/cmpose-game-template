using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

namespace Kturker.UnityBase.Tests.EditMode
{
    // The economy's promise: a full-screen ad comes only after the end panel, never on pause. Generated games copy
    // this test, so game code that shows an interstitial itself fails the gate.
    public sealed class AdCallSitesTests
    {
        // A call or a method group; declarations have no dot before the name
        private static readonly Regex Call = new Regex(@"\.\s*Show(?:Interstitial|Break)Async\b");

        // The files that may make those calls, by path under Assets/_Project
        private static readonly HashSet<string> Allowed = new HashSet<string>
        {
            // The shell's break after the game-over or level card
            "Features/Shell/GameShell/GameShell.cs",
            // The shell's break over IAds
            "Features/Shell/GameShell/RewardedShellAds.cs",
            // IAds over the Google Mobile Ads gateway
            "Core/Ads/GmaAds.cs",
            // The template's own showcase games have no shell (ShowcaseArchitectureTests): their finished run is their
            // end panel. Compass never scaffolds the showcase kind, so no generated game has this file.
            "Features/Showcase/Common/Ads/ShowcaseContinuePresenter.cs",
        };

        [Test]
        public void Only_the_shell_shows_full_screen_ads()
        {
            var root = Path.Combine(Application.dataPath, "_Project").Replace('\\', '/').TrimEnd('/');
            var offenders = Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories)
                .Select(path => path.Replace('\\', '/').Substring(root.Length).TrimStart('/'))
                // Inside the project only: a folder named Tests above Assets must not hide every file
                .Where(path => !("/" + path).Contains("/Tests/"))
                .Where(path => !Allowed.Contains(path))
                .Where(path => Call.IsMatch(File.ReadAllText(root + "/" + path)))
                .ToList();

            Assert.That(offenders, Is.Empty,
                "full-screen ads come only after the end panel (GameShell); never call them from game code or pause: " +
                string.Join(", ", offenders));
        }
    }
}
