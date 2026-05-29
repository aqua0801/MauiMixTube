using CommunityToolkit.Mvvm.Messaging.Messages;
using MauiMixTube.Models.Settings;

namespace MauiMixTube.Messages
{
    public class QualityChangedMessage : ValueChangedMessage<FetchQuality>
    {
        public QualityChangedMessage(FetchQuality value) : base(value)
        {
        }
    }
}
