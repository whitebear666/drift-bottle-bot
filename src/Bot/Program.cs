using Bot;
using Bot.Commands;
using Microsoft.Extensions.Configuration;

var builder = Host.CreateApplicationBuilder(args);

// Worker 类型在 Bot 项目里，能定位到 UserSecretsId
builder.Configuration.AddUserSecrets<Worker>(optional: true);

builder.Services.AddHostedService<Worker>();
builder.Services.AddSingleton<ITelegramCommand, StartCommand>();
builder.Services.AddSingleton<ITelegramCommand, HelpCommand>();

var host = builder.Build();
host.Run();