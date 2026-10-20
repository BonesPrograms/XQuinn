namespace XQuinn.Runtime.NavigatorEngine
{
    internal abstract class CoreObject
    {
        protected NavigatorCore Core { get; private init; }
        public CoreObject(NavigatorCore navig)
        {
            Core = navig;
        }
    }
}
