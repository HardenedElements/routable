using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Routable
{
	public abstract class RoutableOptions<TContext, TRequest, TResponse>
		where TContext : RoutableContext<TContext, TRequest, TResponse>
		where TRequest : RoutableRequest<TContext, TRequest, TResponse>
		where TResponse : RoutableResponse<TContext, TRequest, TResponse>
	{
		/// <summary>
		/// String encoding to use around the routable framework. (default is UTF-8)
		/// </summary>
		public virtual Encoding StringEncoding { get; set; } = Encoding.UTF8;
		private IDictionary<string, string> MimeTypes = new Dictionary<string, string>();
		/// <summary>
		/// Handlers for responses of various types.
		/// </summary>
		public virtual ResponseTypeHandlerCollection<TContext, TRequest, TResponse> ResponseTypeHandlers { get; } = new ResponseTypeHandlerCollection<TContext, TRequest, TResponse>();
		/// <summary>
		/// Handles empty responses.
		/// </summary>
		public virtual ResponseTypeHandler<TContext, TRequest, TResponse> EmptyResponseHandler { get; } = DefaultResponseTypeHandlers.EmptyResponseTypeHandler;
		/// <summary>
		/// Handles any response type not handled by a configured response type handler.
		/// </summary>
		public virtual ResponseTypeHandler<TContext, TRequest, TResponse> DefaultResponseHandler { get; } = DefaultResponseTypeHandlers.StringResponseTypeHandler;
		private Dictionary<RoutableEventPipelines, IList<Routing<TContext, TRequest, TResponse>>> Routing = new Dictionary<RoutableEventPipelines, IList<Routing<TContext, TRequest, TResponse>>>();
		/// <summary>
		/// Factory used to create routes when routes are authored.
		/// </summary>
		public virtual RouteFactory<TContext, TRequest, TResponse> RouteFactory { get; set; } = new RouteFactory<TContext, TRequest, TResponse>();
		private Dictionary<Type, object> FeatureOptions = new Dictionary<Type, object>();
		public IRoutableLogger Logger { get; protected set; } = new DefaultConsoleLogger();

		protected RoutableOptions()
		{
			AddDefaultMimeTypes();
			AddDefaultResponseTypeHandlers();
		}

		private async Task<bool> InvokeRouting(RoutableEventPipelines eventPipeline, TContext context, bool ignoreCompletion)
		{
			// obtain list of routing objects.
			IList<Routing<TContext, TRequest, TResponse>> routing;
			lock(Routing) {
				if(Routing.TryGetValue(eventPipeline, out routing) == false) {
					return false;
				}
			}

			// obtain a snapshot of the routes.
			var routeCollections = new List<List<Route<TContext, TRequest, TResponse>>>();
			lock(routing) {
				foreach(var r in routing) {
					routeCollections.Add(r.Routes.Where(_ => _.IsMatch(context)).ToList());
				}
			}

			// invoke each route until one succeeds (unless ignoreCompletion).
			bool wasCompletedSuccessfully = false;
			foreach(var routeCollection in routeCollections) {
				foreach(var route in routeCollection) {
					if(await route.Invoke(context) == true) {
						if(ignoreCompletion == true) {
							wasCompletedSuccessfully = true;
							break;
						} else {
							return true;
						}
					}
				}
			}

			return wasCompletedSuccessfully;
		}
		protected async Task<bool> InvokeRouting(TContext context)
		{
			try {
				bool wasRequestHandled = false;

				do {
					// invoke initialize routes.
					if(await InvokeRouting(RoutableEventPipelines.RouteEventInitialize, context, false) == true) {
						wasRequestHandled = true;
						break;
					}

					// invoke main routes.
					if(await InvokeRouting(RoutableEventPipelines.RouteEventMain, context, false) == true) {
						wasRequestHandled = true;
						break;
					}
				} while(false);

				if(wasRequestHandled == true) {
					// invoke finalize routes.
					await InvokeRouting(RoutableEventPipelines.RouteEventFinalize, context, true);
					await context.Response.Finalize();
					return true;
				} else {
					// invoke unhandled route finalizer.
					if(await InvokeRouting(RoutableEventPipelines.RouteEventFinalizeUnhandledRequests, context, false) == true) {
						// in that case, invoke finalize routes.
						await InvokeRouting(RoutableEventPipelines.RouteEventFinalize, context, true);
						await context.Response.Finalize();
						return true;
					}
				}
			} catch(Exception ex) {
				// invoke error handling routes.
				context.Error = ex;
				context.Response.ClearPendingWrites();
				if(await InvokeRouting(RoutableEventPipelines.RouteEventError, context, true) == true) {
					await context.Response.Finalize();
					return true;
				}
			}

			return false;
		}

		private IList<Routing<TContext, TRequest, TResponse>> GetEventPipelineRouting(RoutableEventPipelines eventPipeline)
		{
			lock(Routing) {
				if(Routing.TryGetValue(eventPipeline, out var list) == false) {
					list = new List<Routing<TContext, TRequest, TResponse>>();
					Routing.Add(eventPipeline, list);
					return list;
				} else {
					return list;
				}
			}
		}
		/// <summary>
		/// Add a routing instance to handle requests.
		/// </summary>
		public RoutableOptions<TContext, TRequest, TResponse> AddRouting(Routing<TContext, TRequest, TResponse> routing)
		{
			var list = GetEventPipelineRouting(RoutableEventPipelines.RouteEventMain);
			lock(list) {
				list.Add(routing);
			}
			return this;
		}
		/// <summary>
		/// Synonym for AppendRoutingToEventPipeline.
		/// </summary>
		public RoutableOptions<TContext, TRequest, TResponse> AddRouting(RoutableEventPipelines eventPipeline, Routing<TContext, TRequest, TResponse> routing) => AppendRoutingToEventPipeline(eventPipeline, routing);
		/// <summary>
		/// Append a routing instance to handle requests to the specified pipeline.
		/// </summary>
		/// <seealso cref="RoutableEventPipelines"/>
		public RoutableOptions<TContext, TRequest, TResponse> AppendRoutingToEventPipeline(RoutableEventPipelines eventPipeline, Routing<TContext, TRequest, TResponse> routing)
		{
			var list = GetEventPipelineRouting(eventPipeline);
			lock(list) {
				list.Add(routing);
			}
			return this;
		}
		/// <summary>
		/// Prepend a routing instance to handle requests to the specified pipeline.
		/// </summary>
		/// <seealso cref="RoutableEventPipelines"/>
		public RoutableOptions<TContext, TRequest, TResponse> PrependRoutingToEventPipeline(RoutableEventPipelines eventPipeline, Routing<TContext, TRequest, TResponse> routing)
		{
			var list = GetEventPipelineRouting(eventPipeline);
			lock(list) {
				list.Insert(0, routing);
			}
			return this;
		}
		/// <summary>
		/// Set handler for unhandled errors.
		/// </summary>
		public RoutableOptions<TContext, TRequest, TResponse> OnError(Routing<TContext, TRequest, TResponse> routing)
		{
			AppendRoutingToEventPipeline(RoutableEventPipelines.RouteEventError, routing);
			return this;
		}
		/// <summary>
		/// Set a different logger than the default.
		/// </summary>
		public RoutableOptions<TContext, TRequest, TResponse> UseLogger(IRoutableLogger logger)
		{
			Logger = logger;
			return this;
		}
		/// <summary>
		/// Get detailed options of an arbitrary type to make available to routable requests and components.
		/// </summary>
		/// <typeparam name="TFeatureOptions">A plain old class</typeparam>
		/// <param name="details">A plain old class representing configuration items</param>
		/// <returns>Indicates whether or not the details exist</returns>
		public bool TryGetFeatureOptions<TFeatureOptions>(out TFeatureOptions details)
			where TFeatureOptions : class
		{
			if(FeatureOptions.TryGetValue(typeof(TFeatureOptions), out var value) == false) {
				details = null;
				return false;
			} else {
				details = value as TFeatureOptions;
				return true;
			}
		}
		/// <summary>
		/// Set detailed options of an arbitrary type to make available to routable requests and components.
		/// </summary>
		/// <typeparam name="TFeatureOptions">A plain old class</typeparam>
		/// <param name="details">A plain old class representing configuration items</param>
		public void SetFeatureOptions<TFeatureOptions>(TFeatureOptions details) => FeatureOptions[typeof(TFeatureOptions)] = details;
		/// <summary>
		/// Add mime type for a given file extension.
		/// </summary>
		/// <param name="extension">File extension (should start with a period, otherwise one will be added)</param>
		/// <param name="mimeType">MIME type (eg. text/html)</param>
		public void AddMimeType(string extension, string mimeType) => MimeTypes[extension?.StartsWith(".") == true ? extension : $".{extension}"] = mimeType;
		/// <summary>
		/// Try to get a mime type for a file.
		/// </summary>
		/// <param name="extension">File extension for the inquery</param>
		/// <param name="mimeType">Variable to write the mime type to if it is found</param>
		/// <returns>True or false, indicating if the mime type was found</returns>
		public bool TryGetMimeType(string extension, out string mimeType) => MimeTypes.TryGetValue(extension, out mimeType);
		/// <summary>
		/// Remove mime type for a given file extension.
		/// </summary>
		/// <param name="extension">File extension to remove mime type for</param>
		public void RemoveMimeType(string extension) => MimeTypes.Remove(extension);
		/// <summary>
		/// Remove all mime types.
		/// </summary>
		public void ClearMimeTypes() => MimeTypes.Clear();
		private void AddDefaultResponseTypeHandlers()
		{
			ResponseTypeHandlers.Add(typeof(object), DefaultResponseTypeHandlers.StringResponseTypeHandler);
			ResponseTypeHandlers.Add(typeof(string), DefaultResponseTypeHandlers.StringResponseTypeHandler);
			ResponseTypeHandlers.Add(typeof(byte[]), DefaultResponseTypeHandlers.ByteArrayResponseTypeHandler);
		}
		private void AddDefaultMimeTypes()
		{
			AddMimeType(".323", "text/h323");
			AddMimeType(".aaf", "application/octet-stream");
			AddMimeType(".aca", "application/octet-stream");
			AddMimeType(".accdb", "application/msaccess");
			AddMimeType(".accde", "application/msaccess");
			AddMimeType(".accdt", "application/msaccess");
			AddMimeType(".acx", "application/internet-property-stream");
			AddMimeType(".afm", "application/octet-stream");
			AddMimeType(".ai", "application/postscript");
			AddMimeType(".aif", "audio/x-aiff");
			AddMimeType(".aifc", "audio/aiff");
			AddMimeType(".aiff", "audio/aiff");
			AddMimeType(".application", "application/x-ms-application");
			AddMimeType(".art", "image/x-jg");
			AddMimeType(".asd", "application/octet-stream");
			AddMimeType(".asf", "video/x-ms-asf");
			AddMimeType(".asi", "application/octet-stream");
			AddMimeType(".asm", "text/plain");
			AddMimeType(".asr", "video/x-ms-asf");
			AddMimeType(".asx", "video/x-ms-asf");
			AddMimeType(".atom", "application/atom+xml");
			AddMimeType(".au", "audio/basic");
			AddMimeType(".avi", "video/x-msvideo");
			AddMimeType(".axs", "application/olescript");
			AddMimeType(".bas", "text/plain");
			AddMimeType(".bcpio", "application/x-bcpio");
			AddMimeType(".bin", "application/octet-stream");
			AddMimeType(".bmp", "image/bmp");
			AddMimeType(".c", "text/plain");
			AddMimeType(".cab", "application/octet-stream");
			AddMimeType(".calx", "application/vnd.ms-office.calx");
			AddMimeType(".cat", "application/vnd.ms-pki.seccat");
			AddMimeType(".cdf", "application/x-cdf");
			AddMimeType(".chm", "application/octet-stream");
			AddMimeType(".class", "application/x-java-applet");
			AddMimeType(".clp", "application/x-msclip");
			AddMimeType(".cmx", "image/x-cmx");
			AddMimeType(".cnf", "text/plain");
			AddMimeType(".cod", "image/cis-cod");
			AddMimeType(".cpio", "application/x-cpio");
			AddMimeType(".cpp", "text/plain");
			AddMimeType(".crd", "application/x-mscardfile");
			AddMimeType(".crl", "application/pkix-crl");
			AddMimeType(".crt", "application/x-x509-ca-cert");
			AddMimeType(".csh", "application/x-csh");
			AddMimeType(".css", "text/css");
			AddMimeType(".csv", "application/octet-stream");
			AddMimeType(".cur", "application/octet-stream");
			AddMimeType(".dcr", "application/x-director");
			AddMimeType(".deploy", "application/octet-stream");
			AddMimeType(".der", "application/x-x509-ca-cert");
			AddMimeType(".dib", "image/bmp");
			AddMimeType(".dir", "application/x-director");
			AddMimeType(".disco", "text/xml");
			AddMimeType(".dll", "application/x-msdownload");
			AddMimeType(".dll.config", "text/xml");
			AddMimeType(".dlm", "text/dlm");
			AddMimeType(".doc", "application/msword");
			AddMimeType(".docm", "application/vnd.ms-word.document.macroEnabled.12");
			AddMimeType(".docx", "application/vnd.openxmlformats-officedocument.wordprocessingml.document");
			AddMimeType(".dot", "application/msword");
			AddMimeType(".dotm", "application/vnd.ms-word.template.macroEnabled.12");
			AddMimeType(".dotx", "application/vnd.openxmlformats-officedocument.wordprocessingml.template");
			AddMimeType(".dsp", "application/octet-stream");
			AddMimeType(".dtd", "text/xml");
			AddMimeType(".dvi", "application/x-dvi");
			AddMimeType(".dwf", "drawing/x-dwf");
			AddMimeType(".dwp", "application/octet-stream");
			AddMimeType(".dxr", "application/x-director");
			AddMimeType(".eml", "message/rfc822");
			AddMimeType(".emz", "application/octet-stream");
			AddMimeType(".eot", "application/octet-stream");
			AddMimeType(".eps", "application/postscript");
			AddMimeType(".etx", "text/x-setext");
			AddMimeType(".evy", "application/envoy");
			AddMimeType(".exe", "application/octet-stream");
			AddMimeType(".exe.config", "text/xml");
			AddMimeType(".fdf", "application/vnd.fdf");
			AddMimeType(".fif", "application/fractals");
			AddMimeType(".fla", "application/octet-stream");
			AddMimeType(".flr", "x-world/x-vrml");
			AddMimeType(".flv", "video/x-flv");
			AddMimeType(".gif", "image/gif");
			AddMimeType(".gtar", "application/x-gtar");
			AddMimeType(".gz", "application/x-gzip");
			AddMimeType(".h", "text/plain");
			AddMimeType(".hdf", "application/x-hdf");
			AddMimeType(".hdml", "text/x-hdml");
			AddMimeType(".hhc", "application/x-oleobject");
			AddMimeType(".hhk", "application/octet-stream");
			AddMimeType(".hhp", "application/octet-stream");
			AddMimeType(".hlp", "application/winhlp");
			AddMimeType(".hqx", "application/mac-binhex40");
			AddMimeType(".hta", "application/hta");
			AddMimeType(".htc", "text/x-component");
			AddMimeType(".htm", "text/html");
			AddMimeType(".html", "text/html");
			AddMimeType(".htt", "text/webviewhtml");
			AddMimeType(".hxt", "text/html");
			AddMimeType(".ico", "image/x-icon");
			AddMimeType(".ics", "application/octet-stream");
			AddMimeType(".ief", "image/ief");
			AddMimeType(".iii", "application/x-iphone");
			AddMimeType(".inf", "application/octet-stream");
			AddMimeType(".ins", "application/x-internet-signup");
			AddMimeType(".isp", "application/x-internet-signup");
			AddMimeType(".IVF", "video/x-ivf");
			AddMimeType(".jar", "application/java-archive");
			AddMimeType(".java", "application/octet-stream");
			AddMimeType(".jck", "application/liquidmotion");
			AddMimeType(".jcz", "application/liquidmotion");
			AddMimeType(".jfif", "image/pjpeg");
			AddMimeType(".jpb", "application/octet-stream");
			AddMimeType(".jpe", "image/jpeg");
			AddMimeType(".jpeg", "image/jpeg");
			AddMimeType(".jpg", "image/jpeg");
			AddMimeType(".js", "application/x-javascript");
			AddMimeType(".jsx", "text/jscript");
			AddMimeType(".latex", "application/x-latex");
			AddMimeType(".lit", "application/x-ms-reader");
			AddMimeType(".lpk", "application/octet-stream");
			AddMimeType(".lsf", "video/x-la-asf");
			AddMimeType(".lsx", "video/x-la-asf");
			AddMimeType(".lzh", "application/octet-stream");
			AddMimeType(".m13", "application/x-msmediaview");
			AddMimeType(".m14", "application/x-msmediaview");
			AddMimeType(".m1v", "video/mpeg");
			AddMimeType(".m3u", "audio/x-mpegurl");
			AddMimeType(".man", "application/x-troff-man");
			AddMimeType(".manifest", "application/x-ms-manifest");
			AddMimeType(".map", "text/plain");
			AddMimeType(".mdb", "application/x-msaccess");
			AddMimeType(".mdp", "application/octet-stream");
			AddMimeType(".me", "application/x-troff-me");
			AddMimeType(".mht", "message/rfc822");
			AddMimeType(".mhtml", "message/rfc822");
			AddMimeType(".mid", "audio/mid");
			AddMimeType(".midi", "audio/mid");
			AddMimeType(".mix", "application/octet-stream");
			AddMimeType(".mmf", "application/x-smaf");
			AddMimeType(".mno", "text/xml");
			AddMimeType(".mny", "application/x-msmoney");
			AddMimeType(".mov", "video/quicktime");
			AddMimeType(".movie", "video/x-sgi-movie");
			AddMimeType(".mp2", "video/mpeg");
			AddMimeType(".mp3", "audio/mpeg");
			AddMimeType(".mpa", "video/mpeg");
			AddMimeType(".mpe", "video/mpeg");
			AddMimeType(".mpeg", "video/mpeg");
			AddMimeType(".mpg", "video/mpeg");
			AddMimeType(".mpp", "application/vnd.ms-project");
			AddMimeType(".mpv2", "video/mpeg");
			AddMimeType(".ms", "application/x-troff-ms");
			AddMimeType(".msi", "application/octet-stream");
			AddMimeType(".mso", "application/octet-stream");
			AddMimeType(".mvb", "application/x-msmediaview");
			AddMimeType(".mvc", "application/x-miva-compiled");
			AddMimeType(".nc", "application/x-netcdf");
			AddMimeType(".nsc", "video/x-ms-asf");
			AddMimeType(".nws", "message/rfc822");
			AddMimeType(".ocx", "application/octet-stream");
			AddMimeType(".oda", "application/oda");
			AddMimeType(".odc", "text/x-ms-odc");
			AddMimeType(".ods", "application/oleobject");
			AddMimeType(".one", "application/onenote");
			AddMimeType(".onea", "application/onenote");
			AddMimeType(".onetoc", "application/onenote");
			AddMimeType(".onetoc2", "application/onenote");
			AddMimeType(".onetmp", "application/onenote");
			AddMimeType(".onepkg", "application/onenote");
			AddMimeType(".osdx", "application/opensearchdescription+xml");
			AddMimeType(".p10", "application/pkcs10");
			AddMimeType(".p12", "application/x-pkcs12");
			AddMimeType(".p7b", "application/x-pkcs7-certificates");
			AddMimeType(".p7c", "application/pkcs7-mime");
			AddMimeType(".p7m", "application/pkcs7-mime");
			AddMimeType(".p7r", "application/x-pkcs7-certreqresp");
			AddMimeType(".p7s", "application/pkcs7-signature");
			AddMimeType(".pbm", "image/x-portable-bitmap");
			AddMimeType(".pcx", "application/octet-stream");
			AddMimeType(".pcz", "application/octet-stream");
			AddMimeType(".pdf", "application/pdf");
			AddMimeType(".pfb", "application/octet-stream");
			AddMimeType(".pfm", "application/octet-stream");
			AddMimeType(".pfx", "application/x-pkcs12");
			AddMimeType(".pgm", "image/x-portable-graymap");
			AddMimeType(".pko", "application/vnd.ms-pki.pko");
			AddMimeType(".pma", "application/x-perfmon");
			AddMimeType(".pmc", "application/x-perfmon");
			AddMimeType(".pml", "application/x-perfmon");
			AddMimeType(".pmr", "application/x-perfmon");
			AddMimeType(".pmw", "application/x-perfmon");
			AddMimeType(".png", "image/png");
			AddMimeType(".pnm", "image/x-portable-anymap");
			AddMimeType(".pnz", "image/png");
			AddMimeType(".pot", "application/vnd.ms-powerpoint");
			AddMimeType(".potm", "application/vnd.ms-powerpoint.template.macroEnabled.12");
			AddMimeType(".potx", "application/vnd.openxmlformats-officedocument.presentationml.template");
			AddMimeType(".ppam", "application/vnd.ms-powerpoint.addin.macroEnabled.12");
			AddMimeType(".ppm", "image/x-portable-pixmap");
			AddMimeType(".pps", "application/vnd.ms-powerpoint");
			AddMimeType(".ppsm", "application/vnd.ms-powerpoint.slideshow.macroEnabled.12");
			AddMimeType(".ppsx", "application/vnd.openxmlformats-officedocument.presentationml.slideshow");
			AddMimeType(".ppt", "application/vnd.ms-powerpoint");
			AddMimeType(".pptm", "application/vnd.ms-powerpoint.presentation.macroEnabled.12");
			AddMimeType(".pptx", "application/vnd.openxmlformats-officedocument.presentationml.presentation");
			AddMimeType(".prf", "application/pics-rules");
			AddMimeType(".prm", "application/octet-stream");
			AddMimeType(".prx", "application/octet-stream");
			AddMimeType(".ps", "application/postscript");
			AddMimeType(".psd", "application/octet-stream");
			AddMimeType(".psm", "application/octet-stream");
			AddMimeType(".psp", "application/octet-stream");
			AddMimeType(".pub", "application/x-mspublisher");
			AddMimeType(".qt", "video/quicktime");
			AddMimeType(".qtl", "application/x-quicktimeplayer");
			AddMimeType(".qxd", "application/octet-stream");
			AddMimeType(".ra", "audio/x-pn-realaudio");
			AddMimeType(".ram", "audio/x-pn-realaudio");
			AddMimeType(".rar", "application/octet-stream");
			AddMimeType(".ras", "image/x-cmu-raster");
			AddMimeType(".rf", "image/vnd.rn-realflash");
			AddMimeType(".rgb", "image/x-rgb");
			AddMimeType(".rm", "application/vnd.rn-realmedia");
			AddMimeType(".rmi", "audio/mid");
			AddMimeType(".roff", "application/x-troff");
			AddMimeType(".rpm", "audio/x-pn-realaudio-plugin");
			AddMimeType(".rtf", "application/rtf");
			AddMimeType(".rtx", "text/richtext");
			AddMimeType(".scd", "application/x-msschedule");
			AddMimeType(".sct", "text/scriptlet");
			AddMimeType(".sea", "application/octet-stream");
			AddMimeType(".setpay", "application/set-payment-initiation");
			AddMimeType(".setreg", "application/set-registration-initiation");
			AddMimeType(".sgml", "text/sgml");
			AddMimeType(".sh", "application/x-sh");
			AddMimeType(".shar", "application/x-shar");
			AddMimeType(".sit", "application/x-stuffit");
			AddMimeType(".sldm", "application/vnd.ms-powerpoint.slide.macroEnabled.12");
			AddMimeType(".sldx", "application/vnd.openxmlformats-officedocument.presentationml.slide");
			AddMimeType(".smd", "audio/x-smd");
			AddMimeType(".smi", "application/octet-stream");
			AddMimeType(".smx", "audio/x-smd");
			AddMimeType(".smz", "audio/x-smd");
			AddMimeType(".snd", "audio/basic");
			AddMimeType(".snp", "application/octet-stream");
			AddMimeType(".spc", "application/x-pkcs7-certificates");
			AddMimeType(".spl", "application/futuresplash");
			AddMimeType(".src", "application/x-wais-source");
			AddMimeType(".ssm", "application/streamingmedia");
			AddMimeType(".sst", "application/vnd.ms-pki.certstore");
			AddMimeType(".stl", "application/vnd.ms-pki.stl");
			AddMimeType(".sv4cpio", "application/x-sv4cpio");
			AddMimeType(".sv4crc", "application/x-sv4crc");
			AddMimeType(".swf", "application/x-shockwave-flash");
			AddMimeType(".t", "application/x-troff");
			AddMimeType(".tar", "application/x-tar");
			AddMimeType(".tcl", "application/x-tcl");
			AddMimeType(".tex", "application/x-tex");
			AddMimeType(".texi", "application/x-texinfo");
			AddMimeType(".texinfo", "application/x-texinfo");
			AddMimeType(".tgz", "application/x-compressed");
			AddMimeType(".thmx", "application/vnd.ms-officetheme");
			AddMimeType(".thn", "application/octet-stream");
			AddMimeType(".tif", "image/tiff");
			AddMimeType(".tiff", "image/tiff");
			AddMimeType(".toc", "application/octet-stream");
			AddMimeType(".tr", "application/x-troff");
			AddMimeType(".trm", "application/x-msterminal");
			AddMimeType(".tsv", "text/tab-separated-values");
			AddMimeType(".ttf", "application/octet-stream");
			AddMimeType(".txt", "text/plain");
			AddMimeType(".u32", "application/octet-stream");
			AddMimeType(".uls", "text/iuls");
			AddMimeType(".ustar", "application/x-ustar");
			AddMimeType(".vbs", "text/vbscript");
			AddMimeType(".vcf", "text/x-vcard");
			AddMimeType(".vcs", "text/plain");
			AddMimeType(".vdx", "application/vnd.ms-visio.viewer");
			AddMimeType(".vml", "text/xml");
			AddMimeType(".vsd", "application/vnd.visio");
			AddMimeType(".vss", "application/vnd.visio");
			AddMimeType(".vst", "application/vnd.visio");
			AddMimeType(".vsto", "application/x-ms-vsto");
			AddMimeType(".vsw", "application/vnd.visio");
			AddMimeType(".vsx", "application/vnd.visio");
			AddMimeType(".vtx", "application/vnd.visio");
			AddMimeType(".wav", "audio/wav");
			AddMimeType(".wax", "audio/x-ms-wax");
			AddMimeType(".wbmp", "image/vnd.wap.wbmp");
			AddMimeType(".wcm", "application/vnd.ms-works");
			AddMimeType(".wdb", "application/vnd.ms-works");
			AddMimeType(".wks", "application/vnd.ms-works");
			AddMimeType(".wm", "video/x-ms-wm");
			AddMimeType(".wma", "audio/x-ms-wma");
			AddMimeType(".wmd", "application/x-ms-wmd");
			AddMimeType(".wmf", "application/x-msmetafile");
			AddMimeType(".wml", "text/vnd.wap.wml");
			AddMimeType(".wmlc", "application/vnd.wap.wmlc");
			AddMimeType(".wmls", "text/vnd.wap.wmlscript");
			AddMimeType(".wmlsc", "application/vnd.wap.wmlscriptc");
			AddMimeType(".wmp", "video/x-ms-wmp");
			AddMimeType(".wmv", "video/x-ms-wmv");
			AddMimeType(".wmx", "video/x-ms-wmx");
			AddMimeType(".wmz", "application/x-ms-wmz");
			AddMimeType(".wps", "application/vnd.ms-works");
			AddMimeType(".wri", "application/x-mswrite");
			AddMimeType(".wrl", "x-world/x-vrml");
			AddMimeType(".wrz", "x-world/x-vrml");
			AddMimeType(".wsdl", "text/xml");
			AddMimeType(".wvx", "video/x-ms-wvx");
			AddMimeType(".x", "application/directx");
			AddMimeType(".xaf", "x-world/x-vrml");
			AddMimeType(".xaml", "application/xaml+xml");
			AddMimeType(".xap", "application/x-silverlight-app");
			AddMimeType(".xbap", "application/x-ms-xbap");
			AddMimeType(".xbm", "image/x-xbitmap");
			AddMimeType(".xdr", "text/plain");
			AddMimeType(".xla", "application/vnd.ms-excel");
			AddMimeType(".xlam", "application/vnd.ms-excel.addin.macroEnabled.12");
			AddMimeType(".xlc", "application/vnd.ms-excel");
			AddMimeType(".xlm", "application/vnd.ms-excel");
			AddMimeType(".xls", "application/vnd.ms-excel");
			AddMimeType(".xlsb", "application/vnd.ms-excel.sheet.binary.macroEnabled.12");
			AddMimeType(".xlsm", "application/vnd.ms-excel.sheet.macroEnabled.12");
			AddMimeType(".xlsx", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
			AddMimeType(".xlt", "application/vnd.ms-excel");
			AddMimeType(".xltm", "application/vnd.ms-excel.template.macroEnabled.12");
			AddMimeType(".xltx", "application/vnd.openxmlformats-officedocument.spreadsheetml.template");
			AddMimeType(".xlw", "application/vnd.ms-excel");
			AddMimeType(".xml", "text/xml");
			AddMimeType(".xof", "x-world/x-vrml");
			AddMimeType(".xpm", "image/x-xpixmap");
			AddMimeType(".xps", "application/vnd.ms-xpsdocument");
			AddMimeType(".xsd", "text/xml");
			AddMimeType(".xsf", "text/xml");
			AddMimeType(".xsl", "text/xml");
			AddMimeType(".xslt", "text/xml");
			AddMimeType(".xsn", "application/octet-stream");
			AddMimeType(".xtp", "application/octet-stream");
			AddMimeType(".xwd", "image/x-xwindowdump");
			AddMimeType(".z", "application/x-compress");
			AddMimeType(".zip", "application/x-zip-compressed");
		}
	}
}
