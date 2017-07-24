using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Routable
{
	public sealed class ResponseTypeHandlerCollection<TContext, TRequest, TResponse> : IEnumerable<KeyValuePair<Type, ResponseTypeHandler<TContext, TRequest, TResponse>>>
		where TContext : RoutableContext<TContext, TRequest, TResponse>
		where TRequest : RoutableRequest<TContext, TRequest, TResponse>
		where TResponse : RoutableResponse<TContext, TRequest, TResponse>
	{
		private IDictionary<Type, ResponseTypeHandler<TContext, TRequest, TResponse>> Collection = new Dictionary<Type, ResponseTypeHandler<TContext, TRequest, TResponse>>();
		private IDictionary<Type, IDictionary<Type, int>> Distances = new Dictionary<Type, IDictionary<Type, int>>();

		public IEnumerator<KeyValuePair<Type, ResponseTypeHandler<TContext, TRequest, TResponse>>> GetEnumerator() => Collection.GetEnumerator();
		IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

		public void Add(Type type, ResponseTypeHandler<TContext, TRequest, TResponse> handler)
		{
			lock(this) {
				Collection[type] = handler;
				foreach(var pair in Distances) {
					if(pair.Value.ContainsKey(type) == false) {
						var dist = pair.Key.DistanceToType(type);
						if(dist >= 0) {
							pair.Value.Add(type, dist);
						}
					}
				}
			}
		}
		public bool TryGetHandler(Type inputType, out ResponseTypeHandler<TContext, TRequest, TResponse> handler)
		{
			lock(this) {
				if(Distances.TryGetValue(inputType, out var dict) == true) {
					var handleAsType = dict.OrderByDescending(pair => pair.Value).Select(pair => pair.Key);
					if(handleAsType == null || handleAsType.Any() == false) {
						throw new UnhandledResponseTypeException(inputType);
					}
					if(Collection.TryGetValue(handleAsType.First(), out handler) == false) {
						throw new UnhandledResponseTypeException(inputType);
					}
					return true;
				} else {
					dict = new Dictionary<Type, int>();
					Distances.Add(inputType, dict);

					Tuple<int, Type> closest = null;
					foreach(var targetType in Collection.Keys) {
						var dist = inputType.DistanceToType(targetType);
						if(dist >= 0) {
							dict.Add(targetType, dist);
							if(closest == null || closest.Item1 > dist) {
								closest = Tuple.Create(dist, targetType);
							}
						}
					}

					if(closest != null) {
						handler = Collection[closest.Item2];
						return true;
					}

					handler = null;
					return false;
				}
			}
		}
	}
}
