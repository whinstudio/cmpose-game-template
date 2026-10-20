using System;
using System.Threading;
using Kturker.UnityBase.Core.Presentation;
using UnityEngine;

namespace Kturker.UnityBase.Features.Economy
{
    // The economy's window frame for a custom extra: a centred modal with the extra's name as its title, an empty
    // Content area and Close (SceneBuilder builds it into Resources/Economy/EconomyWindows)
    public interface IExtraWindowView
    {
        event Action CloseClicked;
        // The area the extra fills (anchored to the frame, between the title and Close)
        RectTransform Content { get; }
        void SetOpen(bool open);
        void ShowTitle(string key);
        // Close is disabled while busy
        void SetBusy(bool busy);
    }

    // What a custom extra puts into the frame (IEconomyWindows.OpenExtra). The frame never opens with the economy off,
    // during an AutoPlay run or for an id that is not a custom extra of GameEconomy.json; while it is open the menu's
    // banner is hidden.
    public interface IEconomyExtraContent
    {
        // The extra's id in GameEconomy.json (kind "custom"); the title reads economy.extra.<id>.name
        string ExtraId { get; }

        // The frame opened: build the content under parent the first time (KitFactory parts; the frame binds the theme
        // and the texts after Show), then fill it. Keep frame to mark an ad or an animation busy and to close.
        void Show(RectTransform parent, IEconomyExtraFrame frame);

        // Once a second while open and not busy (unscaled), for timers
        void Tick();

        // The frame closed (its Token is cancelled)
        void Hide();
    }

    // The open frame as its content sees it
    public interface IEconomyExtraFrame
    {
        // Cancelled when the frame closes: an ad or a spin in progress gives nothing to a closed frame
        CancellationToken Token { get; }

        // While busy (an ad on screen) Close is disabled and Tick waits
        bool Busy { get; set; }

        void Close();
    }

    // The frame's presenter: one content at a time
    public sealed class ExtraWindow : EconomyWindow, IEconomyExtraFrame
    {
        private readonly IExtraWindowView view;
        private readonly Action<GameObject> bind;
        private IEconomyExtraContent content;

        // bind: UiBinder.BindTree for the content's new parts (null in tests)
        public ExtraWindow(IExtraWindowView view, Economy economy, IEconomyAds ads, IUiFeedback feedback, Func<bool> autoPlay,
            Action<GameObject> bind) : base(economy, ads, feedback, autoPlay)
        {
            this.view = view;
            this.bind = bind;
            view.CloseClicked += OnCloseClicked;
        }

        public IEconomyExtraContent Content => IsOpen ? content : null;

        CancellationToken IEconomyExtraFrame.Token => Token;

        bool IEconomyExtraFrame.Busy
        {
            get => Busy;
            set
            {
                Busy = value;
                view.SetBusy(value);
            }
        }

        void IEconomyExtraFrame.Close() => Close();

        // False while another content is open or when the id is not one of the game's custom extras
        public bool Open(IEconomyExtraContent next)
        {
            if (IsOpen || next == null) return false;
            var extra = Economy.Config?.Extra(next.ExtraId);
            if (extra == null || extra.Kind != ExtraKind.Custom) return false;
            content = next;
            if (Open()) return true;
            content = null;
            return false;
        }

        public override void Dispose()
        {
            base.Dispose();
            view.CloseClicked -= OnCloseClicked;
        }

        protected override bool Available => content != null;

        protected override void OnOpen()
        {
            view.ShowTitle(EconomyKeys.ExtraName(content.ExtraId));
            view.SetBusy(false);
            content.Show(view.Content, this);
            if (view.Content != null) bind?.Invoke(view.Content.gameObject);
        }

        protected override void SetViewOpen(bool open)
        {
            view.SetOpen(open);
            if (open || content == null) return;
            var closed = content;
            content = null;
            closed.Hide();
        }

        protected override void Refresh() => content?.Tick();
    }
}
