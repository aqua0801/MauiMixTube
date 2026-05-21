using System;
using System.Collections.Generic;
using System.Text;

namespace MauiMixTube.Extensions
{
    public static class TaskExtensions
    {
        extension(Task? task)
        {
            public Task Safe => task ?? Task.CompletedTask;
        }
    }
}
