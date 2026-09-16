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

namespace XQuinn.Runtime.NavigatorEngine
{


    sealed class Parser : CoreObject
    {
        Invoker _invoker => _core._invoker;
        public Parser(NavigatorCore navig) : base(navig)
        {
        }

        /// <summary>
        /// Convert a method AST object to actual parameters by matching it to a MethodInfo's parameter array.
        /// </summary>
        public object?[] ParseParameters(ParameterInfo[] parameters, MethodString invocation)
        {
            int inputAmount = invocation.Params.Count;
            int reqAmount = parameters.Length;
            int lastparam = reqAmount - 1;
            if (reqAmount == 0)
                return inputAmount == 0 ? Array.Empty<object>() : throw new TargetParameterCountException($"input param count: {invocation.Params.Count} required count: {parameters.Length} method name {invocation.Name}");
            object?[] args = new object[parameters.Length];
            if (invocation.Params.Count != reqAmount)
                UnequalParamCount(inputAmount, reqAmount, parameters, args, invocation, lastparam);
            else if (lastparam >= 0 && parameters[lastparam].IsDefined(typeof(ParamArrayAttribute)))
                ParamsArray(lastparam, parameters, args, invocation);
            else
                for (int i = 0; i < parameters.Length; i++)
                    args[i] = ParameterToObject(invocation.Params[i], parameters[i].ParameterType);
            return args;
        }

        void UnequalParamCount(int inputAmount, int reqAmount, ParameterInfo[] parameters, object?[] args, MethodString invocation, int lastparam)
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
                        throw new TargetParameterCountException($"Parameter {parameter} does not have a default value. Input param count {inputAmount} Required count {reqAmount} method name {invocation.Name}");
                }
                for (int i = 0; i < inputAmount; i++)
                    args[i] = ParameterToObject(invocation.Params[i], parameters[i].ParameterType);
            }
            else if (inputAmount > reqAmount && lastparam >= 0 && parameters[lastparam].IsDefined(typeof(ParamArrayAttribute)))
            {
                ParamsArray(lastparam, parameters, args, invocation);
            }
            else
                throw new TargetParameterCountException($"input param count: {invocation.Params.Count} required count: {parameters.Length} method name {invocation.Name}");
        }

    //Notes about
        void ParamsArray(int lastparam, ParameterInfo[] parameters, object?[] args, MethodString invocation)
        {
            int lastBeforeThat = lastparam - 1;
            for (int i = 0; i <= lastBeforeThat; i++)
                args[i] = ParameterToObject(invocation.Params[i], parameters[i].ParameterType);
            Type elementType = parameters[lastparam].ParameterType.GetElementType() ?? throw new ArgumentNullException();
            if (invocation.Params.Count == parameters.Length)
            {
                //this checks to see if youve sent an array. if the object is not an array or null, divert to creating an array
                object? arg = ParameterToObject(invocation.Params[lastparam], elementType);
                if( arg == null || arg is IList) //list && list.GetType() == actualParameters[lastparam].ParameterType) //i dont bother checking conversions most of the time runtime does it for me
                {
                    args[lastparam] = arg;
                    return;
                }
                else if(arg!=null) //I have some notes about this choice below
                {
                    Array singleArgArray = Array.CreateInstance(elementType, 1);
                    
                    singleArgArray.SetValue(arg, 0);
                    args[lastparam] = singleArgArray;
                    return;
                }
                //Fixes a bug wherein; if you send an already initialized array[] to a method with the 'params' keyword, it would create a new array (of arrays)
                //and assign the array you passed us as an element nested inside of the new array.
            }
            //constructs nonarray args into an array
            Array paramsArray = Array.CreateInstance(elementType, invocation.Params.Count - lastparam); //assign array type via reflection becuase object[] wont work for params keyword
            for (int i = lastparam; i < invocation.Params.Count; i++)
                paramsArray.SetValue(ParameterToObject(invocation.Params[i], elementType), i - lastparam);
            args[lastparam] = paramsArray;
        }
        //Notes:
        //If you send 'null' as your single argument to a 'params' array, I have a choice between creating an array of size 1 with a null value at index 0,
        //or I have the choice of treating the entire argument as 'null' and returning 'null' for the params array.
        //Experimentation reveals that if you send 'null' as a single argument to a params array in C#, the params array is assigned null
        //However, if you send more than one 'null' argument to a params array, it will create an array of that size with it's elements null
        //So we match that decision here as well.
        //In essence if you want to have a 'params array' compile as a size 1 array with a single null element, you actually need to create your own array
        //and pass it instead rather than utilizing the 'params' feature.
        internal object? ParameterToObject(ParameterString value, Type paramType)
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
                    throw new ArgumentException($"Expected method syntax for type {paramType}, but received {value.Argument}");
            }
            return obj;

        }


        internal object? ParseValue(ValueString value, Type paramType) //need half and int128 support
        {
            string strng = value.Argument;
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
