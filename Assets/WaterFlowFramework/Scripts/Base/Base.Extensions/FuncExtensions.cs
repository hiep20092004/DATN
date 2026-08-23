using System;

namespace WaterFlow.Framework.Base.Extensions
{
    public static class FuncExtensions
    {
        public static bool InvokeOr(this Func<bool> func)
        {
            bool result = false;

            if (func != null)
            {
                foreach (var @delegate in func.GetInvocationList())
                {
                    var d = (Func<bool>)@delegate;
                    result |= d.Invoke();
                }
            }

            return result;
        }

        public static bool InvokeAnd(this Func<bool> func)
        {
            bool result = true;

            if (func != null)
            {
                foreach (var @delegate in func.GetInvocationList())
                {
                    var d = (Func<bool>)@delegate;
                    result &= d.Invoke();
                }
            }

            return result;
        }

        public static bool InvokeAnd<T>(this Func<T, bool> func, T arg)
        {
            bool result = true;

            if (func != null)
            {
                foreach (var @delegate in func.GetInvocationList())
                {
                    var d = (Func<T, bool>)@delegate;
                    result &= d.Invoke(arg);
                }
            }

            return result;
        }
    }
}