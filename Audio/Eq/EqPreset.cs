using System;
using System.Collections.Generic;
using System.Text;

namespace MauiMixTube.Audio.Eq
{
    public record EqPreset(double Preamp, IReadOnlyList<EqBand> Filters);
}
