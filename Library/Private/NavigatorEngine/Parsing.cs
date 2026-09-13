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


        sealed class Parser : CoreObject
        {
            Invoker _invoker => _navig._invoker;
            public Parser(NavigatorCore navig) : base(navig)
            {
            }

            /// <summary>
            /// Convert a method AST object to actual parameters by matching it to a MethodInfo's parameter array.
            /// </summary>
            public object?[] ParseParameters(ParameterInfo[] actualParameters, MethodString invocation)
            {
                int inputAmount = invocation.Params.Count;
                int reqAmount = actualParameters.Length;
                int lastparam = reqAmount - 1;
                if (reqAmount == 0)
                    return inputAmount == 0 ? Array.Empty<object>() : throw new TargetParameterCountException($"input param count: {invocation.Params.Count} required count: {actualParameters.Length} method name {invocation.NameOrValue}");
                object?[] prms = new object[actualParameters.Length];
                if (invocation.Params.Count != reqAmount)
                    UnequalParamCount(inputAmount, reqAmount, actualParameters, prms, invocation, lastparam);
                else if (lastparam >= 0 && actualParameters[lastparam].IsDefined(typeof(ParamArrayAttribute)))
                    ParamsArray(lastparam, actualParameters, prms, invocation);
                else
                    for (int i = 0; i < actualParameters.Length; i++)
                        prms[i] = ParameterToObject(invocation.Params[i], actualParameters[i].ParameterType);
                return prms;
            }

            void UnequalParamCount(int inputAmount, int reqAmount, ParameterInfo[] actualParameters, object?[] prms, MethodString invocation, int lastparam)
            {
                if (inputAmount < reqAmount)
                {
                    for (int i = inputAmount; i < reqAmount; i++)
                    {
                        ParameterInfo parameter = actualParameters[i];
                        if (parameter.HasDefaultValue)
                            prms[i] = parameter.DefaultValue;
                        else if (parameter.IsDefined(typeof(ParamArrayAttribute)))
                        {
                            Type elementType = actualParameters[i].ParameterType.GetElementType() ?? throw new ArgumentNullException();
                            prms[i] = Array.CreateInstance(elementType, 0);
                        }
                        else
                            throw new TargetParameterCountException($"Parameter {parameter} does not have a default value. Input param count {inputAmount} Required count {reqAmount} method name {invocation.NameOrValue}");
                    }
                    for (int i = 0; i < inputAmount; i++)
                        prms[i] = ParameterToObject(invocation.Params[i], actualParameters[i].ParameterType);
                }
                else if (inputAmount > reqAmount && lastparam >= 0 && actualParameters[lastparam].IsDefined(typeof(ParamArrayAttribute)))
                {
                    ParamsArray(lastparam, actualParameters, prms, invocation);
                }
                else
                    throw new TargetParameterCountException($"input param count: {invocation.Params.Count} required count: {actualParameters.Length} method name {invocation.NameOrValue}");
            }

            void ParamsArray(int lastparam, ParameterInfo[] actualParameters, object?[] prms, MethodString invocation)
            {
                int lastBeforeThat = lastparam - 1;
                for (int i = 0; i <= lastBeforeThat; i++)
                    prms[i] = ParameterToObject(invocation.Params[i], actualParameters[i].ParameterType);
                Type elementType = actualParameters[lastparam].ParameterType.GetElementType() ?? throw new ArgumentNullException();
                if (invocation.Params.Count == actualParameters.Length)
                {
                    object? potentialArray = ParameterToObject(invocation.Params[lastparam], elementType);
                    if (potentialArray is IList)
                    {
                        prms[lastparam] = potentialArray;
                        return;
                    }
                    //Fixes a bug wherein; if you send an already initialized array[] to a method with the 'params' keyword, it would create a new array (of arrays)
                    //and assign the array you passed us as an element nested inside of the new array.
                }

                Array paramArray = Array.CreateInstance(elementType, invocation.Params.Count - lastparam); //assign array type via reflection becuase object[] wont work for params keyword
                for (int i = lastparam; i < invocation.Params.Count; i++)
                    paramArray.SetValue(ParameterToObject(invocation.Params[i], elementType), i - lastparam);
                prms[lastparam] = paramArray;
            }
            //This sorts between whether or not a parameter is a method invocation as a parameter, or an actual primitive/string value.
            object? ParameterToObject(ParameterString value, Type paramType)
            {
                object? obj;
                if (value is FieldString field)
                    obj = _invoker.InvokeFieldOrProperty(field);
                else if (value is MethodString method)
                    obj = _invoker.InvokeMethod(method);
                else
                {
                    obj = ParseValue((ValueString)value, paramType);
                    if (obj == null && !(paramType.IsClass || (paramType.IsGenericType && paramType.GetGenericTypeDefinition() == typeof(Nullable<>))))
                        throw new ArgumentException($"Expected method syntax for type {paramType}, but received {value.NameOrValue}");
                }
                return obj;

            }


            internal object? ParseValue(ValueString value, Type paramType) //need half and int128 support
            {
                string strng = value.NameOrValue;
                if (strng.EqualsCaseless("this"))
                {
                    if (_instance == null)
                        throw new InvalidOperationException("Cannot pass this as parameter, instance is null.");
                    // if (!paramType.IsAssignableFrom(_instanceType))
                    //     throw new InvalidCastException($"Current instance is a {_instanceType} and does not cast to parameter type {paramType}");
                    return _instance;
                }
                if (_variables?.TryGetValue(strng, out VariableBinding? variable) ?? false)
                {
                    // if (!paramType.IsAssignableFrom(variable.ObjectType))
                    //     throw new InvalidCastException($"Instance index object with key {strng} is a {variable.ObjectType} and does not cast to parameter type {paramType}");
                    return variable.Object;
                }
                if (_fields.TryGetValue(strng, out FieldInfo? field))
                {
                    // object? val = field.GetValue(_instance);
                    // Type t = val == null ? field.FieldType : val.GetType();
                    // if(!paramType.IsAssignableFrom(t))
                    return field.GetValue(_instance);
                }
                if (_props.TryGetValue(strng, out PropertyInfo? prop))
                {
                    return prop.GetValue(_instance, NavigatorCore.Flag, null, null, null);
                }
                return value.Parse(paramType);
            }
        }
    }
