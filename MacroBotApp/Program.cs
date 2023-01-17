﻿using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using MacroBot.Config;
using MacroBot.DataAccess;
using MacroBot.DataAccess.AutoMapper;
using MacroBot.DataAccess.Repositories;
using MacroBot.DataAccess.RepositoryInterfaces;
using MacroBot.Discord.Modules.Tagging;
using MacroBot.Extensions;
using MacroBot.ServiceInterfaces;
using MacroBot.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;

namespace MacroBot;

public static class Program {
	public static async Task Main(string[] args)
	{
		Log.Logger = new LoggerConfiguration()
			.WriteTo.Console()
			.CreateLogger();
		
		// ReSharper disable once HeapView.ClosureAllocation
		var botConfig = await BotConfig.LoadAsync(Constants.BotConfigPath);
		var commandsConfig = await CommandsConfig.LoadAsync(Constants.CommandsConfigPath);
		var statusCheckConfig = await StatusCheckConfig.LoadAsync(Constants.StatusCheckConfigPath);
		
		DiscordSocketConfig discordSocketConfig = new() {
			AlwaysDownloadUsers = true,
			MaxWaitBetweenGuildAvailablesBeforeReady = (int)new TimeSpan(0, 0, 15).TotalMilliseconds,
			MessageCacheSize = 100,
			GatewayIntents = GatewayIntents.All,
		};

		InteractionServiceConfig interactionServiceConfig = new()
		{
			AutoServiceScopes = true,
			DefaultRunMode = RunMode.Async
		};
		
		var builder = Host.CreateDefaultBuilder(args)
			.UseSerilog()
			.ConfigureServices(services =>
			{
				services.AddDbContext<MacroBotContext>();
				services.AddAutoMapper(typeof(TagMapping));
				services.AddTransient<ITagRepository, TagRepository>();
				services.AddTransient<TaggingUtils>();
				services.AddSingleton(botConfig);
				services.AddSingleton(commandsConfig);
				services.AddSingleton(discordSocketConfig);
				services.AddSingleton(statusCheckConfig);
				services.AddSingleton<DiscordSocketClient>();
				services.AddSingleton(x => new InteractionService(x.GetRequiredService<DiscordSocketClient>(),
					interactionServiceConfig));
				services.AddSingleton<CommandHandler>();
				services.AddInjectableHostedService<IDiscordService, DiscordService>();
				services.AddInjectableHostedService<IStatusCheckService, StatusCheckService>();
				services.AddInjectableHostedService<ITimerService, TimerService>();
				services.AddHttpClient();
			});

		var app = builder.Build();
		app.CheckAndCreateDirectories();
		await app.MigrateDatabaseAsync();
		await app.RunAsync();
	}
}