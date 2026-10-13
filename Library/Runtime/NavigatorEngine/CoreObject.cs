namespace XQuinn.Runtime.NavigatorEngine
{
    internal abstract class CoreObject
    {
        protected readonly NavigatorCore _core;
        public CoreObject(NavigatorCore navig)
        {
            _core = navig;
        }
    }
}
