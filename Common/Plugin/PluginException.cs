namespace TaskbarIconHost
{
    using System;

#pragma warning disable CA1032 // Implement standard exception constructors
#pragma warning disable CA2237 // Mark ISerializable types with SerializableAttribute
    /// <summary>
    /// Represents an exception generated when loading plugins.
    /// </summary>
    public class PluginException : Exception
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="PluginException"/> class.
        /// </summary>
        /// <param name="message">The exception message.</param>
        public PluginException(string message)
            : base(message)
        {
        }
    }
#pragma warning restore CA2237 // Mark ISerializable types with SerializableAttribute
#pragma warning restore CA1032 // Implement standard exception constructors
}
