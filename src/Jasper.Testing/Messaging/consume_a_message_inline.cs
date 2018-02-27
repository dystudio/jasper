﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Jasper.Messaging.Configuration;
using Jasper.Messaging.ErrorHandling;
using Jasper.Messaging.Logging;
using Jasper.Messaging.Runtime;
using Jasper.Messaging.Runtime.Subscriptions;
using Jasper.Testing.Messaging.Runtime;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace Jasper.Testing.Messaging
{
    public class consume_a_message_inline : IntegrationContext, IMessageEventSink, IExceptionSink
    {
        private readonly WorkTracker theTracker = new WorkTracker();


        public consume_a_message_inline()
        {
            with(_ =>
            {
                _.Services.AddSingleton(theTracker);

                _.Publish.MessagesFromAssemblyContaining<Message1>().To("loopback://cascading");

                _.Logging.LogMessageEventsWith(this);
                _.Logging.LogExceptionsWith(this);

                _.Handlers.OnException<DivideByZeroException>().Requeue();
                _.Handlers.DefaultMaximumAttempts = 3;
            });


        }

        [Fact]
        public async Task will_process_inline()
        {
            var message = new Message5
            {

            };

            await Bus.Invoke(message);

            theTracker.LastMessage.ShouldBeSameAs(message);
        }

        [Fact]
        public Task exceptions_will_be_thrown_to_caller()
        {
            var message = new Message5
            {
                FailThisManyTimes = 1
            };


            return Testing.Exception<DivideByZeroException>
                .ShouldBeThrownByAsync(() => Bus.Invoke(message));
        }

        [Fact]
        public async Task will_send_cascading_messages()
        {
            var message = new Message5
            {

            };

            await Bus.Invoke(message);

            var m1 = await theTracker.Message1;
            m1.Id.ShouldBe(message.Id);

            var m2 = await theTracker.Message2;
            m2.Id.ShouldBe(message.Id);
        }

        [Fact]
        public async Task will_log_an_exception()
        {
            try
            {
                await Bus.Invoke(new Message5 {FailThisManyTimes = 1});
            }
            catch (Exception)
            {

            }

            Exceptions.Any().ShouldBeTrue();
        }


        void IMessageEventSink.Sent(Envelope envelope)
        {

        }

        void IMessageEventSink.Received(Envelope envelope)
        {
        }

        void IMessageEventSink.ExecutionStarted(Envelope envelope)
        {
        }

        void IMessageEventSink.ExecutionFinished(Envelope envelope)
        {
        }

        void IMessageEventSink.MessageSucceeded(Envelope envelope)
        {
        }

        public readonly IList<Exception> Exceptions = new List<Exception>();

        void IMessageEventSink.MessageFailed(Envelope envelope, Exception ex)
        {
            Exceptions.Add(ex);
        }

        void IExceptionSink.LogException(Exception ex, Guid correlationId = default(Guid), string message = "Exception detected:")
        {
            Exceptions.Add(ex);
        }

        void IMessageEventSink.NoHandlerFor(Envelope envelope)
        {
        }

        public void NoRoutesFor(Envelope envelope)
        {

        }

        public void SubscriptionMismatch(PublisherSubscriberMismatch mismatch)
        {

        }

        public void Undeliverable(Envelope envelope)
        {

        }

        public void MovedToErrorQueue(Envelope envelope, Exception ex)
        {

        }

        public void DiscardedEnvelope(Envelope envelope)
        {

        }
    }

    public class WorkTracker
    {
        public Message5 LastMessage;

        private readonly TaskCompletionSource<Message1> _message1 = new TaskCompletionSource<Message1>();
        private readonly TaskCompletionSource<Message2> _message2 = new TaskCompletionSource<Message2>();

        public Task<Message1> Message1 => _message1.Task;
        public Task<Message2> Message2 => _message2.Task;

        public void Record(Message2 message)
        {
            _message2.SetResult(message);
        }

        public void Record(Message1 message)
        {
            _message1.SetResult(message);
        }
    }

    public class WorkConsumer
    {
        private readonly WorkTracker _tracker;

        public WorkConsumer(WorkTracker tracker)
        {
            _tracker = tracker;
        }

        public object[] Handle(Envelope envelope, Message5 message)
        {
            if (message.FailThisManyTimes != 0 && message.FailThisManyTimes >= envelope.Attempts)
            {
                throw new DivideByZeroException();
            }

            _tracker.LastMessage = message;

            return new object[] {new Message1 {Id = message.Id}, new Message2 {Id = message.Id}};
        }



        public void Handle(Message2 message)
        {
            _tracker.Record(message);
        }

        public void Handle(Message1 message)
        {
            _tracker.Record(message);
        }
    }
}