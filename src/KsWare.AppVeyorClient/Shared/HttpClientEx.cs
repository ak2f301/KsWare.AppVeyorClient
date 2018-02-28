﻿using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Mime;
using System.Security;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Serialization;
using Common.Logging;
using JetBrains.Annotations;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;

using static System.Net.Http.HttpMethod;

namespace KsWare.AppVeyorClient {

	public sealed class HttpClientEx {

		// TODO remove out of class
		private static readonly JsonSerializerSettings JsonSerializerSettings = new JsonSerializerSettings {
			ContractResolver = new CamelCasePropertyNamesContractResolver(),
		};

		private static readonly ILog Log = LogManager.GetLogger<HttpClientEx>();

		private SecureString _secureToken;
		private readonly HttpClient _httpClient;

		public HttpClientEx(SecureString token) {
			_secureToken = token;
			_httpClient = new HttpClient();
		}

		public HttpClientEx(string unsecureToken) : this(CreateSecureToken(unsecureToken)) {}

		public Uri BaseUri { get; set; }

		public bool HasToken => _secureToken != null && _secureToken.Length > 0;

		public EventHandler TokenChanged;

		public static SecureString CreateSecureToken(string unsecureToken) {
			var s = new SecureString();
			if (unsecureToken != null) foreach (var c in unsecureToken) s.AppendChar(c);
			return s;
		}

		public void SetToken(SecureString secureToken) {
			_secureToken = secureToken;
			TokenChanged?.BeginInvoke(this, EventArgs.Empty, null, null);
		}

		private string UnsecureToken =>
			System.Runtime.InteropServices.Marshal.PtrToStringAuto(
				System.Runtime.InteropServices.Marshal.SecureStringToBSTR(_secureToken));


//		private string GetUrl(string api) {
//			if(string.IsNullOrWhiteSpace(Protocoll)) throw new InvalidOperationException("Protocol not specified.");
//			if(string.IsNullOrWhiteSpace(Server)) throw new InvalidOperationException("Server not specified.");
//			if (!api.StartsWith("/")) api = "/" + api;
//			return $"{Protocoll}://{Server}{api}";
//			return new Uri(BaseUri, api);
//		}

		public async Task<T> GetJsonAsync<T>(string api) {
			var content = await SendAsync("GET", api, null, null);
			try {
				// return JsonConvert.DeserializeObject<T>(content);

				var o = JsonConvert.DeserializeObject<T>(content, JsonSerializerSettings);
				var ot = JsonConvert.SerializeObject(o,Formatting.Indented, JsonSerializerSettings);
				var j = JsonConvert.DeserializeObject(content, JsonSerializerSettings);
				var jt = JsonConvert.SerializeObject(j, Formatting.Indented, JsonSerializerSettings);
				if (ot != jt) {
					Debug.WriteLine($"JSON: incomplete deserialized! {typeof(T).FullName}");
					var p1=Path.Combine(Path.GetTempPath(), "mine - {C29066B1-293D-4CAA-AE55-5EF68764F184}.json");
					var p2=Path.Combine(Path.GetTempPath(), "origin - {00E1C157-7B19-41C6-87AB-DAA57EFC3D9D}.json");
					File.WriteAllText(p1,ot);
					File.WriteAllText(p2,jt);
					Process.Start(@"C:\Program Files (x86)\WinMerge\WinMergeU.exe", $"\"{p1}\" \"{p2}\"");
				}
				return o;
			}
			catch (Exception ex) {
				ex.Data.Add("ResponseText", content);
				throw;
			}
		}

		public string GetJsonText(string api) => RunSync(async () => await GetJsonTextAsync(api));

		public string GetJsonText(string api, out Exception exception) => RunSync(async () => await GetJsonTextAsync(api),out exception);

		public async Task<string> GetJsonTextAsync(string api) => await SendAsync("GET", api); 

		public async Task<string> GetTextAsync(string api) => await SendAsync("GET", api); 

		public async Task PutTextAsync(string api, string text) => await SendAsync("PUT", api, text, "text/plain");

		public async Task PutJsonAsync(string api, object json) {
			var jsonString = ToJsonString(json);
			await SendAsync("PUT", api, jsonString, "application/json");
		}

		public async Task PutJsonTextAsync(string api, string jsonString) {
			await SendAsync("PUT", api, jsonString, "application/json");
		}

		public async Task PostJsonAsync(string api, object json) {
			var jsonString = JsonConvert.SerializeObject(json);
			await SendAsync("POST", api, jsonString, "application/json");
		}

		private async Task<string> SendAsync(HttpRequestMessage message) {
			Debug.WriteLine($"{message.Method} {message.RequestUri.PathAndQuery}");

			using (var response = await _httpClient.SendAsync(message)) {
				Log.Trace(response.StatusCode);
				//TODO response.Content.Headers.ContentType
				var responseContent = await response.Content.ReadAsStringAsync();
				if (!response.IsSuccessStatusCode) {
					//  400 (Invalid input parameters. See response body for detailed error message.)
					// {"message":"The request is invalid.","modelState":{"variables.Headers":["An error has occurred."]}}
					try {
						response.EnsureSuccessStatusCode();
					}
					catch (Exception ex) {
						ex.Data.Add("Response.StatusCode", response.StatusCode);
						ex.Data.Add("Response",            response.ToString());
						ex.Data.Add("Response.Body",       responseContent);
						throw;
					}
				}
				return responseContent;
			}
		}

		private async Task<string> SendAsync(string method, string api, string content = null, string contentType=null) {
			Debug.WriteLine($"{method} {api}");
			var message=CreateRequest(method,api,content,contentType);
			return await SendAsync(message);
		}

		private HttpRequestMessage CreateRequest(string method, string api, string content, string contentType) {
			var m = new HttpMethod(method.ToUpperInvariant());
			var r = new HttpRequestMessage(m, new Uri(BaseUri,api));
			if(contentType!=null) r.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue(contentType));
			r.Headers.Authorization = new AuthenticationHeaderValue("Bearer", UnsecureToken); // make optional/configurable
			if (content != null) {
				//TODO validate contentType
				r.Content=new StringContent(content,Encoding.UTF8,contentType);
			}
			return r;
		}

		private string ToJsonString(object json) {
			return JsonConvert.SerializeObject(json,settings: JsonSerializerSettings);
		}

		private static readonly TaskFactory _myTaskFactory = new TaskFactory(CancellationToken.None, TaskCreationOptions.None,
			TaskContinuationOptions.None, TaskScheduler.Default);

		public static T RunSync<T>(Func<Task<T>> func) {
			var cultureUi = CultureInfo.CurrentUICulture;
			var culture   = CultureInfo.CurrentCulture;
			return _myTaskFactory.StartNew<Task<T>>(delegate {
				Thread.CurrentThread.CurrentCulture   = culture;
				Thread.CurrentThread.CurrentUICulture = cultureUi;
				return func();
			}).Unwrap<T>().GetAwaiter().GetResult();
		}
		// Helper.RunSync(new Func<Task<ReturnTypeGoesHere>>(async () => await AsyncCallGoesHere(myparameter)));

		public static RunSyncResult<T> RunSync2<T>(Func<Task<T>> func) {
			try {
				var cultureUi = CultureInfo.CurrentUICulture;
				var culture   = CultureInfo.CurrentCulture;
				var r = _myTaskFactory.StartNew<Task<T>>(delegate {
					Thread.CurrentThread.CurrentCulture   = culture;
					Thread.CurrentThread.CurrentUICulture = cultureUi;
					return func();
				}).Unwrap<T>().GetAwaiter().GetResult();
				return new RunSyncResult<T>(r);
			}
			catch (Exception ex) {
				return new RunSyncResult<T>(ex);
			}
		}

		public static T RunSync<T>(Func<Task<T>> func, out Exception exception) {
			exception = null;
			try {
				var cultureUi = CultureInfo.CurrentUICulture;
				var culture   = CultureInfo.CurrentCulture;
				return _myTaskFactory.StartNew<Task<T>>(delegate {
					Thread.CurrentThread.CurrentCulture   = culture;
					Thread.CurrentThread.CurrentUICulture = cultureUi;
					return func();
				}).Unwrap<T>().GetAwaiter().GetResult();
			}
			catch (Exception ex) {
				exception = ex;
				return default(T);
			}
		}

		public class RunSyncResult<T> {

			public RunSyncResult(T result) { Result = result; }

			public RunSyncResult([NotNull] Exception exception) {
				Exception = exception ?? throw new ArgumentNullException(nameof(exception));
			}

			public T Result { get; }

			public Exception Exception { get; }
		}


	}

	public static class HttpClientExLogExtension {
		public static void Trace(this ILog log, HttpStatusCode statusCode) {
			log.Trace($"done {(int) statusCode} {statusCode}");
		}
	}
}
