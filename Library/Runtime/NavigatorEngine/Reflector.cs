using System.Reflection;
using XQuinn.Extensions;
using XQuinn.Reflection;
using XQuinn.CodeAnalysis.AST;
using System.Linq;
using System;
using System.Collections.Generic;

namespace XQuinn.Runtime.NavigatorEngine
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
            if (method is ConstructorInfo ctor && ctor.IsStatic)
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
                instance = prop.GetValue(_instance, NavigatorCore.Flag, null, null, null) ?? throw new ArgumentException($"Property {member.DeclaringType.Name} in type {_loadedType} returned null and it's member methods and fields cannot be invoked.");
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
            return typename.ToType(LocalCache);

        }

        internal MethodBase FindMethod(MethodString method)// out ResolvedOverload? query)
        {
            _methods.TryGetValue(MethodKey.MethodQuery(method), out MethodBase? methodbase);
            if (methodbase == null)
                throw new MissingMethodException($"No method named {method.Name}  with generic arg count {method.Generics.Count} found in {_loadedType}'s method dictionary. It may have been removed due to having a ref return type or in/out/ref parameters.");
            if (methodbase.IsGenericMethodDefinition && methodbase is MethodInfo actualmethod)
                methodbase = method.ConvertToGeneric(actualmethod, LocalCache);
            return methodbase;
        }
        internal readonly struct Assignment
        {
            public readonly MemberInfo Member;
            public readonly Type ObjectType;
            public Assignment(MemberInfo member)
            {
                if (member is PropertyInfo prop)
                    ObjectType = prop.PropertyType;
                else if (member is FieldInfo f)
                    ObjectType = f.FieldType;
                else
                    throw new NotSupportedException($"non-assignable member {member.GetType()}.");
                Member = member;
            }
            public void SetValue(object? instance, object? value)
            {
                if (value == null) //valuestring handles direct parsing from 'null' for structs (it throws), but methods and fields can return null and are not valuestrings, so we need to manually check on assignmnet
                {                   //otherwise reflection will assign the default value if the target is a struct, rather than throwing like it should
                    if (!(ObjectType.IsClass || (ObjectType.IsGenericType && ObjectType.GetGenericTypeDefinition() == typeof(Nullable<>))))
                        throw new ArgumentException($"Cannot assign null to type {ObjectType}.");
                }
                if (Member is PropertyInfo prop)
                    prop.SetValue(instance, value, NavigatorCore.Flag, null, null, null);
                else if (Member is FieldInfo field)
                    field.SetValue(instance, value, NavigatorCore.Flag, null, null);
            }
        }
    }

    internal sealed class VariableBinding
    {
        public object Object { get => _object; private init => _object = value ?? throw new ArgumentException("Variables cannot be assigned null."); }
        public Type ObjectType => _object.GetType();
        readonly object _object;
        internal VariableBinding(object instance)
        {
            _object = instance;
        }

        public override string ToString()
        {
            return $"ObjectType: {ReflectionPrinter.Print(ObjectType, false)} :: ObjectToString: {_object}";
        }
    }




}