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
using XQuinn.Private;
using System.Runtime.ExceptionServices;

using XQuinn.Runtime;

namespace XQuinn.Private.NavigatorEngine
{


    internal sealed class Reflector : ExpandedCoreObject
    {
        TypeString? _implicit_this => _core._implicit_this;
        Dictionary<MethodKey, MethodBase> _methods => _core._methods;
        public Reflector(NavigatorCore navig) : base(navig)
        {

        }

        public static bool SupportedMember(MethodBase method, ParameterInfo[] parameters)
        {
            if (method is MethodInfo mthd && mthd.ReturnType.IsByRef)
                return false;
            return !parameters.Any(x => x.IsOut || x.IsIn || x.ParameterType.IsByRef);
        }

        internal Type FindReference(IMemberString member, out object? instance)
        {
            //  if (string.IsNullOrWhiteSpace(member.DeclaringType?.String)) { variable = new("this", _instance); return LoadedType ?? throw new InvalidOperationException("cannot implicitly access loaded type, no type is loaded"); }
            //        if (member.DeclaringType == null)
            //          throw new ArgumentNullException();
            if (_fields.TryGetValue(member.DeclaringType.StringID, out FieldInfo? field))
            {
                instance = field.GetValue(_instance) ?? throw new ArgumentException($"Field {field} in type {_loadedType} returned null and it's member methods and fields cannot be invoked.");
                return instance.GetType(); //will not always be == fieldtype
            }
            if (_props.TryGetValue(member.DeclaringType.StringID, out PropertyInfo? prop))
            {
                instance = prop.GetValue(_instance, NavigatorCore.Flag, null, null, null) ?? throw new ArgumentException($"Property {member.DeclaringType.NameOrValue} in type {_loadedType} returned null and it's member methods and fields cannot be invoked.");
                return instance.GetType();
            }
            if (_variables.TryGetValue(member.DeclaringType.StringID, out VariableBinding? variable))
            {
                instance = variable.Object;
                return variable.ObjectType;

            }
            instance = null;
            return FindType(member.DeclaringType, false);
        }


        internal Type FindType(TypeString typename, bool staticLoadOrCasting)
        {
            if (!staticLoadOrCasting)
            {
                // if (_variables.TryGetValue(typename.NameOrValue, out VariableBinding? variable))
                //     return variable.ObjectType;
                if (typename.StringID.EqualsCaseless("this"))
                    return _instanceType == null ? throw new InvalidOperationException("Cannot pass this, instance is null.") : _loadedType!;
                if (typename.StringID.EqualsCaseless(_implicit_this?.StringID))
                    return _loadedType!;
            }
            if (typename.StringID.EqualsCaseless("base"))
            {
                if (_instanceType == null)
                    throw new InvalidOperationException("Cannot get instance base, instance is null.");
                return _instanceType.BaseType ?? throw new ArgumentException("Base type of instance is null.");
            }
            Type? t = null;
            GenericKey key = new(typename);
            LocalCache?.TryGetValue(key, out t);
            t ??= TypeCache.GetTypeOrThrow(key);
            if (t.IsGenericTypeDefinition)
            {
                if (InternalCache.s_reified_generic_types.TryGetValue(typename.StringID, out Type? generic))
                    return generic;
                t = typename.ConvertToGeneric(t, LocalCache);
                if (Caching)
                    InternalCache.s_reified_generic_types[typename.StringID] = t;
            }
            else if (!t.IsGenericType && typename.Generics.Count > 0)
                throw new ArgumentException($"type {t} does not accept type arguments.");
            return t;

        }

        internal MethodBase FindMethod(MethodString method)// out ResolvedOverload? query)
        {
            _methods.TryGetValue(MethodKey.MethodQuery(method), out MethodBase? methodbase);
            if (methodbase == null)
                throw new MissingMethodException($"No method named {method.NameOrValue}  with generic arg count {method.Generics.Count} found in {_loadedType}'s method dictionary. It may have been removed due to having a ref return type or in/out/ref parameters.");
            if (methodbase.IsGenericMethodDefinition && methodbase is MethodInfo actualmethod)
                methodbase = method.ConvertToGeneric(actualmethod, LocalCache);
            return methodbase;
        }
        internal readonly struct Assignment
        {
            public readonly object Member;
            public readonly Type ObjectType;
            public Assignment(object member)
            {
                if (member is VariableBinding variable)
                    ObjectType = variable.ObjectType;
                else if (member is PropertyInfo prop)
                    ObjectType = prop.PropertyType;
                else if (member is FieldInfo f)
                    ObjectType = f.FieldType;
                else
                    throw new NotSupportedException();
                Member = member;
            }
            public void SetValue(object? instance, object? value)
            {
                if (Member is PropertyInfo prop)
                    prop.SetValue(instance, value, NavigatorCore.Flag, null, null, null);
                else if (Member is FieldInfo field)
                    field.SetValue(instance, value, NavigatorCore.Flag, null, null);
                else if (Member is VariableBinding variable)
                    variable.Object = value!; //Object cant be null, but object handles throwing over this internally
            }
        }
    }

    internal sealed class VariableBinding
    {
        public object Object { get => _object; set => _object = value ?? throw new ArgumentException("Variables cannot be assigned null."); }
        public Type ObjectType => _object.GetType();
        object _object;
        internal VariableBinding(object instance)
        {
            _object = instance;
        }

        public override string ToString()
        {
            return $"ObjectType: {ObjectType} :: ObjectToString: {_object}";
        }
    }




}