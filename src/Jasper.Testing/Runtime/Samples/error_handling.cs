﻿using System;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Security;
using Baseline;
using Baseline.Dates;
using Jasper.Attributes;
using Jasper.Configuration;
using Jasper.ErrorHandling;
using Jasper.Runtime;
using Jasper.Runtime.Handlers;
using Lamar;
using LamarCodeGeneration;
using Polly;

namespace Jasper.Testing.Runtime.Samples
{
    public class error_handling
    {
    }

    // SAMPLE: ErrorHandlingPolicy
    public class ErrorHandlingPolicy : IHandlerPolicy
    {
        public void Apply(HandlerGraph graph, GenerationRules rules, IContainer container)
        {
            var matchingChains = graph.Chains.Where(x => x.MessageType.IsInNamespace("MyApp.Messages"));

            foreach (var chain in matchingChains)
            {
                chain.Retries.MaximumAttempts = 2;
                chain.Retries.Add(x => x.Handle<SqlException>()
                    .Requeue());
            }
        }
    }
    // ENDSAMPLE

    // SAMPLE: MyApp-with-error-handling
    public class MyApp : JasperOptions
    {
        public MyApp()
        {
            Handlers.GlobalPolicy<ErrorHandlingPolicy>();
        }
    }
    // ENDSAMPLE

    // SAMPLE: GlobalErrorHandlingConfiguration
    public class GlobalRetryApp : JasperOptions
    {
        public GlobalRetryApp()
        {
            Handlers.Retries.Add(x => x.Handle<TimeoutException>().Reschedule(5.Seconds()));

            Handlers.Retries.Add(x => x.Handle<SecurityException>().MoveToErrorQueue());

            // You can also apply an additional filter on the
            // exception type for finer grained policies
            Handlers.Retries.Add(x => x.Handle<SocketException>(ex => ex.Message.Contains("not responding"))
                .Reschedule(5.Seconds()));
        }
    }
    // ENDSAMPLE


    // SAMPLE: configure-error-handling-per-chain-with-configure
    public class MyErrorCausingHandler
    {
        public static void Configure(HandlerChain chain)
        {
            chain.Retries.Add(x => x.Handle<IOException>()
                .Requeue());

            chain.Retries.MaximumAttempts = 3;
        }


        public void Handle(InvoiceCreated created)
        {
            // handle the invoice created message
        }

        public void Handle(InvoiceApproved approved)
        {
            // handle the invoice approved message
        }
    }
    // ENDSAMPLE

    public class InvoiceCreated
    {
        public DateTime Time { get; set; }
        public string Purchaser { get; set; }
        public double Amount { get; set; }
    }

    public class InvoiceApproved
    {
    }

    // SAMPLE: configuring-error-handling-with-attributes
    public class AttributeUsingHandler
    {
        [RescheduleLater(typeof(IOException), 5)]
        [RetryOn(typeof(SqlException))]
        [RequeueOn(typeof(InvalidOperationException))]
        [MoveToErrorQueueOn(typeof(DivideByZeroException))]
        [MaximumAttempts(2)]
        public void Handle(InvoiceCreated created)
        {
            // handle the invoice created message
        }
    }
    // ENDSAMPLE

    public class SqlException : Exception
    {
    }

    // SAMPLE: filtering-by-exception-type
    public class FilteredApp : JasperOptions
    {
        public FilteredApp()
        {
            Handlers.Retries.Add(x => x.Handle<SqlException>().Requeue());

            Handlers.Retries.Add(x => x.Handle<InvalidOperationException>().RetryAsync());
        }
    }
    // ENDSAMPLE

    // SAMPLE: continuation-actions
    public class ContinuationTypes : JasperOptions
    {
        public ContinuationTypes()
        {
            var policy = Policy<IContinuation>.Handle<SqlException>()
                .Retry(3);

            // Try to execute the message again without going
            // back through the queue
            Handlers.Retries.Add(x => x.Handle<SqlException>().RetryAsync());

            // Retry the message again, but wait for the specified time
            Handlers.Retries.Add(x => x.Handle<SqlException>().Reschedule(3.Seconds()));

            // Put the message back into the queue where it will be
            // attempted again
            Handlers.Retries.Add(x => x.Handle<SqlException>().Requeue());

            // Move the message into the error queue for this transport
            Handlers.Retries.Add(x => x.Handle<SqlException>().MoveToErrorQueue());
        }
    }
    // ENDSAMPLE



    public class FailedOnSecurity
    {
        public FailedOnSecurity(string message)
        {
        }
    }





}
