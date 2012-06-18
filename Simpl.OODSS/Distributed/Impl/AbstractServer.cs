﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using Simpl.OODSS.Distributed.Server.ClientSessionManager;
using Simpl.OODSS.Messages;
using Simpl.Serialization;
using ecologylab.collections;
using Simpl.Fundamental.Net;

namespace Simpl.OODSS.Distributed.Impl
{
    abstract class AbstractServer<S, T>:Manager where S:Scope<T>
    {
        protected SimplTypesScope TranslationScope { get; set; }

        protected S ApplicationObjectScope { get; set; }

        /// <summary>
        /// Creates an instance of an NIOServer of some flavor. Creates the backend using the information
        /// in the arguments.
        /// Registers itself as the MAIN_START_AND_STOPPABLE in the object registry.
        /// </summary>
        /// <param name="portNumber"></param>
        /// <param name="ipAddresses"></param>
        /// <param name="requestTranslationScope"></param>
        /// <param name="objectRegistry"></param>
        /// <param name="idleConnectionTimeout"></param>
        /// <param name="maxMessageLength"></param>
        protected AbstractServer(int portNumber, IPAddress[] ipAddresses, 
            SimplTypesScope requestTranslationScope, S objectRegistry, int idleConnectionTimeout,
            int maxMessageLength) 
        {
            Console.WriteLine("setting up server...");
            TranslationScope = requestTranslationScope;
            ApplicationObjectScope = objectRegistry;
        }

        static readonly Type[] _ourTranslation = {typeof (InitConnectionRequest)};

        public static SimplTypesScope ComposeTranslations(int portNumber, IPAddress ipAddress, 
            SimplTypesScope requestTranslationSpace)
        {
            return ComposeTranslations(_ourTranslation, portNumber, ipAddress, requestTranslationSpace);
        }

        public static SimplTypesScope ComposeTranslations(Type[] newTranslations,
            int portNumber, IPAddress ipAddress, SimplTypesScope requestTranslationSpace, 
            String prefix = "server_base: ")
        {
            return SimplTypesScope.Get(prefix + ipAddress.ToString() + ":" + portNumber, requestTranslationSpace,
                                       newTranslations);
        }

        protected AbstractServer(int portNumber, IPAddress ipAddress, SimplTypesScope requestTranslationSpace, 
                S objectRegistry, int idleConnectionTimeout, int maxMessageLength)
            :this(portNumber, NetTools.WrapSingleAddress(ipAddress), requestTranslationSpace, 
                objectRegistry, idleConnectionTimeout, maxMessageLength)
        {
        }

        protected abstract WebSocketClientSessionManager generateContextManager(string seesionId, )
    }
}
