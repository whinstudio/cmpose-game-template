using System;
using System.Collections;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Kturker.UnityBase.Core.Domain;
using Kturker.UnityBase.Core.Navigation;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace Kturker.UnityBase.Tests.EditMode
{
    public sealed class SceneLoaderTests
    {
        [UnityTest]
        public IEnumerator Loads_the_scene_by_name_behind_the_overlay() => UniTask.ToCoroutine(async () =>
        {
            var gateway = new FakeGateway();
            var overlay = new FakeOverlay();
            var log = new FakeLog();
            var loader = new SceneLoader(gateway, overlay, log);

            await loader.LoadAsync(SceneId.Game);

            Assert.That(gateway.Loaded, Is.EqualTo(new[] { "Game" }));
            Assert.That(overlay.Shown, Is.EqualTo(1));
            Assert.That(overlay.Visible, Is.False);
            Assert.That(loader.IsLoading, Is.False);
            Assert.That(log.Lines, Does.Contain("MARKER CMPOSE_SCENE Game"));
        });

        // What the scene's first visible frame needs (the banner's room) is set while the overlay still covers it
        [UnityTest]
        public IEnumerator Revealing_runs_behind_the_overlay_and_loaded_after_it_hides() => UniTask.ToCoroutine(async () =>
        {
            var overlay = new FakeOverlay();
            var loader = new SceneLoader(new FakeGateway(), overlay, new FakeLog());
            var order = new List<string>();
            overlay.OnHide = () => order.Add("hide");
            loader.Revealing += id => order.Add($"revealing {id} covered={overlay.Visible} loading={loader.IsLoading}");
            loader.Loaded += id => order.Add($"loaded {id} covered={overlay.Visible} loading={loader.IsLoading}");

            await loader.LoadAsync(SceneId.Menu);

            Assert.That(order, Is.EqualTo(new[]
            {
                "revealing Menu covered=True loading=True",
                "hide",
                "loaded Menu covered=False loading=False"
            }));
        });

        [UnityTest]
        public IEnumerator A_failed_load_reveals_nothing() => UniTask.ToCoroutine(async () =>
        {
            var loader = new SceneLoader(new FakeGateway { Failure = new InvalidOperationException("no scene") }, new FakeOverlay(),
                new FakeLog());
            var revealed = 0;
            loader.Revealing += _ => revealed++;

            try { await loader.LoadAsync(SceneId.Game); }
            catch (InvalidOperationException) { }

            Assert.That(revealed, Is.Zero);
        });

        [UnityTest]
        public IEnumerator A_request_while_loading_is_ignored() => UniTask.ToCoroutine(async () =>
        {
            var gateway = new FakeGateway { Pending = new UniTaskCompletionSource() };
            var log = new FakeLog();
            var loader = new SceneLoader(gateway, new FakeOverlay(), log);

            var first = loader.LoadAsync(SceneId.Game);
            Assert.That(loader.IsLoading, Is.True);
            await loader.LoadAsync(SceneId.Menu);

            Assert.That(gateway.Loaded, Is.EqualTo(new[] { "Game" }));
            Assert.That(log.Lines, Does.Contain("WARNING Scene load ignored while another is running: Menu"));
            gateway.Pending.TrySetResult();
            await first;
            Assert.That(loader.IsLoading, Is.False);
        });

        [UnityTest]
        public IEnumerator A_failed_load_hides_the_overlay_and_rethrows() => UniTask.ToCoroutine(async () =>
        {
            var gateway = new FakeGateway { Failure = new InvalidOperationException("no scene") };
            var overlay = new FakeOverlay();
            var loader = new SceneLoader(gateway, overlay, new FakeLog());

            var thrown = false;
            try { await loader.LoadAsync(SceneId.Game); }
            catch (InvalidOperationException) { thrown = true; }

            Assert.That(thrown, Is.True);
            Assert.That(overlay.Visible, Is.False);
            Assert.That(loader.IsLoading, Is.False);
        });

        [UnityTest]
        public IEnumerator Loaded_fires_after_success_only_and_allows_a_follow_up_load() => UniTask.ToCoroutine(async () =>
        {
            var gateway = new FakeGateway();
            var loader = new SceneLoader(gateway, new FakeOverlay(), new FakeLog());
            var loaded = new List<SceneId>();
            loader.Loaded += id =>
            {
                loaded.Add(id);
                // A deep link applied when the menu opens loads the next scene from inside this event
                if (id == SceneId.Menu) loader.LoadAsync(SceneId.Game).Forget();
            };

            await loader.LoadAsync(SceneId.Menu);

            Assert.That(gateway.Loaded, Is.EqualTo(new[] { "Menu", "Game" }));
            Assert.That(loaded, Is.EqualTo(new[] { SceneId.Menu, SceneId.Game }));

            gateway.Failure = new InvalidOperationException("no scene");
            try { await loader.LoadAsync(SceneId.Boot); }
            catch (InvalidOperationException) { }

            Assert.That(loaded, Is.EqualTo(new[] { SceneId.Menu, SceneId.Game }));
        });
    }
}
