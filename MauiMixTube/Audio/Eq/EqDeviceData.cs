using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;

namespace MauiMixTube.Audio.Eq
{
    public record EqDeviceData(
        string Source,
        [property: JsonPropertyName("peq")] EqPreset Peq
    );
}
