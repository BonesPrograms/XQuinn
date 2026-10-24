namespace XQuinn.Runtime.NavigatorEngine
{
    internal abstract class CoreObject
    {
        protected NavigatorCore Core { get; }
        public CoreObject(NavigatorCore navig)
        {
            Core = navig;
        }
    }
}
