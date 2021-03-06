﻿using Bot.Common;
using Bot.Telegram.Core.Abstraction;
using Bot.Telegram.CoreAbstraction;
using Bot.Telegram.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Bot.Telegram.Core
{
	public sealed class TelegramBot
	{
		private readonly int _pollingSleepTime;
		private readonly IList<IUpdateProcessor> _processors;
		private readonly IApiProvider _apiProvider;

		public TelegramBot()
		{
			_pollingSleepTime = 500;

			var appConfig = new AppConfig();
			_apiProvider = new ApiProvider(appConfig);
			_processors = new List<IUpdateProcessor>();
		}

		public static TelegramBot Create() => new TelegramBot();

		public void AddModule<T>(T module) where T : IUpdateProcessor
			=> _processors.Add(module);
		public void AddModule<T>() where T : IUpdateProcessor, new() 
			=> _processors.Add(new T());
		public void AddModule<T>(Func<T> initFunc) where T : IUpdateProcessor 
			=> _processors.Add(initFunc());

		public async Task StartSafePollingAsync()
		{
			long offset = 0;
			IEnumerable<Update> data = null;

			while (true)
			{
				data = (await _apiProvider.GetUpdatesAsync(offset)).Result.Where(i => i.UpdateId > offset);

				if (!data.Any())
					continue;

				if (offset != 0)
				{
					foreach (var item in data)
					{
						var processingResults = _processors.Select(s => s.ProcessAsync(item));
						if (!processingResults.Any())
							continue;

						try
						{
							foreach (var result in processingResults)
							{
								var request = await result;
								if (request == null)
									continue;

								await _apiProvider.SendMessageAsync(request);
							}
						}
						catch (Exception ex)
						{
							//TODO: add exception logging
						}
					}
				}

				offset = data.Last().UpdateId;
				Thread.Sleep(_pollingSleepTime);
			}
		}
	}
}
