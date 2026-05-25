using System;
using System.Collections.Generic;
using System.Text;

namespace MauiMixTube.Models.Eq
{
    public record EqBand(string Type, double Fc, double Gain, double Q);
}
