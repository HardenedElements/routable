using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Routable.Patterns
{
	class PathRegexPattern<TContext, TRequest, TResponse> : RoutePattern<TContext, TRequest, TResponse>
		where TContext : RoutableContext<TContext, TRequest, TResponse>
		where TRequest : RoutableRequest<TContext, TRequest, TResponse>
		where TResponse : RoutableResponse<TContext, TRequest, TResponse>
	{
		public Regex Pattern { get; set; }

		public PathRegexPattern(Regex pattern) => Pattern = pattern;
		public PathRegexPattern(string pattern) => Pattern = new Regex(pattern, RegexOptions.Compiled);

		public override bool IsMatch(RoutableContext<TContext, TRequest, TResponse> context)
		{
			var match = Pattern.Match(context.Request.Uri.AbsolutePath);
			if(match.Success == true) {
				if(match.Groups != null) {
					foreach(var groupName in Pattern.GetGroupNames().Where(name => int.TryParse(name, out _) == false)) {
						AddParameter(context, groupName, match.Groups[groupName].Value);
					}
				}
				return true;
			} else {
				return false;
			}
		}
	}
}
