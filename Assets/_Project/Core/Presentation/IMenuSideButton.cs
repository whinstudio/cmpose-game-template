namespace Kturker.UnityBase.Core.Presentation
{
    // A round button of the menu's side columns (IMenuSideButtons.AddSideButton)
    public interface IMenuSideButton
    {
        // timer is the countdown's text (e.g. "2:59:41"), read only with MenuBadge.Timer
        void SetBadge(MenuBadge badge, string timer = null);
    }
}
