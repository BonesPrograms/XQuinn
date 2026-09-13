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
        protected readonly Navigator _navig;
        protected object? _instance => _navig._instance;
        protected Dictionary<string, FieldInfo> _fields => _navig._fields;
        protected Dictionary<string, PropertyInfo> _props => _navig._props;
        protected Dictionary<string, VariableBinding> _variables => _navig._variables;

        public CoreObject(Navigator navig)
        {
            _navig = navig;
        }
    }

    internal abstract class ExpandedCoreObject : CoreObject
    {

        protected Type? _instanceType => _navig._instanceType;
        protected Type? _loadedType => _navig._loadedType;
        protected bool Caching => _navig.Caching;
        protected TypeBook? LocalCache => _navig.LocalCache;
        public ExpandedCoreObject(Navigator navig) : base(navig)
        {

        }
    }
}
