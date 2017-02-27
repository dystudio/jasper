﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading.Tasks;
using Baseline;
using Jasper.Codegen;
using Jasper.Internal;
using JasperBus.Runtime.Invocation;
using StructureMap;

namespace JasperBus.Model
{


    public abstract class MessageHandler : IHandler<IInvocationContext>
    {
        public HandlerChain Chain { get; set; }

        public abstract Task Handle(IInvocationContext input);
    }


    public class HandlerChain : IGenerates<MessageHandler>
    {
        public static HandlerChain For<T>(Expression<Action<T>> expression)
        {
            throw new NotImplementedException();
        }

        public Type MessageType { get; }

        public HandlerChain(Type messageType)
        {
            if (messageType == null) throw new ArgumentNullException(nameof(messageType));

            MessageType = messageType;

            TypeName = messageType.FullName.Replace(".", "_");
        }

        public string TypeName { get; }

        public List<MethodCall> Handlers = new List<MethodCall>();

        HandlerCode IGenerates<MessageHandler>.ToHandlerCode()
        {
            if (!Handlers.Any())
            {
                throw new InvalidOperationException("No method handlers configured for message type " + MessageType.FullName);
            }

            var chain = new HandlerCode(TypeName, typeof(MessageHandler));

            foreach (var method in Handlers)
            {
                chain.AddToEnd(method);
            }

            return chain;
        }

        private string _code;

        string IGenerates<MessageHandler>.SourceCode
        {
            get { return _code; }
            set { _code = value; }
        }

        MessageHandler IGenerates<MessageHandler>.Create(Assembly assembly, IContainer container)
        {
            var type = assembly.GetExportedTypes().FirstOrDefault(x => x.Name == TypeName);
            if (type == null)
            {
                throw new ArgumentOutOfRangeException(nameof(assembly), $"Could not find a type named '{TypeName}' in this assembly");
            }

            var handler = container.GetInstance(type).As<MessageHandler>();

            handler.Chain = this;

            return handler;
        }
    }
}