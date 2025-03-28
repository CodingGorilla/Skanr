using System;

namespace Skanr.Attributes
{
    public enum RegistrationMode
    {
        /// <summary>
        /// Automatically determine the registration mode.
        /// </summary>
        /// <remarks>
        /// The default registration mode is to register the first interface implemented by the class if interfaces are implemented,
        /// otherwise, register the class itself.
        /// </remarks>
        Auto,

        /// <summary>
        /// Register the first interface implemented by the class.
        /// </summary>
        FirstInterface,

        /// <summary>
        /// Register all interfaces implemented by the class.
        /// </summary>
        AllInterfaces,

        /// <summary>
        /// Register a specific instance of the class.
        /// </summary>
        Instance,

        /// <summary>
        /// Manually handle the registration.
        /// </summary>
        /// <remarks>
        /// The attribute must include the interfaces to register.
        /// </remarks>
        Manual
    }
}