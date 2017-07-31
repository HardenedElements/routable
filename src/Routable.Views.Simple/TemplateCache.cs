using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace Routable.Views.Simple
{
	public class TemplateCache<TContext, TRequest, TResponse>
		where TContext : RoutableContext<TContext, TRequest, TResponse>
		where TRequest : RoutableRequest<TContext, TRequest, TResponse>
		where TResponse : RoutableResponse<TContext, TRequest, TResponse>
	{
		private class Entry
		{
			public DateTime? LastModified;
			public byte[] Digest;
			public Template<TContext, TRequest, TResponse> Template;
		}
		private static Dictionary<string, Entry> Entries = new Dictionary<string, Entry>();

		private static bool IsNew(DateTime? lastModified, byte[] digest, Entry entry)
		{
			if(lastModified == null) {
				return !entry.Digest.SequenceEqual(digest);
			} else {
				return entry.LastModified == null || entry.LastModified.Value != lastModified;
			}
		}
		public static bool Fetch(string name, DateTime? lastModified, string source, out Template<TContext, TRequest, TResponse> template)
		{
			byte[] digest = null;
			if(lastModified == null) {
				using(var hash = SHA256.Create()) {
					digest = hash.ComputeHash(Encoding.UTF8.GetBytes(source));
				}
			}

			lock(Entries) {
				if(Entries.TryGetValue(name, out var entry) == false || IsNew(lastModified, digest, entry) == true) {
					template = null;
					return false;
				} else {
					template = entry.Template;
					return true;
				}
			}
		}
		public static void Add(string name, DateTime? lastModified, string source, Template<TContext, TRequest, TResponse> template)
		{
			byte[] digest = null;
			if(lastModified == null) {
				using(var hash = SHA256.Create()) {
					digest = hash.ComputeHash(Encoding.UTF8.GetBytes(source));
				}
			}

			lock(Entries) {
				Entries[name] = new Entry { LastModified = lastModified, Digest = digest, Template = template };
			}
		}
	}
}
