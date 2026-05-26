using System;
using System.Collections.Generic;
using System.Text;

namespace MauiMixTube.Audio.Eq
{
    public record EqBand(string Type, double Fc, double Gain, double Q);
}
