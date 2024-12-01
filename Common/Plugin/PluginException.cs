#pragma warning disable CA1515 // Consider making public types internal

namespace TaskbarIconHost;

using System;

#pragma warning disable CA1032 // Implement standard exception constructors
#pragma warning disable CA2237 // Mark ISerializable types with SerializableAttribute
/// <summary>
/// Represents an exception generated when loading plugins.
/// </summary>
/// <param name="message">The exception message.</param>
public class PluginException(string message) : Exception(message)
{
}
#pragma warning restore CA2237 // Mark ISerializable types with SerializableAttribute
#pragma warning restore CA1032 // Implement standard exception constructors
