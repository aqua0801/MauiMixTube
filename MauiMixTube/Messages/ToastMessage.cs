using CommunityToolkit.Mvvm.Messaging.Messages;
using System;
using System.Collections.Generic;
using System.Text;

namespace MauiMixTube.Messages
{
    public class ToastMessage : ValueChangedMessage<string>
    {
        public ToastMessage(string value) : base(value)
        {
        }
    }
}
