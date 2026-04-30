using Honua.Sdk.Abstractions.Scenes;
using Honua.Sdk.Scenes.Extensions;
using Microsoft.Extensions.DependencyInjection;

namespace Honua.Sdk.Scenes.Tests;

public sealed class ServiceCollectionExtensionsTests
{
    [Fact]
    public void AddHonuaScenes_RegistersConcreteClientAndAbstraction()
    {
        var services = new ServiceCollection();

        services.AddHonuaScenes(options =>
        {
            options.BaseAddress = new Uri("https://api.honua.test");
            options.EnableRetry = false;
        });

        using var provider = services.BuildServiceProvider();
        var concrete = provider.GetRequiredService<HonuaSceneClient>();
        var abstraction = provider.GetRequiredService<IHonuaSceneClient>();

        Assert.NotNull(concrete);
        Assert.IsType<HonuaSceneClient>(abstraction);
    }
}
