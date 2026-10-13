using System.Reflection;
using XQuinn.Extensions;
using XQuinn.Reflection;
using XQuinn.LangInterp.SyntaxTree;
using System.Linq;
using System;

namespace XQuinn.Runtime.NavigatorEngine
{
    internal sealed class Reflector : CoreObject
    {
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
            if (_core._variables.TryGetValue(member.DeclaringType.StringID, out ReferenceBinding? variable))
            {
                instance = variable.Object;
                return variable.Object.GetType();
            }
            if (_core._fields.TryGetValue(member.DeclaringType.StringID, out FieldInfo? field))
            {
                instance = field.GetValue(_core._object) ?? throw new ArgumentException($"Field {field} in type {_core._static_type} returned null and it's member methods and fields cannot be invoked.");
                return instance.GetType();
            }
            if (_core._props.TryGetValue(member.DeclaringType.StringID, out PropertyInfo? prop))
            {
                instance = prop.GetValue(_core._object, NavigatorCore.Flag, null, null, null) ?? throw new ArgumentException($"Property {member.DeclaringType.Name} in type {_core._static_type} returned null and it's member methods and fields cannot be invoked.");
                return instance.GetType();
            }

            instance = null;
            return FindType(member.DeclaringType, false);
        }

        internal Type FindType(TypeString typename, bool staticLoadOrCasting)
        {
            if (!staticLoadOrCasting)
            {
                if (typename.StringID.EqualsCaseless("this"))
                    return _core._object_type == null ? throw new InvalidOperationException("Cannot pass this, instance is null.") : _core._static_type!;
                if (typename.StringID.EqualsCaseless(_core._implicit_this?.StringID))
                    return _core._static_type!;
            }
            if (typename.StringID.EqualsCaseless("base"))
            {
                if (_core._object_type == null)
                    throw new InvalidOperationException("Cannot get instance base, instance is null.");
                return _core._object_type.BaseType ?? throw new ArgumentException("Base type of instance is null.");
            }
            return typename.ToType(_core.LocalCache);
        }

        internal MethodBase FindMethod(MethodString method)
        {
            _core._methods.TryGetValue(MethodKey.MethodQuery(method), out MethodBase? methodbase);
            if (methodbase == null)
                throw new MissingMethodException($"No method named {method.Name}  with generic arg count {method.Generics.Count} found in {_core._static_type}'s method dictionary. It may have been removed due to having a ref return type or in/out/ref parameters.");
            if (methodbase.IsGenericMethodDefinition && methodbase is MethodInfo actualmethod)
                methodbase = method.ConvertToGeneric(actualmethod, _core.LocalCache);
            return methodbase;
        }

        internal static void NotNullable(Type type, object? value)
        {
            if (value == null)
            {
                if (!(type.IsClass || (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>))))
                    throw new ArgumentException($"Cannot assign null to type {type}.");
            }
        }
        internal readonly struct Assignment
        {
            public readonly MemberInfo Member;
            public readonly Type ObjectType;
            public Assignment(MemberInfo member)
            {
                ObjectType = member switch
                {
                    PropertyInfo prop => prop.PropertyType,
                    FieldInfo field => field.FieldType,
                    _ => throw new NotSupportedException()
                };
                Member = member;
            }
            public void SetValue(object? instance, object? value)
            {
                NotNullable(ObjectType, value);
                if (Member is PropertyInfo prop)
                    prop.SetValue(instance, value, NavigatorCore.Flag, null, null, null);
                else if (Member is FieldInfo field)
                    field.SetValue(instance, value, NavigatorCore.Flag, null, null);
            }
        }
    }

    internal sealed class ReferenceBinding
    {
        public readonly object Object;
        internal ReferenceBinding(object instance)
        {
            Object = instance;
        }
        public override string ToString()
        {
            string? objToStr = Object is MemberInfo inf ? ReflectionPrinter.Print(inf, false) : Object.ToString();
            return $"ObjectType: {ReflectionPrinter.Print(Object.GetType(), false)} :: ToString: {objToStr}";
        }
    }
}
