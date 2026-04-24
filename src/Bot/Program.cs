using Application.Bottles;
using Application.Bottles.Contracts;
using Application.Common;
using Application.Users.Contracts;
using Bot;
using Bot.Commands;
using Infrastructure.Bottles;
using Infrastructure.Users;
using Microsoft.Extensions.Configuration;
using Application.Conversations.Contracts;
using Infrastructure.Conversations;
using Application.Conversations;
using Application.Risk;
using Application.Risk.Contracts;
using Infrastructure.Risk;
using Bot.Moderation;

var builder = Host.CreateApplicationBuilder(args);

builder.Configuration.AddUserSecrets<Worker>(optional: true);

// Application
builder.Services.AddSingleton<IClock, SystemClock>();
builder.Services.AddSingleton<BottleService>();
builder.Services.AddSingleton<ConversationService>();
builder.Services.AddSingleton<ModerationService>();

builder.Services.AddSingleton<IReportRepository, InMemoryReportRepository>();
builder.Services.AddSingleton<IUserBlockRepository, InMemoryUserBlockRepository>();
builder.Services.AddSingleton<IBanRepository, InMemoryBanRepository>();

// Infrastructure (InMemory)
builder.Services.AddSingleton<IBottleRepository, InMemoryBottleRepository>();
builder.Services.AddSingleton<IPickupRepository, InMemoryPickupRepository>();
builder.Services.AddSingleton<IUserStateRepository, InMemoryUserStateRepository>();
builder.Services.AddSingleton<IConversationThreadRepository, InMemoryConversationThreadRepository>();
builder.Services.AddSingleton<IConversationMessageRepository, InMemoryConversationMessageRepository>();

// Bot hosted worker
builder.Services.AddHostedService<Worker>();

// message commands
builder.Services.AddSingleton<ITelegramCommand, StartCommand>();
builder.Services.AddSingleton<ITelegramCommand, HelpCommand>();
builder.Services.AddSingleton<ITelegramCommand, MenuCommand>();
builder.Services.AddSingleton<ITelegramCommand, ComposeTextCommand>();
builder.Services.AddSingleton<ITelegramCommand, AdminCommand>();//管理员看瓶子内容

// callback commands
builder.Services.AddSingleton<ITelegramCallbackCommand, CallbackCommand>();

//AdminCommand 
builder.Services.Configure<AdminOptions>(builder.Configuration.GetSection("Moderation"));

var host = builder.Build();
host.Run();