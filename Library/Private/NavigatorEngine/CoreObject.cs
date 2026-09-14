using System.Reflection;
using XQuinn.Extensions;
using XQuinn.Reflection;
using XQuinn.CodeAnalysis.AST;
using System.Linq;
using System;
using System.Collections.Generic;
using XQuinn.CodeAnalysis;
using System.Text;
using System.Collections;
using System.Runtime.ExceptionServices;
using XQuinn.Runtime;

namespace XQuinn.Private.NavigatorEngine
{


    internal abstract class CoreObject
    {
        protected readonly NavigatorCore _core;
        protected object? _instance => _core._instance;
        protected Dictionary<string, FieldInfo> _fields => _core._fields;
        protected Dictionary<string, PropertyInfo> _props => _core._props;
        protected Dictionary<string, VariableBinding> _variables => _core._variables;

        public CoreObject(NavigatorCore navig)
        {
            _core = navig;
        }
    }

    internal abstract class ExpandedCoreObject : CoreObject
    {

        protected Type? _instanceType => _core._instanceType;
        protected Type? _loadedType => _core._loadedType;
        protected bool Caching => _core.Caching;
        protected TypeBook? LocalCache => _core.LocalCache;
        public ExpandedCoreObject(NavigatorCore navig) : base(navig)
        {

        }
    }
}
