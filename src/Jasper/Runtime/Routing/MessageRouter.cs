﻿using System;
using System.Collections.Generic;
using System.Linq;
using Baseline;
using Baseline.Reflection;
using Jasper.Attributes;
using Jasper.Runtime.Handlers;
using Jasper.Serialization;
using Jasper.Transports;
using Jasper.Transports.Sending;
using Jasper.Util;

namespace Jasper.Runtime.Routing
{
    public class MessageRouter : IMessageRouter
    {
        private readonly HandlerGraph _handlers;
        private readonly MessagingSerializationGraph _serializers;
        private readonly AdvancedSettings _settings;
        private readonly ITransportRuntime _runtime;

        private ImHashMap<Type, MessageRoute[]> _routes = ImHashMap<Type, MessageRoute[]>.Empty;

        private ImHashMap<Type, Action<Envelope>[]> _messageRules = ImHashMap<Type, Action<Envelope>[]>.Empty;

        public MessageRouter(HandlerGraph handlers, MessagingSerializationGraph serializers, AdvancedSettings settings, ITransportRuntime runtime)
        {
            _handlers = handlers;
            _serializers = serializers;
            _settings = settings;
            _runtime = runtime;
        }

        public void ClearAll()
        {
            _routes = ImHashMap<Type, MessageRoute[]>.Empty;
        }

        public MessageRoute[] Route(Type messageType)
        {
            if (_routes.TryFind(messageType, out var routes)) return routes;

            routes = compileRoutes(messageType).ToArray();
            _routes = _routes.AddOrUpdate(messageType, routes);

            return routes;
        }

        private ImHashMap<Type, ISendingAgent> _localQueueByType = ImHashMap<Type, ISendingAgent>.Empty;

        public ISendingAgent LocalQueueByMessageType(Type messageType)
        {
            if (_localQueueByType.TryFind(messageType, out var agent))
            {
                return agent;
            }

            var route = CreateLocalRoute(messageType);
            _localQueueByType = _localQueueByType.AddOrUpdate(messageType, route.Sender);

            return route.Sender;
        }

        public MessageRoute RouteForDestination(Envelope envelope)
        {
            var channel = _runtime.GetOrBuildSendingAgent(envelope.Destination);


            if (envelope.Message != null)
            {
                var messageType = envelope.Message.GetType();
                var routes = Route(messageType);

                var candidate = routes.FirstOrDefault(x => x.MatchesEnvelope(envelope));
                if (candidate != null) return candidate;


                var modelWriter = _serializers.WriterFor(messageType);
                var contentType = envelope.ContentType ??
                                  envelope.AcceptedContentTypes.Intersect(modelWriter.ContentTypes).FirstOrDefault()
                                  ?? "application/json";

                return new MessageRoute(
                    messageType,
                    modelWriter,
                    channel,
                    contentType);
            }
            else if (envelope.Data != null)
            {
                return new MessageRoute(envelope, channel);
            }
            else
            {
                throw new ArgumentOutOfRangeException(nameof(envelope), "Missing message");
            }




        }

        public Envelope[] Route(Envelope envelope)
        {
            var envelopes = route(envelope);
            foreach (var outgoing in envelopes) outgoing.Source = _settings.ServiceName;

            return envelopes;
        }

        private Envelope[] route(Envelope envelope)
        {
            if (envelope.Destination == null)
            {
                var routes = compileRoutes(envelope.Message.GetType());

                var outgoing = routes.Select(x => x.CloneForSending(envelope)).ToArray();

                // A hack.
                if (outgoing.Length == 1) outgoing[0].Id = envelope.Id;

                return outgoing;
            }

            var route = RouteForDestination(envelope);


            var toBeSent = route.CloneForSending(envelope);
            toBeSent.Id = envelope.Id;

            return new[] {toBeSent};
        }

        private List<MessageRoute> compileRoutes(Type messageType)
        {
            var list = new List<MessageRoute>();

            var modelWriter = _serializers.WriterFor(messageType);
            var supported = modelWriter.ContentTypes;


            applyStaticPublishingRules(messageType, supported, list, modelWriter);

            if (!list.Any())
                if (_handlers.CanHandle(messageType))
                    list.Add(CreateLocalRoute(messageType));

            return list;
        }

        private void applyStaticPublishingRules(Type messageType, string[] supported, List<MessageRoute> list,
            WriterCollection<IMessageSerializer> writerCollection)
        {
            foreach (var agent in _runtime.FindSubscribers(messageType))
            {
                var contentType = supported.FirstOrDefault(x => x != "application/json") ?? "application/json";

                if (contentType.IsNotEmpty())
                    list.Add(new MessageRoute(messageType, writerCollection, agent, contentType));
            }
        }

        public MessageRoute CreateLocalRoute(Type messageType)
        {
            if (messageType.HasAttribute<LocalQueueAttribute>())
            {
                var queueName = messageType.GetAttribute<LocalQueueAttribute>().QueueName;
                var agent = _runtime.AgentForLocalQueue(queueName);

                return new MessageRoute(messageType, agent.Destination, "application/json")
                {
                    Sender = agent
                };
            }

            var subscribers = _runtime.FindLocalSubscribers(messageType);
            var sender = subscribers.FirstOrDefault() ?? _runtime.GetOrBuildSendingAgent(TransportConstants.LocalUri);

            return new MessageRoute(messageType, sender.Destination, "application/json")
            {
                Sender = _runtime.GetOrBuildSendingAgent(sender.Destination)
            };
        }

        public void ApplyMessageTypeSpecificRules(Envelope envelope)
        {
            if (envelope.Message == null) return;

            var messageType = envelope.Message.GetType();
            if (!_messageRules.TryFind(messageType, out var rules))
            {
                rules = findMessageTypeCustomizations(messageType).ToArray();
                _messageRules = _messageRules.AddOrUpdate(messageType, rules);
            }

            foreach (var action in rules) action(envelope);
        }

        private IEnumerable<Action<Envelope>> findMessageTypeCustomizations(Type messageType)
        {
            foreach (var att in messageType.GetAllAttributes<ModifyEnvelopeAttribute>())
                yield return e => att.Modify(e);
        }

    }
}