using System;
using System.Collections.Generic;
using System.Text;

namespace MauiMixTube.Extensions
{
    public static class ViewExtensions
    {
        public static void SetThemeColorToggle(this BindableObject view, BindableProperty prop,
                                   bool active, string activeLight, string activeDark,
                                   string inactiveLight, string inactiveDark)
        {
            var res = Application.Current!.Resources;

            if (active)
                view.SetAppThemeColor(prop,
                    (Color)res[activeLight], (Color)res[activeDark]);
            else
                view.SetAppThemeColor(prop,
                    (Color)res[inactiveLight], (Color)res[inactiveDark]);
        }
    }
}
