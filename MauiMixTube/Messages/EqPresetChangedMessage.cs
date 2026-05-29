using MauiMixTube.Audio.Eq;
using System;
using System.Collections.Generic;
using System.Text;

namespace MauiMixTube.Messages
{
    public record EqPresetChangedMessage(EqPreset? Preset);
}
