using System;
using System.Collections.Generic;
using System.Text;

namespace MauiMixTube.Audio.Eq
{
    public sealed class AutoEqProcessor
    {
        private readonly BiquadFilter[] _filters;
        private readonly float _preampGain;

        public AutoEqProcessor(EqPreset preset, int sampleRate)
        {
            _preampGain = (float)Math.Pow(10, preset.Preamp / 20.0);

            _filters = preset.Filters
                .Select(b => BiquadFilter.FromEqBand(b, sampleRate))
                .ToArray();
        }

        public void Process(Span<short> pcm)
        {
            foreach (var filter in _filters)
                filter.ProcessStereo(pcm);

            if (Math.Abs(_preampGain - 1.0f) > 0.001f)
            {
                for (int i = 0; i < pcm.Length; i++)
                    pcm[i] = (short)Math.Clamp(pcm[i] * _preampGain,
                        short.MinValue, short.MaxValue);
            }
        }
    }
}
