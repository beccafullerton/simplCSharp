﻿using System;

namespace Simpl.Serialization.Context
{
    public interface IDeserializationHookStrategy
    {
        void DeserializationPreHook(Object o, FieldDescriptor fd);

        void DeserializationPostHook(Object o, FieldDescriptor fd);
    }
}
