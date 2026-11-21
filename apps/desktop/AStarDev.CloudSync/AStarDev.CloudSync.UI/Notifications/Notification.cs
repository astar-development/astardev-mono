namespace AStarDev.CloudSync.UI.Notifications;

/// <summary>A localised message that says what happened, and what is happening or what to do next.</summary>
/// <param name="Category">How prominent it is.</param>
/// <param name="Key">Identifies the repeated event this aggregates, made of kind and subject.</param>
/// <param name="Title">A short headline.</param>
/// <param name="Message">What happened. States the repeat count when <paramref name="Count"/> is above one.</param>
/// <param name="NextStep">What is happening or what the user should do next. Never blank.</param>
/// <param name="Target">Where activating the notification leads.</param>
/// <param name="Count">How many events this stands for. One for a single event.</param>
internal sealed record Notification(NotificationCategory Category, string Key, string Title, string Message, string NextStep, NotificationTarget Target, int Count);
