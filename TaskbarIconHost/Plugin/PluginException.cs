﻿using System;

namespace TaskbarIconHost
{
#pragma warning disable CA1032 // Implement standard exception constructors
#pragma warning disable CA2237 // Mark ISerializable types with SerializableAttribute
    public class PluginException : Exception
    {
        public PluginException(string message)
            : base(message)
        {
        }
    }
#pragma warning restore CA2237 // Mark ISerializable types with SerializableAttribute
#pragma warning restore CA1032 // Implement standard exception constructors
}
