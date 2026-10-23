using System.Reflection;
using XQuinn.Extensions;
using XQuinn.LangInterp.SyntaxTree;
using System;

namespace XQuinn.Runtime.NavigatorEngine
{
    sealed class Parser : CoreObject
    {
        public Parser(NavigatorCore navig) : base(navig)
        {
        }

        public object?[] ParseArguments(ParameterInfo[] parameters, MethodString method)
        {
            int inputAmount = method.Params.Count;
            int reqAmount = parameters.Length;
            int lastparam = reqAmount - 1;
            if (reqAmount == 0)
                return inputAmount == 0 ? Array.Empty<object>() : throw new TargetParameterCountException($"input param count: {method.Params.Count} required count: {parameters.Length} method name {method.StringID}");
            object?[] args = new object[parameters.Length];
            if (inputAmount < reqAmount)
                DefaultArgs(inputAmount, reqAmount, parameters, args, method);
            else if (parameters[lastparam].IsDefined(typeof(ParamArrayAttribute)))
                return NewArray(lastparam, parameters, args, method);
            else if (inputAmount > reqAmount)
                throw new TargetParameterCountException($"input param count: {method.Params.Count} required count: {parameters.Length} method name {method.Name}");
            for (int i = 0; i < inputAmount; i++)
                args[i] = NewArg(method.Params[i], parameters[i].ParameterType);
            return args;
        }

        static void DefaultArgs(int inputAmount, int reqAmount, ParameterInfo[] parameters, object?[] args, MethodString method)
        {

            for (int i = inputAmount; i < reqAmount; i++)
            {
                ParameterInfo parameter = parameters[i];
                if (parameter.HasDefaultValue)
                    args[i] = parameter.DefaultValue;
                else if (parameter.IsDefined(typeof(ParamArrayAttribute)))
                {
                    Type elementType = parameters[i].ParameterType.GetElementType() ?? throw new ArgumentNullException();
                    args[i] = Array.CreateInstance(elementType, 0);
                }
                else
                    throw new TargetParameterCountException($"Parameter {parameter} does not have a default value. Input param count {inputAmount} Required count {reqAmount} method name {method.StringID}");
            }
        }

        object?[] NewArray(int lastparam, ParameterInfo[] parameters, object?[] args, MethodString method)
        {
            for (int i = 0; i < lastparam; i++)
                args[i] = NewArg(method.Params[i], parameters[i].ParameterType);
            Type elementType = parameters[lastparam].ParameterType.GetElementType() ?? throw new ArgumentNullException();
            if (method.Params.Count == parameters.Length)
            {
                ArgumentString argument = method.Params[lastparam];
                if (!(argument is ValueString val && (val.Argument.EqualsCaseless("null") || val.Argument.EqualsCaseless("default"))))
                {
                    object? arg = NewArg(argument, elementType);
                    if (!arg?.GetType().IsArray ?? true)
                    {
                        Array newArray = Array.CreateInstance(elementType, 1);
                        newArray.SetValue(arg, 0);
                        arg = newArray;
                    }
                    args[lastparam] = arg;
                }
                else
                    args[lastparam] = null;
            }
            else
            {
                Array newArray = Array.CreateInstance(elementType, method.Params.Count - lastparam);
                for (int i = lastparam; i < method.Params.Count; i++)
                {
                    object? arg = NewArg(method.Params[i], elementType);
                    newArray.SetValue(arg, i - lastparam);
                }
                args[lastparam] = newArray;
            }
            return args;
        }

        internal object? NewArg(ArgumentString arg, Type paramType)
        {
            object? obj = arg switch
            {
                FieldString field => Core.Invoker.InvokeFieldOrProperty(field),
                MethodString method => Core.Invoker.InvokeMethod(method),
                ValueString value => ParseValue(value, paramType),
                _ => throw new NotSupportedException()
            };
            Reflector.NotNullable(paramType, obj);
            return obj;
        }
        internal object? ParseValue(ValueString value, Type paramType)
        {
            if (value.Argument.EqualsCaseless("this"))
                return Core.Object ?? throw new InvalidOperationException("Cannot pass this as parameter, instance is null.");
            if (Core.References.TryGetValue(value.Argument, out Reference reference))
                return reference.Object;
            if (Core.Fields.TryGetValue(value.Argument, out FieldInfo? field))
                return field.GetValue(Core.Object);
            if (Core.Props.TryGetValue(value.Argument, out PropertyInfo? prop))
                return prop.GetValue(Core.Object, NavigatorCore.Flag, null, null, null);
            return value.Parse(paramType);
        }
    }
}
