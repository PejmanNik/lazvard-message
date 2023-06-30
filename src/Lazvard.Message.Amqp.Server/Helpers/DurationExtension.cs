using Iso8601DurationHelper;

namespace Lazvard.Message.Amqp.Server.Helpers;

public static class DurationExtension
{
    public static TimeSpan ToTimeSpan(this Duration duration)
    {
        var timeSpan = TimeSpan.FromSeconds(duration.Seconds);
        timeSpan += TimeSpan.FromMinutes(duration.Minutes);
        timeSpan += TimeSpan.FromHours(duration.Hours);
        timeSpan += TimeSpan.FromDays(duration.Days);
        timeSpan += TimeSpan.FromDays(duration.Weeks * 7);
        timeSpan += TimeSpan.FromDays(duration.Months * 30);
        timeSpan += TimeSpan.FromDays(duration.Years * 365);

        return timeSpan;
    }
}
