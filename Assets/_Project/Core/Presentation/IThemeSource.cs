using System;

namespace Kturker.UnityBase.Core.Presentation
{
    // The palette in use and the family's style (fonts, shapes, motion; it does not change while the app runs);
    // Changed fires after the palette changes
    public interface IThemeSource
    {
        Palette Current { get; }
        ThemeStyle Style { get; }
        event Action Changed;
    }
}
