using Bot;
using Bot.Commands;
using Microsoft.Extensions.Configuration;

var builder = Host.CreateApplicationBuilder(args);

// Worker 类型在 Bot 项目里，能定位到 UserSecretsId
builder.Configuration.AddUserSecrets<Worker>(optional: true);

builder.Services.AddHostedService<Worker>();

// message commands
builder.Services.AddSingleton<ITelegramCommand, StartCommand>();
builder.Services.AddSingleton<ITelegramCommand, HelpCommand>();

// callback commands（先预留，后续会添加实现）
// builder.Services.AddSingleton<ITelegramCallbackCommand, XxxCallbackCommand>();

var host = builder.Build();
host.Run();