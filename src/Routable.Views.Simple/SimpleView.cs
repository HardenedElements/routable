using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Routable.Views.Simple
{
	public sealed class SimpleView<TContext, TRequest, TResponse>
			where TContext : RoutableContext<TContext, TRequest, TResponse>
			where TRequest : RoutableRequest<TContext, TRequest, TResponse>
			where TResponse : RoutableResponse<TContext, TRequest, TResponse>
	{
		private static Regex ModelPattern = new Regex(@"\@Model(\.([\@]*[\w]+))+", RegexOptions.Compiled | RegexOptions.Multiline);
		private static Regex ModelConditionalPattern = new Regex(@"@IfSet\((?<expr>(\.([\@]*[\w]+))*)\)(?<body>(.*?\n.*?)*?)@EndIfSet", RegexOptions.Compiled | RegexOptions.Singleline);
		private SimpleViewOptions<TContext, TRequest, TResponse> Options;
		private string ViewContent;
		public string MimeType { get; set; }

		private SimpleView(SimpleViewOptions<TContext, TRequest, TResponse> options, string mimeType, string viewContent)
		{
			Options = options;
			MimeType = mimeType;
			ViewContent = viewContent;
		}

		private IEnumerable<string> GetModelMethodComponents(Group group)
		{
			foreach(var capture in group.Captures.OfType<Capture>()) {
				yield return capture.Value;
			}
		}
		private bool TryGetModelValue(object model, IEnumerable<string> fields, out string value)
		{
			var current = model;
			while(fields.Any() == true && current != null) {
				// get the first field name.
				var fieldHead = fields.FirstOrDefault();
				fields = fields.Skip(1);
				if(fieldHead == null) {
					value = null;
					return false;
				}

				// get type information.
				var typeInfo = current.GetType()?.GetTypeInfo();
				if(typeInfo == null) {
					value = null;
					return false;
				}

				// TODO: add support for index operator.
				if(typeInfo.DeclaredProperties.Any(_ => _.Name == fieldHead) == true) {
					current = typeInfo.GetDeclaredProperty(fieldHead)?.GetValue(current);
				} else if(typeInfo.DeclaredFields.Any(_ => _.IsPublic && _.IsStatic == false) == true) {
					current = typeInfo.GetDeclaredField(fieldHead)?.GetValue(current);
				}
			}

			// make sure we went through all of the fields.
			if(current != model && fields.Any() == false) {
				value = current?.ToString() ?? "";
				return true;
			}

			value = null;
			return false;
		}

		/// <summary>
		/// Render view as a string using a model.
		/// </summary>
		/// <param name="model">A model to use with the view</param>
		/// <returns>The rendered view</returns>
		public string Render(object model)
		{
			var steps = new RenderStep[] {
				RenderStepRemoveConditionals,
				RenderStepResolveModel
			};

			var content = ViewContent;
			foreach(var step in steps) {
				content = step(content, model);
			}

			return content;
		}
		private delegate string RenderStep(string input, object model);
		private string RenderStepRemoveConditionals(string input, object model)
		{
			return ModelConditionalPattern.Replace(input, match => {
				var modelParameters = GetModelMethodComponents(match.Groups[2]).ToList();
				if(TryGetModelValue(model, modelParameters, out _) == false) {
					return "";
				} else {
					return match.Groups["body"].Value;
				}
			});
		}
		private string RenderStepResolveModel(string input, object model)
		{
			return ModelPattern.Replace(input, match => {
				// get model path.
				var modelProperties = GetModelMethodComponents(match.Groups[2]);

				// get value from model.
				if(TryGetModelValue(model, modelProperties, out var value) == false && Options.TryResolveUnresolvedModelKey(MimeType, match.Value, modelProperties, model, out value) == false) {
					return $"[ERROR: unable to resolve model ({match.Value})]";
				} else {
					// TODO: consider if we can use response type handlers to do something cute here (eg. convert objects to JSON :))
					return value ?? "";
				}
			});
		}

		/// <summary>
		/// Write a rendered representation of this view to the stream using the provided model.
		/// </summary>
		/// <param name="stream">Output stream to write model</param>
		/// <param name="model">Model to render the view with</param>
		/// <returns>A task indicating completion of the write</returns>
		public Task WriteAsync(Stream stream, object model)
		{
			using(stream) {
				var rendered = Render(model);
				var bytes = Options.RoutableOptions.StringEncoding.GetBytes(rendered);
				return stream.WriteAsync(bytes, 0, bytes.Length);
			}
		}

		public static async Task<SimpleView<TContext, TRequest, TResponse>> Find(SimpleViewOptions<TContext, TRequest, TResponse> options, string viewName)
		{
			// resolve view.
			var resolveViewArgs = new ResolveViewArgs { Name = viewName };
			await options.ResolveView(resolveViewArgs);
			if(resolveViewArgs.Success == false) {
				throw new SimpleViewNotFoundException(viewName);
			}

			// read view content and return a view.
			using(var stream = await resolveViewArgs.GetStream())
			using(var reader = new StreamReader(stream)) {
				return new SimpleView<TContext, TRequest, TResponse>(options, resolveViewArgs.MimeType, await reader.ReadToEndAsync());
			}
		}
	}
}
