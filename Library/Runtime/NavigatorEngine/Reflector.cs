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
            if (Core.References.TryGetValue(member.DeclaringType.StringID, out Reference reference))
            {
                instance = reference.Object;
                return reference.Object.GetType();
            }
            if (Core.Fields.TryGetValue(member.DeclaringType.StringID, out FieldInfo? field))
            {
                instance = field.GetValue(Core.Object) ?? throw new ArgumentException($"Field {field} in type {Core.StaticType} returned null and it's member methods and fields cannot be invoked.");
                return instance.GetType();
            }
            if (Core.Props.TryGetValue(member.DeclaringType.StringID, out PropertyInfo? prop))
            {
                instance = prop.GetValue(Core.Object, NavigatorCore.Flag, null, null, null) ?? throw new ArgumentException($"Property {member.DeclaringType.Name} in type {Core.StaticType} returned null and it's member methods and fields cannot be invoked.");
                return instance.GetType();
            }

            instance = null;
            return FindType(member.DeclaringType, false);
        }

        internal Type FindType(TypeString typename, bool cast)
        {
            if (!cast)
            {
                if (typename.StringID.EqualsCaseless("this"))
                    return Core.ObjectType == null ? throw new InvalidOperationException("Cannot pass this, instance is null.") : Core.StaticType!;
                if (typename.StringID.EqualsCaseless(Core.ImplicitRef?.StringID))
                    return Core.StaticType!;
            }
            if (typename.StringID.EqualsCaseless("base"))
            {
                if (Core.ObjectType == null)
                    throw new InvalidOperationException("Cannot get instance base, instance is null.");
                return Core.ObjectType.BaseType ?? throw new ArgumentException("Base type of instance is null.");
            }
            return typename.ToType(Core.LocalCache);
        }

        internal MethodBase FindMethod(MethodString method)
        {
            Core.Methods.TryGetValue(MethodKey.MethodQuery(method), out MethodBase? methodbase);
            if (methodbase == null)
                throw new MissingMethodException($"No method named {method.Name}  with generic arg count {method.Generics.Count} found in {Core.StaticType}'s method dictionary. It may have been removed due to having a ref return type or in/out/ref parameters.");
            if (methodbase.IsGenericMethodDefinition && methodbase is MethodInfo actualmethod)
                methodbase = method.ConvertToGeneric(actualmethod, Core.LocalCache);
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

    internal readonly struct Reference
    {
        public readonly object Object;
        internal Reference(object instance)
        {
            Object = instance;
        }
        public override string ToString()
        {
            return $"ObjectType: {Object.GetType().ToReflectionString()} :: ToString: {Object.ToReflectionString()}";
        }
    }
}
