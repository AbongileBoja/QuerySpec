using System;
using Microsoft.Extensions.DependencyInjection;

namespace QuerySpec.DependencyInjection;

/// <summary>
/// Plugin system builder.
/// </summary>
public class PluginBuilder
{
    private readonly IServiceCollection _services;

    /// <summary>Initializes a new plugin builder.</summary>
    public PluginBuilder(IServiceCollection services)
    {
        _services = services;
    }

    /// <summary>
    /// Loads plugins from directory.
    /// </summary>
    public PluginBuilder LoadFromDirectory(string path)
    {
        return this;
    }

    /// <summary>
    /// Enables hot reloading of plugins.
    /// </summary>
    public PluginBuilder EnableHotReload()
    {
        return this;
    }

    /// <summary>
    /// Registers individual plugin.
    /// </summary>
    public PluginBuilder RegisterPlugin(Type pluginType)
    {
        return this;
    }
}
