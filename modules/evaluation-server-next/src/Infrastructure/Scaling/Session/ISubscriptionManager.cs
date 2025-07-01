namespace Infrastructure.Scaling.Session;

/// <summary>
/// Interface for managing WebSocket subscriptions (abstraction to avoid circular dependencies)
/// </summary>
public interface ISubscriptionManager
{
    /// <summary>
    /// Add a channel subscription to a connection
    /// </summary>
    /// <param name="connectionId">Connection identifier</param>
    /// <param name="channel">Channel to subscribe to</param>
    void AddChannelToSubscription(string connectionId, string channel);
    
    /// <summary>
    /// Remove a channel subscription from a connection
    /// </summary>
    /// <param name="connectionId">Connection identifier</param>
    /// <param name="channel">Channel to unsubscribe from</param>
    void RemoveChannelFromSubscription(string connectionId, string channel);
    
    /// <summary>
    /// Remove all subscriptions for a connection
    /// </summary>
    /// <param name="connectionId">Connection identifier</param>
    void RemoveSubscription(string connectionId);
    
    /// <summary>
    /// Send a message to a specific subscriber
    /// </summary>
    /// <param name="connectionId">Connection identifier</param>
    /// <param name="message">Message to send</param>
    /// <returns>Task</returns>
    Task BroadcastToSubscriberAsync(string connectionId, string message);
} 