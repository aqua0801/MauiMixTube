using System;
using System.Collections.Generic;
using System.Text;

namespace MauiMixTube.Audio.Eq
{
    public sealed class BiquadFilter
    {
        private readonly double _b0, _b1, _b2, _a1, _a2;

        private double _x1L, _x2L, _y1L, _y2L;
        private double _x1R, _x2R, _y1R, _y2R;

        private BiquadFilter(double b0, double b1, double b2, double a1, double a2)
        {
            _b0 = b0;
            _b1 = b1;
            _b2 = b2;
            _a1 = a1;
            _a2 = a2;
        }

        public static BiquadFilter PeakingEQ(double freq, double gain, double q, double sampleRate)
        {
            double A = Math.Pow(10, gain / 40.0);
            double w0 = 2 * Math.PI * freq / sampleRate;
            double alpha = Math.Sin(w0) / (2 * q);

            double b0 = 1 + alpha * A;
            double b1 = -2 * Math.Cos(w0);
            double b2 = 1 - alpha * A;
            double a0 = 1 + alpha / A;
            double a1 = -2 * Math.Cos(w0);
            double a2 = 1 - alpha / A;

            return new BiquadFilter(b0 / a0, b1 / a0, b2 / a0, a1 / a0, a2 / a0);
        }

        public static BiquadFilter FromEqBand(EqBand band, double sampleRate) => band.Type switch
        {
            "PK" => PeakingEQ(band.Fc, band.Gain, band.Q, sampleRate),
            "LSC" => LowShelf(band.Fc, band.Gain, band.Q, sampleRate),
            "HSC" => HighShelf(band.Fc, band.Gain, band.Q, sampleRate),
            _ => PeakingEQ(band.Fc, band.Gain, band.Q, sampleRate) 
        };

        public static BiquadFilter LowShelf(double freq, double gain, double q, double sampleRate)
        {
            double A = Math.Pow(10, gain / 40.0);
            double w0 = 2 * Math.PI * freq / sampleRate;
            double alpha = Math.Sin(w0) / (2 * q);

            double b0 = A * ((A + 1) - (A - 1) * Math.Cos(w0) + 2 * Math.Sqrt(A) * alpha);
            double b1 = 2 * A * ((A - 1) - (A + 1) * Math.Cos(w0));
            double b2 = A * ((A + 1) - (A - 1) * Math.Cos(w0) - 2 * Math.Sqrt(A) * alpha);
            double a0 = (A + 1) + (A - 1) * Math.Cos(w0) + 2 * Math.Sqrt(A) * alpha;
            double a1 = -2 * ((A - 1) + (A + 1) * Math.Cos(w0));
            double a2 = (A + 1) + (A - 1) * Math.Cos(w0) - 2 * Math.Sqrt(A) * alpha;

            return new BiquadFilter(b0 / a0, b1 / a0, b2 / a0, a1 / a0, a2 / a0);
        }

        public static BiquadFilter HighShelf(double freq, double gain, double q, double sampleRate)
        {
            double A = Math.Pow(10, gain / 40.0);
            double w0 = 2 * Math.PI * freq / sampleRate;
            double alpha = Math.Sin(w0) / (2 * q);

            double b0 = A * ((A + 1) + (A - 1) * Math.Cos(w0) + 2 * Math.Sqrt(A) * alpha);
            double b1 = -2 * A * ((A - 1) + (A + 1) * Math.Cos(w0));
            double b2 = A * ((A + 1) + (A - 1) * Math.Cos(w0) - 2 * Math.Sqrt(A) * alpha);
            double a0 = (A + 1) - (A - 1) * Math.Cos(w0) + 2 * Math.Sqrt(A) * alpha;
            double a1 = 2 * ((A - 1) - (A + 1) * Math.Cos(w0));
            double a2 = (A + 1) - (A - 1) * Math.Cos(w0) - 2 * Math.Sqrt(A) * alpha;

            return new BiquadFilter(b0 / a0, b1 / a0, b2 / a0, a1 / a0, a2 / a0);
        }

        public void ProcessStereo(Span<short> pcm)
        {
            for (int i = 0; i < pcm.Length - 1; i += 2)
            {
                pcm[i] = ProcessSample(pcm[i], ref _x1L, ref _x2L, ref _y1L, ref _y2L);
                pcm[i + 1] = ProcessSample(pcm[i + 1], ref _x1R, ref _x2R, ref _y1R, ref _y2R);
            }
        }


        private short ProcessSample(short input,
            ref double x1, ref double x2, ref double y1, ref double y2)
        {
            double x0 = input;
            double y0 = _b0 * x0 + _b1 * x1 + _b2 * x2 - _a1 * y1 - _a2 * y2;

            x2 = x1; x1 = x0;
            y2 = y1; y1 = y0;

            return (short)Math.Clamp(y0, short.MinValue, short.MaxValue);
        }

    }
}
