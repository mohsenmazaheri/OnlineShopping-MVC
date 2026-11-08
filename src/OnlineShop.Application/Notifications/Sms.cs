namespace OnlineShop.Application.Notifications;

/// <summary>Whether an SMS provider is configured. SMS is optional; phase 2b adds the providers.</summary>
public interface ISmsCapability
{
    bool IsEnabled { get; }
}

/// <summary>Default: no SMS provider configured, so every SMS-based feature stays off.</summary>
public sealed class NoSmsCapability : ISmsCapability
{
    public bool IsEnabled => false;
}
