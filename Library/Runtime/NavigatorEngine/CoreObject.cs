using System.Reflection;
using XQuinn.Extensions;
using XQuinn.Reflection;
using XQuinn.LangInterp.SyntaxTree;
using System.Linq;
using System;
using System.Collections.Generic;
using XQuinn.LangInterp;
using System.Text;
using System.Collections;
using System.Runtime.ExceptionServices;
using XQuinn.Runtime;

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
