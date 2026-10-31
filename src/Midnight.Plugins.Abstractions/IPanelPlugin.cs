using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace Midnight.Plugins.Abstractions;

// TODO: Phase 7 - plugin loading from plugins/, ui components, dashboard widgets and context actions.
// The interface is defined early so the api server can reference it, it is not loaded yet.
public interface IPanelPlugin
{
    string Name { get; }
    string Version { get; }

    void ConfigureServices(IServiceCollection services);

    void Configure(WebApplication app);
}
