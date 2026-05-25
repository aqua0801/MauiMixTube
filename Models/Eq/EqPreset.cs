using System;
using System.Collections.Generic;
using System.Text;

namespace MauiMixTube.Models.Eq
{
    public record EqPreset(double Preamp, IReadOnlyList<EqBand> Filters);
}
