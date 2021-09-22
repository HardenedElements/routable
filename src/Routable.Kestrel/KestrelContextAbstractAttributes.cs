namespace Routable.Kestrel
{
	internal class KestrelContextAbstractAttributes : AbstractContextAttributes
	{
		private KestrelRoutableContext Context;
		public KestrelContextAbstractAttributes(KestrelRoutableContext context) => Context = context;

		public override void RemovePerRequestItem(string name) => Context.PerRequestItems.Remove(name);
		public override void SetPerRequestItem(string name, object value) => Context.PerRequestItems[name] = value;
		public override bool TryGetPerRequestItem(string name, out object value) => Context.PerRequestItems.TryGetValue(name, out value);
	}
}
