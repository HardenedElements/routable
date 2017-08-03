using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Routable.Views.Simple
{
	internal class RenderContext<TContext, TRequest, TResponse>
		where TContext : RoutableContext<TContext, TRequest, TResponse>
		where TRequest : RoutableRequest<TContext, TRequest, TResponse>
		where TResponse : RoutableResponse<TContext, TRequest, TResponse>
	{
		protected RoutableOptions<TContext, TRequest, TResponse> Options { get; private set; }
		protected SimpleViewOptions<TContext, TRequest, TResponse> ViewOptions { get; private set; }

		private Stack<object> ModelStack = new Stack<object>();
		public object Model => ModelStack.Peek();
		private Dictionary<object, IDictionary<string, object>> Cache = new Dictionary<object, IDictionary<string, object>>();

		public RenderContext(RoutableOptions<TContext, TRequest, TResponse> options, SimpleViewOptions<TContext, TRequest, TResponse> viewOptions)
		{
			Options = options;
			ViewOptions = viewOptions;
		}

		public void Push(object model) => ModelStack.Push(model);
		public void Pop() => ModelStack.Pop();
		private bool TryHitCache(object model, string expression, out object value)
		{
			lock(Cache) {
				if(Cache.ContainsKey(model) == false) {
					value = null;
					return false;
				}

				return Cache[model].TryGetValue(expression, out value);
			}
		}
		private void UpdateCache(object model, string expression, object value)
		{
			lock(Cache) {
				if(Cache.TryGetValue(model, out var dict) == false) {
					dict = new Dictionary<string, object>();
					Cache[model] = dict;
				}

				dict.Add(expression, value);
			}
		}

		private bool TryGetValue(object model, string expression, bool onlyUseModel, out object value)
		{
			if(TryHitCache(model, expression, out value) == true) {
				return true;
			}

			var pathComponents = expression.Split('.');
			if(TryGetModelValue(model, pathComponents, out value) == true) {
				UpdateCache(model, expression, value);
				return true;
			} else if(onlyUseModel == false && ViewOptions.TryResolveUnresolvedModelKey(expression, pathComponents, model, out value) == true) {
				return true;
			}
			return false;
		}
		public bool TryGetValue(string expression, bool onlyUseModel, out object value) => TryGetValue(Model, expression, onlyUseModel, out value);
		public bool TryGetRootValue(string expression, bool onlyUseModel, out object value) => TryGetValue(ModelStack.Last(), expression, onlyUseModel, out value);
		private bool TryGetModelValue(object model, IEnumerable<string> fields, out object value)
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
				value = current;
				return true;
			}

			value = null;
			return false;
		}

	}
}
