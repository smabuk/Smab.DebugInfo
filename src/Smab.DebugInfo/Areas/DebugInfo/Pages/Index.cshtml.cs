using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;

#if NETCOREAPP2_2
using Microsoft.AspNetCore.Hosting;
#endif
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Configuration;
#if NETCOREAPP3_0
	using Microsoft.Extensions.Hosting;
#endif
#if NET5_0
	using Microsoft.Extensions.Hosting;
#endif
using Microsoft.Extensions.Logging;

using Smab.DebugInfo.Helpers;
using Smab.DebugInfo.Models;

namespace Smab.DebugInfo.Pages
{
	public partial class DebugInfoModel : PageModel
	{

		public SortedDictionary<string, string> EnvironmentVariablesInfo = new();
		public SortedDictionary<string, string> RequestHeadersInfo = new();
		public SortedDictionary<string, string> QueryStringsInfo = new();
		public SortedDictionary<string, string> CookiesInfo = new();
		public string EnvironmentFromJson = "";
		public string ConfigFromJson = "";
		public string ProviderInfo = "";
		public MVCStructure MvcInfo = new();
		public AssembliesInformation AssembliesInfo = new();

		public string PreformattedMessage { get; set; } = "";

		private readonly System.Text.StringBuilder strText = new("");

#if NETCOREAPP2_2
		private readonly IHostingEnvironment _env;
#elif NETCOREAPP3_0
        private readonly IHostEnvironment _env;
#elif NET5_0
        private readonly IHostEnvironment _env;
#endif
		private readonly IConfiguration _config;
		private readonly ILogger _logger;

		public DebugInfoModel(
			ILogger<DebugInfoModel> logger,
#if NETCOREAPP2_2
			IHostingEnvironment env,
#elif NETCOREAPP3_0
			IHostEnvironment env,
#elif NET5_0
			IHostEnvironment env,
#endif
			IConfiguration config
			)
		{
			_logger = logger;
			_env = env;
			_config = config;
		}

		public void OnGet()
		{
			Debug.WriteLine($"Starting OnGet in {nameof(DebugInfoModel.OnGet)}");

			EnvironmentVariablesInfo.Clear();
			foreach (DictionaryEntry de in Environment.GetEnvironmentVariables())
			{
				string key = de.Key.ToString()?.ToLowerInvariant() ?? "";
				if (key.Contains("secret") || key.Contains("key") || key.Contains("password") || key.Contains("pwd") || key.StartsWith("rules") || key.EndsWith("address"))
				{
					EnvironmentVariablesInfo.TryAdd(de.Key.ToString() ?? "", "****************");
				}
				else
				{
					EnvironmentVariablesInfo.TryAdd(de.Key.ToString() ?? "", de.Value?.ToString() ?? "");
				}
			}
			foreach (KeyValuePair<string, Microsoft.Extensions.Primitives.StringValues> hd in Request.Headers)
			{
				RequestHeadersInfo.TryAdd(hd.Key.ToString(), hd.Value.ToString());
			}
			foreach (KeyValuePair<string, Microsoft.Extensions.Primitives.StringValues> q in Request.Query)
			{
				QueryStringsInfo.TryAdd(q.Key.ToString(), q.Value.ToString());
			}
			foreach (KeyValuePair<string, string> c in Request.Cookies)
			{
				CookiesInfo.TryAdd(c.Key.ToString(), c.Value.ToString());
			}

			EnvironmentFromJson = $"EnvironmentName: {_env.EnvironmentName}";
			EnvironmentFromJson += Environment.NewLine + $"ApplicationName: {_env.ApplicationName}";
#if NETCOREAPP2_2
			EnvironmentFromJson += Environment.NewLine + $"WebRootPath: {_env.WebRootPath}";
#endif
			EnvironmentFromJson += Environment.NewLine + $"ContentRootPath: {_env.ContentRootPath}";

			JsonSerializerOptions? jsonOptions = new()
			{
				WriteIndented = true,
			};

			try
			{
				EnvironmentFromJson = JsonSerializer.Serialize<object>(_env, jsonOptions);
			}
			catch (Exception ex)
			{
				_logger.LogError("Serialize IHostingEnvironment Error: {Error}", ex.Message);

				EnvironmentFromJson = $"EnvironmentName: {_env.EnvironmentName}";
				EnvironmentFromJson += Environment.NewLine + $"ApplicationName: {_env.ApplicationName}";
#if NETCOREAPP2_2
				EnvironmentFromJson += Environment.NewLine + $"WebRootPath: {_env.WebRootPath}";
#endif
				EnvironmentFromJson += Environment.NewLine + $"ContentRootPath: {_env.ContentRootPath}";
			}

#if NETCOREAPP2_2
			try {
				//ConfigFromJson = JsonSerializer.Serialize(_config, _config.GetType(), jsonOptions);
				ConfigFromJson = JsonSerializer.Serialize(_config, jsonOptions);
			}
			catch (Exception ex)
			{
				ConfigFromJson += Environment.NewLine + Environment.NewLine;
				ConfigFromJson += "System.Text.JsonSerializer.Serialize(_config, jsonOptions)";
				ConfigFromJson += Environment.NewLine + $"{ex.Message}";
			}
#else
			ConfigFromJson = (_config as IConfigurationRoot).GetDebugView();
#endif
			if (ConfigFromJson == "{}" || string.IsNullOrWhiteSpace(ConfigFromJson)) {
				ConfigFromJson = "No configuration found";
			}

			DebugMvcHelper? mvcH = new();
			foreach (Type? controller in mvcH.GetControllers<Controller>().OrderBy(i => i.Name))
			{
				MVCStructure.MVCController? c = new MVCStructure.MVCController
				{
					Name = controller.Name
				};

				mvcH.GetDeclaredFields(controller.Name)
					.ForEach(f => c.Fields.Add(f.Name));
				mvcH.GetDeclaredMethods(controller.Name)
					.Where(m => m.IsPublic)
					.ToList()
					.ForEach(f =>
					{
						MVCStructure.MVCAction? action = new MVCStructure.MVCAction
						{
							Name = f.Name
						};
						f.GetParameters().ToList().ForEach(p => action.Parameters.Add(p.Name ?? "", p.ParameterType.ToString()));
						c.Actions.Add(action);
					});
				MvcInfo.Controllers.Add(c);
			}

			AssembliesInfo.AllAssemblies = (from a in AppDomain.CurrentDomain.GetAssemblies()
											orderby a.FullName
											select new AssemblyInfo
											{
												Name = a.GetName().Name ?? "",
												Assembly = a
											}
						  ).ToList();

			strText.AppendLine($"Runtime OS:           {System.Runtime.InteropServices.RuntimeInformation.OSDescription}");
			strText.AppendLine($"Runtime Framework:    {System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription}");
			strText.AppendLine($"Process Architecture: {System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture}");
			strText.AppendLine($"OS Architecture:      {System.Runtime.InteropServices.RuntimeInformation.OSArchitecture}");
			strText.AppendLine($"Machine Name:         {Environment.MachineName}");
			strText.AppendLine($"No of Processors:     {Environment.ProcessorCount}");
			strText.AppendLine($"System Folder:        {Environment.GetFolderPath(Environment.SpecialFolder.System)}");
			strText.AppendLine();

			strText.AppendLine($"Runtime Version: {System.Runtime.InteropServices.RuntimeEnvironment.GetSystemVersion()}");
			strText.AppendLine($"Runtime Directory: {System.Runtime.InteropServices.RuntimeEnvironment.GetRuntimeDirectory()}");
			// strText.AppendLine($"Runtime Configuration: {System.Runtime.InteropServices.RuntimeEnvironment.SystemConfigurationFile}");
			strText.AppendLine();

			strText.AppendLine($"User Domain Name:     {Environment.UserDomainName}");
			strText.AppendLine($"User Name:            {Environment.UserName}");
			if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
			{
				strText.AppendLine($"ASP.Net User Account: {System.Security.Principal.WindowsIdentity.GetCurrent().Name}");
			}
			strText.AppendLine();

			DateTime dt = DateTime.Now;

			strText.AppendLine($"CurrentThread.CurrentCulture:         {System.Threading.Thread.CurrentThread.CurrentCulture}");
			strText.AppendLine($"CurrentThread.CurrentUICulture:       {System.Threading.Thread.CurrentThread.CurrentUICulture}");
			strText.AppendLine($"Current Server Date using UI Culture: {dt.ToString("D", System.Threading.Thread.CurrentThread.CurrentUICulture)}");
			strText.AppendLine($"Current Server Time using UI Culture: {dt.ToString("T", System.Threading.Thread.CurrentThread.CurrentUICulture)}");
			strText.AppendLine($"Universal DateTime (UTC):             {dt.ToUniversalTime():u}");
			if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
			{
				strText.AppendLine($"UK Time:                              {TimeZoneInfo.ConvertTimeFromUtc(dt.ToUniversalTime(), TimeZoneInfo.FindSystemTimeZoneById("GMT Standard Time")):r}");
			}
			else
			{
				strText.AppendLine($"UK Time:                              {TimeZoneInfo.ConvertTimeFromUtc(dt.ToUniversalTime(), TimeZoneInfo.FindSystemTimeZoneById("GMT")):r}");
			};
			// strText.AppendLine($"Current TimeZone:                     {TimeZone.CurrentTimeZone.StandardName}"); // Obsolete
			// strText.AppendLine($"UTC Offset:                           {TimeZone.CurrentTimeZone.GetUtcOffset(DateTime.Now).ToString()}"); // Obsolete
			strText.AppendLine($"Current TimeZone:                     {TimeZoneInfo.Local.StandardName}");
			strText.AppendLine($"UTC Offset:                           {TimeZoneInfo.Local.GetUtcOffset(DateTime.Now)}");
			strText.AppendLine();

			strText.AppendLine($"Is Request HTTPS?:                 {Request.IsHttps}");
			strText.AppendLine($"Request.Host.Value:                {Request.Host.Value}");
			strText.AppendLine($"Request.Path.Value:                {Request.Path.Value}");
			strText.AppendLine($"Request.PathBase.Value:            {Request.PathBase.Value}");
			strText.AppendLine($"Request.HttpContext IpAddress:     {Request.HttpContext.Connection.LocalIpAddress}");
			strText.AppendLine($"Request.HttpContext Identity.Name: {Request.HttpContext.User.Identity?.Name}");
			strText.AppendLine();

			PreformattedMessage = strText.ToString();

			strText.Clear();

			IConfigurationRoot configurationRoot = (IConfigurationRoot)_config;
			foreach (IConfigurationProvider provider in configurationRoot.Providers)
			{
				strText.AppendLine($"Provider: {provider.ToString()?.Replace("Microsoft.Extensions.Configuration.", "")}");
				if (provider is FileConfigurationProvider f)
				{
					strText.AppendLine($"  Source: {f.Source.Path}");
				}

				List<string> keys = new();
				string value = "";

				foreach (string? key in GetProviderKeys(provider, null))
				{
					provider.TryGet(key, out value);
					if (!string.IsNullOrWhiteSpace(value))
					{
						if (key.ToLowerInvariant().Contains("secret") || key.ToLowerInvariant().Contains("key") || key.ToLowerInvariant().Contains("password") || key.ToLowerInvariant().Contains("pwd") || key.ToLowerInvariant().StartsWith("rules") || key.ToLowerInvariant().EndsWith("address"))
						{
							value = "********";
						}
						strText.AppendLine($"  {key,40}   ==>   {value}");
					}
				}
			}
			strText.AppendLine();
			ProviderInfo = strText.ToString();

		}


		private static IEnumerable<string> GetProviderKeys(IConfigurationProvider provider, string? parentPath)
		{
			string? prefix = parentPath == null
					? string.Empty
					: parentPath + ConfigurationPath.KeyDelimiter;

			List<string> keys = new();
			List<string>? childKeys = provider.GetChildKeys(Enumerable.Empty<string>(), parentPath)
				.Distinct()
				.Select(k => prefix + k).ToList();
			keys.AddRange(childKeys);
			foreach (string? key in childKeys)
			{
				keys.AddRange(GetProviderKeys(provider, key));
			}

			return keys;
		}

	}
}
