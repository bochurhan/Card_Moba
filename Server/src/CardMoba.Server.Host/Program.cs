using CardMoba.Server.Host.Config;
using CardMoba.Server.Host.Hubs;
using CardMoba.Server.Host.Services;
using CardMoba.Server.Host.Snapshots;
using Microsoft.Extensions.Logging;
using System;

var builder = WebApplication.CreateBuilder(args);
builder.Logging.ClearProviders();
builder.Logging.AddSimpleConsole(options =>
{
    options.TimestampFormat = "HH:mm:ss ";
    options.SingleLine = true;
});
builder.Logging.SetMinimumLevel(LogLevel.Information);

builder.Services.AddSignalR(options =>
{
    // MVP 阶段优先让本地联调中的掉线更快被服务端感知。
    options.KeepAliveInterval = TimeSpan.FromSeconds(2);
    options.ClientTimeoutInterval = TimeSpan.FromSeconds(6);
});

builder.Services.AddSingleton<ServerCardCatalog>();
builder.Services.AddSingleton<ServerBuildCatalogFactory>();
builder.Services.AddSingleton<ServerBattleFactoryFactory>();
builder.Services.AddSingleton<LocalMatchTemplateFactory>();
builder.Services.AddSingleton<BattleSnapshotBuilder>();
builder.Services.AddSingleton<BuildWindowSnapshotBuilder>();
builder.Services.AddSingleton<LocalMatchRegistry>();
builder.Services.AddSingleton<MatchSessionManager>();
builder.Services.AddSingleton<MatchConnectionRegistry>();

var app = builder.Build();

app.MapGet("/", () => Results.Ok(new
{
    service = "CardMoba.Server.Host",
    status = "ok",
}));
app.MapHub<MatchHub>("/hubs/match");

app.Run();
