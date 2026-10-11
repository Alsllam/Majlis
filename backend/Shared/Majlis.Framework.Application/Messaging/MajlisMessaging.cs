using System.Reflection;
using System.Text.Json;
using Majlis.Framework.Application.Hosting;
using Majlis.Framework.Domain.Events;
using Majlis.Framework.Domain.Repositories;
using Majlis.Framework.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Wolverine;
using Wolverine.EntityFrameworkCore;
using Wolverine.RabbitMQ;
using Wolverine.SqlServer;

namespace Majlis.Framework.Application.Messaging;

/// <summary>Which integration events a host publishes and which it listens to.</summary>
public sealed class MessagingTopology
{
    public List<Type> Publishes { get; } = [];

    public List<Type> Listens { get; } = [];

    public MessagingTopology Publish<TEvent>() where TEvent : class, IEvent
    {
        Publishes.Add(typeof(TEvent));
        return this;
    }

    public MessagingTopology Listen<TEvent>() where TEvent : class, IEvent
    {
        Listens.Add(typeof(TEvent));
        return this;
    }
}

/// <summary>
/// Messaging on Wolverine over RabbitMQ (ADR-0009). Naming is derived from the event type, so every language agrees:
/// alias <c>turn-completed</c>, exchange <c>majlis.turn-completed</c> (durable fanout), queue <c>{module}.turn-completed</c>.
/// Messages are plain camelCase JSON with the alias in the AMQP <c>type</c> property; a non-.NET producer (ai-service)
/// publishes exactly that.
/// </summary>
public static class MajlisMessaging
{
    public const string ExchangePrefix = "majlis.";

    public static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary><c>SessionEventAppended</c> → <c>session-event-appended</c>.</summary>
    public static string AliasOf(Type eventType)
    {
        var name = eventType.Name;
        var chars = new List<char>(name.Length + 8);
        for (var i = 0; i < name.Length; i++)
        {
            if (i > 0 && char.IsUpper(name[i]) && !char.IsUpper(name[i - 1]))
            {
                chars.Add('-');
            }

            chars.Add(char.ToLowerInvariant(name[i]));
        }

        return new string(chars.ToArray());
    }

    public static string ExchangeOf(Type eventType) => ExchangePrefix + AliasOf(eventType);

    public static string QueueOf(string moduleName, Type eventType) => $"{moduleName}.{AliasOf(eventType)}";

    /// <summary>
    /// A module host: Wolverine's durable inbox/outbox stored in the module's own schema, enlisted in the module DbContext.
    /// Registers <see cref="IEventPublisher"/> and <see cref="IUnitOfWork"/> implementations that write through the outbox.
    /// </summary>
    public static IHostApplicationBuilder AddMajlisMessaging<TContext>(
        this IHostApplicationBuilder builder, string moduleName, string schema, MessagingTopology topology, Assembly handlersAssembly)
        where TContext : MajlisDbContext
    {
        var connectionString = builder.Configuration.GetConnectionString("Default")
            ?? throw new InvalidOperationException("Connection string 'Default' is missing.");

        builder.UseWolverine(opts =>
        {
            Configure(opts, builder.Configuration, moduleName, topology, handlersAssembly, durable: true);
            opts.PersistMessagesWithSqlServer(connectionString, schema);
            opts.UseEntityFrameworkCoreTransactions();
        });

        builder.Services.AddScoped<IEventPublisher, OutboxEventPublisher>();
        builder.Services.Replace(ServiceDescriptor.Scoped<IUnitOfWork, OutboxUnitOfWork>());
        return builder;
    }

    /// <summary>A host without a database (Realtime): in-memory buffering, no durable inbox/outbox.</summary>
    public static IHostApplicationBuilder AddMajlisMessaging(
        this IHostApplicationBuilder builder, string moduleName, MessagingTopology topology, Assembly handlersAssembly)
    {
        builder.UseWolverine(opts => Configure(opts, builder.Configuration, moduleName, topology, handlersAssembly, durable: false));
        builder.Services.AddScoped<IEventPublisher, BusEventPublisher>();
        return builder;
    }

    private static void Configure(WolverineOptions opts, IConfiguration configuration, string moduleName, MessagingTopology topology, Assembly handlersAssembly, bool durable)
    {
        var rabbit = configuration.GetSection("RabbitMq").Get<RabbitMqSettings>() ?? new RabbitMqSettings();

        opts.ServiceName = moduleName;
        opts.Discovery.IncludeAssembly(handlersAssembly);
        opts.UseSystemTextJsonForSerialization(stj =>
        {
            stj.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
            stj.PropertyNameCaseInsensitive = true;
        });

        foreach (var type in topology.Publishes.Concat(topology.Listens).Distinct())
        {
            opts.RegisterMessageType(type, AliasOf(type));
        }

        var transport = opts.UseRabbitMq(factory =>
        {
            factory.HostName = rabbit.Host;
            factory.Port = rabbit.Port;
            factory.VirtualHost = rabbit.VirtualHost;
            factory.UserName = rabbit.Username;
            factory.Password = rabbit.Password;
        }).AutoProvision();

        foreach (var type in topology.Listens)
        {
            var queue = QueueOf(moduleName, type);
            transport.BindExchange(ExchangeOf(type)).ToQueue(queue);
            var listener = opts.ListenToRabbitQueue(queue).DefaultIncomingMessage(type);
            if (durable)
            {
                listener.UseDurableInbox();
            }
        }

        foreach (var type in topology.Publishes)
        {
            var publisher = opts.PublishMessage(type).ToRabbitExchange(ExchangeOf(type));
            if (durable)
            {
                publisher.UseDurableOutbox();
            }
        }
    }
}
