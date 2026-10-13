using System.Reflection;
using XQuinn.Extensions;
using XQuinn.LangInterp.SyntaxTree;
using System;

using HarmonyLib;

namespace XQuinn.Runtime.NavigatorEngine
{
    sealed class Parser : CoreObject
    {
        public Parser(NavigatorCore navig) : base(navig)
        {
        }

        public object?[] ParseParameters(ParameterInfo[] parameters, MethodString method)
        {
            int inputAmount = method.Params.Count;
            int reqAmount = parameters.Length;
            int lastparam = reqAmount - 1;
            if (reqAmount == 0)
                return inputAmount == 0 ? Array.Empty<object>() : throw new TargetParameterCountException($"input param count: {method.Params.Count} required count: {parameters.Length} method name {method.StringID}");
            object?[] args = new object[parameters.Length];
            if (method.Params.Count != reqAmount)
                UnequalParamCount(inputAmount, reqAmount, parameters, args, method, lastparam);
            else if (parameters[lastparam].IsDefined(typeof(ParamArrayAttribute)))
                ParamsArray(lastparam, parameters, args, method);
            else
                for (int i = 0; i < parameters.Length; i++)
                    args[i] = NewArg(method.Params[i], parameters[i].ParameterType);
            return args;
        }

        void UnequalParamCount(int inputAmount, int reqAmount, ParameterInfo[] parameters, object?[] args, MethodString method, int lastparam)
        {
            if (inputAmount < reqAmount)
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
                for (int i = 0; i < inputAmount; i++)
                    args[i] = NewArg(method.Params[i], parameters[i].ParameterType);
            }
            else if (lastparam >= 0 && parameters[lastparam].IsDefined(typeof(ParamArrayAttribute)))
            {
                ParamsArray(lastparam, parameters, args, method);
            }
            else
                throw new TargetParameterCountException($"input param count: {method.Params.Count} required count: {parameters.Length} method name {method.Name}");
        }

        void ParamsArray(int lastparam, ParameterInfo[] parameters, object?[] args, MethodString method)
        {
            for (int i = 0; i < lastparam; i++)
                args[i] = NewArg(method.Params[i], parameters[i].ParameterType);
            Type elementType = parameters[lastparam].ParameterType.GetElementType() ?? throw new ArgumentNullException();
            object? defaultValue = elementType.GetDefaultValue();
            if (method.Params.Count == parameters.Length)
            {
                ArgumentString parameter = method.Params[lastparam];
                if (parameter is ValueString val && (val.Argument.EqualsCaseless("null") || val.Argument.EqualsCaseless("default")))
                    args[lastparam] = null;
                else
                {
                    object? arg = NewArg(parameter, elementType);
                    if (!arg?.GetType().IsArray ?? true)
                    {
                        Array singleArgArray = Array.CreateInstance(elementType, 1);
                        if (arg != null && !arg.Equals(defaultValue))
                            singleArgArray.SetValue(arg, 0);
                        arg = singleArgArray;
                    }
                    args[lastparam] = arg;
                }
            }
            else
            {
                Array paramsArray = Array.CreateInstance(elementType, method.Params.Count - lastparam);
                for (int i = lastparam; i < method.Params.Count; i++)
                {
                    object? arg = NewArg(method.Params[i], elementType);
                    if (arg != null && !arg.Equals(defaultValue))
                        paramsArray.SetValue(arg, i - lastparam);
                }
                args[lastparam] = paramsArray;
            }
        }

        internal object? NewArg(ArgumentString arg, Type paramType)
        {
            object? obj = arg switch
            {
                FieldString field => _core._invoker.InvokeFieldOrProperty(field),
                MethodString method => _core._invoker.InvokeMethod(method),
                ValueString value => ParseValue(value, paramType),
                _ => throw new NotSupportedException()
            };

            Reflector.NotNullable(paramType, obj);
            return obj;
        }
        internal object? ParseValue(ValueString value, Type paramType)
        {
            string strng = value.Argument;
            if (strng.EqualsCaseless("this"))
            {
                if (_core._object == null)
                    throw new InvalidOperationException("Cannot pass this as parameter, instance is null.");

                return _core._object;
            }
            if (_core._variables.TryGetValue(strng, out ReferenceBinding? variable))
            {
                return variable.Object;
            }
            if (_core._fields.TryGetValue(strng, out FieldInfo? field))
            {
                return field.GetValue(_core._object);
            }
            if (_core._props.TryGetValue(strng, out PropertyInfo? prop))
            {
                return prop.GetValue(_core._object, NavigatorCore.Flag, null, null, null);
            }
            return value.Parse(paramType);
        }
    }
}
