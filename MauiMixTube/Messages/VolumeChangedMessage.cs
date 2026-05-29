using CommunityToolkit.Mvvm.Messaging.Messages;


namespace MauiMixTube.Messages
{
    public class VolumeChangedMessage : ValueChangedMessage<double>
    {
        public VolumeChangedMessage(double value) : base(value)
        {
        }
    }
}
