using System;
using System.Linq;
using System.Reflection;

namespace Routable
{
	internal static class Extensions
	{
		public static int DistanceToType(this Type @this, Type target)
		{
			var targetTypeInfo = target.GetTypeInfo();
			var current = @this;
			var currentTypeInfo = current.GetTypeInfo();
			var root = current;
			var rootTypeInfo = currentTypeInfo;
			int distance = 0;

			while(current != null && (targetTypeInfo.IsInterface ? currentTypeInfo.ImplementedInterfaces.Contains(target) : current != target)) {
				root = current;
				rootTypeInfo = currentTypeInfo;

				current = currentTypeInfo.BaseType;
				currentTypeInfo = current?.GetTypeInfo();

				distance++;
			}

			if(current == null) {
				return -1;
			} else if(targetTypeInfo.IsInterface == false) {
				return distance;
			}

			distance--;
			var interfaces = rootTypeInfo.ImplementedInterfaces;
			while(interfaces.Contains(target)) {
				distance++;
				interfaces = interfaces.SelectMany(i => i.GetTypeInfo().ImplementedInterfaces).ToArray();
			}

			return distance;
		}
		public static int DistanceToType<T>(this Type @this) => @this.DistanceToType(typeof(T));
	}
}
