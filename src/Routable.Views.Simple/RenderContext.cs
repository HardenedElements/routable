using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;

namespace Routable.Views.Simple
{
	public class RenderContext<TContext, TRequest, TResponse>
		where TContext : RoutableContext<TContext, TRequest, TResponse>
		where TRequest : RoutableRequest<TContext, TRequest, TResponse>
		where TResponse : RoutableResponse<TContext, TRequest, TResponse>
	{
		protected RoutableOptions<TContext, TRequest, TResponse> Options { get; private set; }
		protected SimpleViewOptions<TContext, TRequest, TResponse> ViewOptions => Options.TryGetFeatureOptions<SimpleViewOptions<TContext, TRequest, TResponse>>(out var details) ? details : throw new InvalidOperationException("Failed to load configuration");

		private Stack<object> ModelStack = new Stack<object>();
		public object Model => ModelStack.Peek();
		private Dictionary<string, string> Cache = new Dictionary<string, string>();

		public RenderContext(RoutableOptions<TContext, TRequest, TResponse> options) => Options = options;

		public void Push(object model)
		{
			ModelStack.Push(model);
			Cache.Clear();
		}
		public void Pop()
		{
			ModelStack.Pop();
			Cache.Clear();
		}

		public bool TryGetValue(string expression, bool onlyUseModel, out string value)
		{
			if(Cache.TryGetValue(expression, out value) == true) {
				return true;
			}

			var pathComponents = expression.Split('.');
			if(TryGetModelValue(Model, pathComponents, out value) == true) {
				Cache.Add(expression, value);
				return true;
			} else if(onlyUseModel == false && ViewOptions.TryResolveUnresolvedModelKey(expression, pathComponents, Model, out value) == true) {
				return true;
			}
			return false;
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

	}
}
