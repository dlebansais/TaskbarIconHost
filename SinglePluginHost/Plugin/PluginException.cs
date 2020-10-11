namespace TaskbarIconHost
{
    using System;

#pragma warning disable CA1032 // Implement standard exception constructors
#pragma warning disable CA2237 // Mark ISerializable types with serializable
    /// <summary>
    /// Represents an exception generated when loading plugins.
    /// </summary>
    public class PluginException : Exception
#pragma warning restore CA2237 // Mark ISerializable types with serializable
#pragma warning restore CA1032 // Implement standard exception constructors
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
}
