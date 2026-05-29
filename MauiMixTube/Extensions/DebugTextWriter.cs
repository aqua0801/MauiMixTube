using System;
using System.Collections.Generic;
using System.Text;

namespace MauiMixTube.Extensions
{
    public class DebugTextWriter : System.IO.TextWriter
    {
        public override System.Text.Encoding Encoding => System.Text.Encoding.UTF8;

        public override void WriteLine(string? value) => System.Diagnostics.Trace.WriteLine(value);
        public override void Write(string? value) => System.Diagnostics.Trace.Write(value);
    }
}
