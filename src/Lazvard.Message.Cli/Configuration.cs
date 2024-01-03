using Iso8601DurationHelper;
using Lazvard.Message.Amqp.Server;
using Lazvard.Message.Amqp.Server.Helpers;
using Tommy;

namespace Lazvard.Message.Cli;

public class CliConfig : BrokerConfig
{
    public string CertificatePath { get; set; } = string.Empty;
    public string CertificatePassword { get; set; } = string.Empty;
}

internal static class ConfigurationSections
{
    public const string Server = nameof(Server);
    public const string AMQP = nameof(AMQP);
    public const string Queues = nameof(Queues);
}

public sealed class Configuration
{
    private const string path = "config.toml";

    public static async Task WriteAsync(CliConfig config)
    {
        using StreamWriter writer = File.CreateText(path);

        var toml = new TomlTable()
        {
            [ConfigurationSections.Server] =
            {

                [nameof(BrokerConfig.IP)] = new TomlString
                {
                    Value = config.IP,
                    Comment = "IP to listen on"
                },
                [nameof(BrokerConfig.Port)] = new TomlInteger
                {
                    Value = config.Port,
                    Comment = "Port to listen on"
                },
                [nameof(CliConfig.CertificatePath)] = new TomlString
                {
                    Value = config.CertificatePath,
                    Comment = "The path to trusted X.509 certificate (PFX - PKCS #12)"
                },
                [nameof(CliConfig.CertificatePassword)] = new TomlString
                {
                    Value = config.CertificatePassword,
                    Comment = "The certificate password, optional"
                },
            },
            [ConfigurationSections.AMQP] =
            {
                [nameof(BrokerConfig.ConnectionIdleTimeOut)] =  new TomlInteger
                {
                    Value = config.ConnectionIdleTimeOut,
                    Comment = "Connection idle timeout in milliseconds for AMQP connection, default is 4 minutes"
                },
                [nameof(BrokerConfig.MaxFrameSize)] =  new TomlInteger
                {
                    Value = config.MaxFrameSize,
                    Comment = "Max AMQP frame size in byte, default is 64Kib"
                },
                [nameof(BrokerConfig.MaxMessageSize)] =  new TomlInteger
                {
                    Value = config.MaxMessageSize,
                    Comment = "Max AMQP message size in byte, default is 64MiB"
                },
            },
        };

        toml.WriteTo(writer);
        writer.WriteLine();

        var queues = new TomlArray
        {
            IsTableArray = true,
        };
        var topics = new TomlArray
        {
            IsTableArray = true,
        };
        toml = new TomlTable
        {
            [nameof(ConfigurationSections.Queues)] = queues,
            [nameof(BrokerConfig.Topics)] = topics,
        };

        foreach (var topic in config.Topics)
        {

            var isQueue = topic.Subscriptions.Count() == 1 && string.IsNullOrEmpty(topic.Subscriptions.First().Name);

            if (isQueue)
            {
                var queue = BuildSubscription(topic.Subscriptions.First());
                queue[nameof(TopicConfig.Name)] = new TomlString
                {
                    Value = topic.Name,
                    Comment = "Queue name",
                };

                queues.Add(queue);
            }
            else
            {
                var subscriptionsArray = new TomlArray
                {
                    IsTableArray = true,
                };
                subscriptionsArray.AddRange(topic.Subscriptions.Select(BuildSubscription));

                topics.Add(new TomlTable
                {
                    [nameof(TopicConfig.Name)] = new TomlString
                    {
                        Value = topic.Name,
                        Comment = "Topic name",
                    },
                    [nameof(TopicConfig.Subscriptions)] = subscriptionsArray,
                });
            }
        };

        toml.WriteTo(writer);
        await writer.FlushAsync();
    }

    private static TomlTable BuildSubscription(TopicSubscriptionConfig config)
    {
        return new TomlTable
        {
            [nameof(TopicSubscriptionConfig.Name)] = new TomlString
            {
                Value = config.Name,
                Comment = "Subscription name"
            },
            [nameof(TopicSubscriptionConfig.MaxDeliveryCount)] = new TomlInteger
            {
                Value = config.MaxDeliveryCount,
                Comment = "Number of maximum deliveries. The default value is 50 times"
            },
            [nameof(TopicSubscriptionConfig.LockDuration)] = new TomlString
            {
                Value = config.LockDuration.ToString(),
                Comment = "ISO 8061 lock duration for the subscription. The default value is 1 minute"
            },
        };
    }

    public static async Task CreateDefaultConfigAsync(string certificatePath, string certificatePassword)
    {
        var config = new CliConfig
        {
            CertificatePath = certificatePath,
            CertificatePassword = certificatePassword,
            Topics = new TopicConfig[]
            {
                new TopicConfig("topic-1", new[]
                {
                    new TopicSubscriptionConfig("topic-1-subscription-a")
                }),
                new TopicConfig("topic-2", new[]
                {
                    new TopicSubscriptionConfig("topic-2-subscription-a"),
                    new TopicSubscriptionConfig("topic-2-subscription-b")
                }),
                new TopicConfig("queue-1", new[]
                {
                    new TopicSubscriptionConfig("")
                }),
            },
        };

        await WriteAsync(config);
    }

    public static bool Exists()
    {
        return File.Exists(path);
    }

    public static Result<CliConfig> Read()
    {
        if (!Exists())
        {
            return Result.Fail("can't find the file.");
        }

        using var configFile = File.OpenText(path);
        var config = TOML.Parse(configFile);
        if (config is null)
        {
            return Result.Fail("file is not a valid Toml.");
        }

        try
        {
            var result = new CliConfig();

            result.IP = config[ConfigurationSections.Server][nameof(BrokerConfig.IP)]?.AsString ?? result.IP;
            result.Port = config[ConfigurationSections.Server][nameof(BrokerConfig.Port)]?.AsInteger ?? result.Port;
            result.CertificatePath = config[ConfigurationSections.Server][nameof(CliConfig.CertificatePath)].AsString;
            result.CertificatePassword = config[ConfigurationSections.Server][nameof(CliConfig.CertificatePassword)].AsString;

            result.ConnectionIdleTimeOut = (uint?)config[ConfigurationSections.AMQP][nameof(BrokerConfig.ConnectionIdleTimeOut)]?.AsInteger
                ?? result.ConnectionIdleTimeOut;

            result.MaxFrameSize = (uint?)config[ConfigurationSections.AMQP][nameof(BrokerConfig.MaxFrameSize)]?.AsInteger
                ?? result.MaxFrameSize;

            result.MaxMessageSize = (uint?)config[ConfigurationSections.AMQP][nameof(BrokerConfig.MaxMessageSize)]?.AsInteger
                ?? result.MaxMessageSize;

            var defaultTopicConf = new TopicSubscriptionConfig("");
            var queues = config[nameof(ConfigurationSections.Queues)]
                .AsArray?
                .Children
                .Select(q => new TopicConfig(q[nameof(TopicConfig.Name)].AsString, new[]
                {
                    new TopicSubscriptionConfig(string.Empty)
                    {
                        LockDuration = Duration.Parse(q[nameof(TopicSubscriptionConfig.LockDuration)]?.AsString
                            ?? defaultTopicConf.LockDuration.ToString()),
                        MaxDeliveryCount = q[nameof(TopicSubscriptionConfig.MaxDeliveryCount)]?.AsInteger
                            ?? defaultTopicConf.MaxDeliveryCount,
                    }
                })
                ).ToArray() ?? Array.Empty<TopicConfig>();

            var topics = config[nameof(BrokerConfig.Topics)]
                .AsArray?
                .Children
                .Select(t =>
                {
                    var subscriptions = t[nameof(TopicConfig.Subscriptions)]
                        .AsArray
                        .Children
                        .Select(s => new TopicSubscriptionConfig(s[nameof(TopicSubscriptionConfig.Name)].AsString)
                        {
                            LockDuration = Duration.Parse(s[nameof(TopicSubscriptionConfig.LockDuration)]?.AsString
                                ?? defaultTopicConf.LockDuration.ToString()),
                            MaxDeliveryCount = s[nameof(TopicSubscriptionConfig.MaxDeliveryCount)]?.AsInteger
                                ?? defaultTopicConf.MaxDeliveryCount,
                        });

                    return new TopicConfig(t[nameof(TopicConfig.Name)].AsString, subscriptions);
                }).ToArray() ?? Array.Empty<TopicConfig>();

            result.Topics = topics.Concat(queues).ToArray();

            return result;
        }
        catch (Exception e)
        {
            return Result.Fail(e.Message);
        }
    }
}
